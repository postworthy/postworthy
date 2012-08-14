using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Account;
using Postworthy.Models.Twitter;
using LinqToTwitter;
using System.Configuration;
using System.Timers;
using Postworthy.Models.Repository;
using Postworthy.Models.Streaming;
using Postworthy.Tasks.Streaming.Models;
using SignalR.Client.Hubs;


namespace Postworthy.Tasks.Streaming
{
    class Program
    {
        private const string TWEETS = "_tweets";
        private static object queue_lock = new object();
        private static object queue_push_lock = new object();
        private static List<Tweet> queue = new List<Tweet>();
        private static List<Tweet> queue_push = new List<Tweet>();
        private static int streamingHubConnectAttempts = 0;
        private static Tweet[] tweets;
        static void Main(string[] args)
        {
            if (!EnsureSingleLoad())
            {
                Console.WriteLine("{0}: Another Instance Currently Runing", DateTime.Now);
                return;
            }

            Console.WriteLine("{0}: Started", DateTime.Now);
            var screenname = UsersCollection.PrimaryUser().TwitterScreenName;

            var secret = ConfigurationManager.AppSettings["TwitterCustomerSecret"];

            HubConnection hubConnection = null;
            IHubProxy streamingHub = null;

            while (streamingHubConnectAttempts++ < 3)
            {
                if (streamingHubConnectAttempts > 1) System.Threading.Thread.Sleep(5000);

                Console.WriteLine("{0}: Attempting To Connect To PushURL '{1}' (Attempt: {2})", DateTime.Now, ConfigurationManager.AppSettings["PushURL"], streamingHubConnectAttempts);
                hubConnection = (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["PushURL"])) ? new HubConnection(ConfigurationManager.AppSettings["PushURL"]) : null;
                
                if (hubConnection != null)
                {
                    try
                    {
                        streamingHub = hubConnection.CreateProxy("streamingHub");
                        hubConnection.StateChanged += new Action<SignalR.Client.StateChange>(sc =>
                        {
                            if (sc.NewState == SignalR.Client.ConnectionState.Connected)
                            {
                                Console.WriteLine("{0}: Push Connection Established", DateTime.Now);
                                lock (queue_push_lock)
                                {
                                    if (queue_push.Count > 0)
                                    {
                                        Console.WriteLine("{0}: Pushing {1} Tweets to Web Application", DateTime.Now, queue_push.Count());
                                        streamingHub.Invoke("Send", new StreamItem() { Secret = secret, Data = queue_push }).Wait();
                                        queue_push.Clear();
                                    }
                                }
                            }
                            else if (sc.NewState == SignalR.Client.ConnectionState.Disconnected)
                                Console.WriteLine("{0}: Push Connection Lost", DateTime.Now);
                            else if (sc.NewState == SignalR.Client.ConnectionState.Reconnecting)
                                Console.WriteLine("{0}: Reestablishing Push Connection", DateTime.Now);
                            else if (sc.NewState == SignalR.Client.ConnectionState.Connecting)
                                Console.WriteLine("{0}: Establishing Push Connection", DateTime.Now);

                        });
                        var startHubTask = hubConnection.Start();
                        startHubTask.Wait();
                        if (!startHubTask.IsFaulted) break;
                    }
                    catch (Exception ex)
                    {
                        hubConnection = null;
                        Console.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
                    }
                }
            }

            Console.WriteLine("{0}: Getting Friends for {1}", DateTime.Now, screenname);
            Friends.Update();
            Console.WriteLine("{0}: Finished Getting Friends for {1}", DateTime.Now, screenname);

            Console.WriteLine("{0}: Listening to Stream", DateTime.Now);

            var context = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname);

            context.Log = Console.Out;

            var stream = StartTwitterStream(context);

            var queueTimer = new Timer(60000);
            queueTimer.Elapsed += new ElapsedEventHandler((x, y) =>
                {
                    queueTimer.Enabled = false;
                    try
                    {
                        lock (queue_lock)
                        {
                            if (queue.Count == 0) return;
                            tweets = new Tweet[queue.Count];
                            queue.CopyTo(tweets);
                            queue.Clear();
                        }

                        Console.WriteLine("{0}: Processing {1} Items from Queue", DateTime.Now, tweets.Length);

                        var tp = new TweetProcessor(tweets, true);
                        tp.Start();

                        tweets
                            .GroupBy(t => t.User.Identifier.ScreenName)
                            .ToList()
                            .ForEach(g =>
                        {
                            Repository<Tweet>.Instance.Save(g.Key + TWEETS, g.OrderBy(t => t.CreatedAt).Select(t => t).ToList());
                            Console.WriteLine("{0}: {1} Tweets Saved for {2}", DateTime.Now, g.Count(), g.Key);
                        });

                        Repository<Tweet>.Instance.FlushChanges();

                        if (hubConnection != null && streamingHub != null)
                        {

                            int retweetThreshold = UsersCollection.PrimaryUser().RetweetThreshold;
                            tweets = tweets.Where(t => t.RetweetCount >= retweetThreshold).ToArray();
                            if (hubConnection.State == SignalR.Client.ConnectionState.Connected)
                            {
                                if (tweets.Length > 0)
                                {
                                    Console.WriteLine("{0}: Pushing {1} Tweets to Web Application", DateTime.Now, tweets.Count());
                                    streamingHub.Invoke("Send", new StreamItem() { Secret = secret, Data = tweets }).Wait();
                                }
                            }
                            else
                            {
                                lock (queue_push_lock)
                                {
                                    queue_push.AddRange(tweets);
                                }
                            }
                        }

                        tweets = null;

                        Console.WriteLine("{0}: Completed Processing Queue", DateTime.Now);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
                    }
                    finally
                    {
                        queueTimer.Enabled = true;
                    }
                });
            queueTimer.Start();

            /*
             * It appears like firing off the friends update while running the stream will 
             * cause the stream to stop working.
             * 
            var friendTimer = new Timer(3600000);
            friendTimer.Elapsed += new ElapsedEventHandler((x, y) =>
                {
                    friendTimer.Enabled = false;
                    try 
                    {
                        Console.WriteLine("{0}: Getting Friends for {1}", DateTime.Now, screenname);
                        Friends.Update();
                        Console.WriteLine("{0}: Finished Getting Friends for {1}", DateTime.Now, screenname);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
                    }
                    finally
                    {
                        friendTimer.Enabled = true;
                    }
                });
            friendTimer.Start();
            */

            while(Console.ReadLine() != "exit");
            Console.WriteLine("{0}: Exiting", DateTime.Now);
        }

        private static UserStream StartTwitterStream(TwitterContext context)
        {
            return context
                .UserStream
                .Where(s => s.Type == LinqToTwitter.UserStreamType.User)
                .Select(strm => strm)
                .StreamingCallback(strm =>
                {
                    try
                    {
                        if (strm != null && !string.IsNullOrEmpty(strm.Content))
                        {
                            var status = new Status(LitJson.JsonMapper.ToObject(strm.Content));
                            if (status != null && !string.IsNullOrEmpty(status.StatusID))
                            {
                                var tweet = new Tweet(string.IsNullOrEmpty(status.RetweetedStatus.StatusID) ? status : status.RetweetedStatus);
                                lock (queue_lock)
                                {
                                    queue.Add(tweet);
                                }
                                Console.WriteLine("{0}: Added Item to Queue: {1}", DateTime.Now, tweet.TweetText);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
                    }
                }).SingleOrDefault();
        }

        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.Streaming." + UsersCollection.PrimaryUser().TwitterScreenName, out result);

            return result;
        }
    }
}
