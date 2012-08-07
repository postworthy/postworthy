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

namespace Postworthy.Tasks.Streaming
{
    class Program
    {
        private const string TWEETS = "_tweets";
        private static object queue_lock = new object();
        private static List<Tweet> queue = new List<Tweet>();
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

            SignalR.Client.Connection pushConnection = (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["PushURL"])) ? new SignalR.Client.Connection(ConfigurationManager.AppSettings["PushURL"]) : null;

            if (pushConnection != null)
            {
                try
                {
                    Console.WriteLine("{0}: Establishing Push Connection", DateTime.Now);
                    pushConnection.Start();
                    Console.WriteLine("{0}: Push Connection Established", DateTime.Now);
                }
                catch
                {
                    pushConnection = null;
                    Console.WriteLine("{0}: Push Connection Currently Unavailable", DateTime.Now);
                }
            }

            Console.WriteLine("{0}: Getting Friends for {1}", DateTime.Now, screenname);
            Friends.Update();
            Console.WriteLine("{0}: Finished Getting Friends for {1}", DateTime.Now, screenname);

            Console.WriteLine("{0}: Listening to Stream", DateTime.Now);

            var stream = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname)
                .UserStream
                .Where(s=>s.Type == LinqToTwitter.UserStreamType.User)
                .Select(strm=>strm)
                .StreamingCallback(strm=>
                {
                    try
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
                    catch { }
                }).SingleOrDefault();

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

                        if (pushConnection != null && pushConnection.State == SignalR.Client.ConnectionState.Connected)
                        {
                            int retweetThreshold = UsersCollection.PrimaryUser().RetweetThreshold;
                            tweets = tweets.Where(t => t.RetweetCount >= retweetThreshold).ToArray();
                            if (tweets.Length > 0)
                            {
                                Console.WriteLine("{0}: Pushing {1} Tweets to Web Application", DateTime.Now, tweets.Count());
                                pushConnection.Send(new StreamItem() { Secret = secret, Data = tweets });
                            }
                        }

                        tweets = null;

                        Console.WriteLine("{0}: Completed Processing Queue", DateTime.Now);
                    }
                    catch { }
                    finally
                    {
                        queueTimer.Enabled = true;
                    }
                });
            queueTimer.Start();


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
                    catch { }
                    finally
                    {
                        friendTimer.Enabled = true;
                    }
                });
            friendTimer.Start();

            while(Console.ReadLine() != "exit");
            Console.WriteLine("{0}: Exiting", DateTime.Now);
        }

        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.Streaming." + UsersCollection.PrimaryUser().TwitterScreenName, out result);

            return result;
        }
    }
}
