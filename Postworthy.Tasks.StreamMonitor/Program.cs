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
using SignalR.Client.Hubs;
using System.Net;

namespace Postworthy.Tasks.StreamMonitor
{
    class Program
    {
        private static object queue_lock = new object();
        private static object queue_push_lock = new object();
        private static List<Tweet> queue = new List<Tweet>();
        private static List<Tweet> queue_push = new List<Tweet>();
        private static int streamingHubConnectAttempts = 0;
        private static Tweet[] tweets;
        private static StreamContent stream = null;
        private static DateTime lastCallBackTime = DateTime.Now;
        private static bool hadStreamFailure = false;

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

            Console.WriteLine("{0}: Listening to Stream", DateTime.Now);

            var context = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname);
            stream = StartTwitterStream(context);

            var queueTimer = new Timer(60000);
            queueTimer.Elapsed += new ElapsedEventHandler((x, y) =>
            {
                queueTimer.Enabled = false;
                try
                {
                    Console.WriteLine("{0}: Processing Queue", DateTime.Now);

                    lock (queue_lock)
                    {
                        if (queue.Count == 0)
                        {
                            Console.WriteLine("{0}: No Items to Process", DateTime.Now);
                            return;
                        }
                        tweets = new Tweet[queue.Count];
                        queue.CopyTo(tweets);
                        queue.Clear();
                    }

                    if (hubConnection != null && streamingHub != null && tweets.Length > 0)
                    {
                        if (hubConnection.State == SignalR.Client.ConnectionState.Connected)
                        {
                            Console.WriteLine("{0}: Pushing {1} Tweets to Web Application for Processing", DateTime.Now, tweets.Count());
                            streamingHub.Invoke("Process", new StreamItem() { Secret = secret, Data = tweets }).Wait();
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
                }
                catch (Exception ex)
                {
                    Console.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
                }
                finally
                {
                    Console.WriteLine("{0}: Completed Processing Queue", DateTime.Now);
                    if (Math.Abs((lastCallBackTime - DateTime.Now).TotalSeconds) > 90) //The Stream Stalled or was Closed
                    {
                        if (hadStreamFailure)
                            Console.WriteLine("{0}: LinqToTwitter UserStream Was Closed Attempting to Reconnect", DateTime.Now);
                        else
                            Console.WriteLine("{0}: LinqToTwitter UserStream Stalled Attempting to Restart It", DateTime.Now);

                        context = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname);
                        stream = StartTwitterStream(context);
                    }
                    queueTimer.Enabled = true;
                }
            });
            queueTimer.Start();
        }

        private static StreamContent StartTwitterStream(TwitterContext context)
        {
            StreamContent sc = null;
            hadStreamFailure = false;

            context.Log = Console.Out;

            string track = ConfigurationManager.AppSettings["Track"] ?? "";

            try
            {
                if (string.IsNullOrEmpty(track)) 
                    throw new ArgumentNullException("AppSetting 'Track' Cannot be null!");

                context.Streaming
                    .Where(s => s.Type == LinqToTwitter.StreamingType.Filter && s.Track == track)
                    .Select(strm => strm)
                    .StreamingCallback(strm =>
                    {
                        try
                        {
                            lastCallBackTime = DateTime.Now;
                            sc = strm;
                            if (strm != null)
                            {
                                if (strm.Status == TwitterErrorStatus.RequestProcessingException)
                                {
                                    var wex = strm.Error as WebException;
                                    if (wex != null && wex.Status == WebExceptionStatus.ConnectFailure)
                                    {
                                        Console.WriteLine("{0}: LinqToTwitter UserStream Connection Failure", DateTime.Now);
                                        hadStreamFailure = true;
                                        //Will Be Restarted By Processing Queue
                                    }
                                }
                                else if (!string.IsNullOrEmpty(strm.Content))
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
                                    else
                                        Console.WriteLine("{0}: Unhandled Item in Stream: {1}", DateTime.Now, strm.Content);
                                }
                                else
                                    Console.WriteLine("{0}: Twitter Keep Alive", DateTime.Now);
                            }
                            else
                                throw new ArgumentNullException("strm", "This value should never be null!");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
                        }
                    }).SingleOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
            }

            while (sc == null)
            {
                Console.WriteLine("{0}: Waiting On Twitter Connection", DateTime.Now);
                System.Threading.Thread.Sleep(1000);
            }

            return sc;
        }
        
        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.StreamMonitor." + UsersCollection.PrimaryUser().TwitterScreenName, out result);

            return result;
        }
    }
}
