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
using System.Threading.Tasks;

namespace Postworthy.Tasks.StreamMonitor
{
    class Program
    {
        private static object queue_lock = new object();
        private static List<Tweet> queue = new List<Tweet>();
        private static Tweet[] tweets;
        private static StreamContent userStream = null;
        private static StreamContent trackerStream = null;
        private static DateTime lastCallBackTimeUserStream = DateTime.Now;
        private static DateTime lastCallBackTimeTrackerStream = DateTime.Now;
        private static bool hadUserStreamFailure = false;
        private static bool hadTrackerStreamFailure = false;
        private static IProcessingStep processingStep = null;

        private static Timer queueTimer = null;

        static void Main(string[] args)
        {
            if (!EnsureSingleLoad())
            {
                Console.WriteLine("{0}: Another Instance Currently Runing", DateTime.Now);
                return;
            }

            Console.WriteLine("{0}: Started", DateTime.Now);

            Console.WriteLine("{0}: Initializing IProcessingStep", DateTime.Now);
            GetIProcessingStep().Init(Console.Out);

            var screenname = UsersCollection.PrimaryUser().TwitterScreenName;

            Console.WriteLine("{0}: Getting Friends for {1}", DateTime.Now, screenname);
            Friends.UpdateForPrimaryUser();
            Console.WriteLine("{0}: Finished Getting Friends for {1}", DateTime.Now, screenname);

            Console.WriteLine("{0}: Listening to Stream", DateTime.Now);

            
            var userStreamContext = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname);
            var trackerStreamContext = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname);
            var streams = StartTwitterStream(userStreamContext, trackerStreamContext);

            userStream = streams.FirstOrDefault();
            trackerStream = streams.LastOrDefault();

            StartProcessingQueue(userStreamContext, trackerStreamContext);

            while (Console.ReadLine() != "exit") ;
            Console.WriteLine("{0}: Exiting", DateTime.Now);
            streams.ForEach(s =>
            {
                if (s != null) s.CloseStream();
            });
        }

        private static void StartProcessingQueue(TwitterContext userStreamContext, TwitterContext trackerStreamContext)
        {
            var screenname = UsersCollection.PrimaryUser().TwitterScreenName;
            var queueTime = int.Parse(ConfigurationManager.AppSettings["QueueTime"] ?? "60000");
            
            queueTimer = new Timer(queueTime);

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

                    tweets = tweets.Distinct().ToArray();

                    Console.WriteLine("{0}: Processing {1} Items from Queue", DateTime.Now, tweets.Length);

                    //Currently there is only one step but there could potentially be multiple user defined steps
                    GetIProcessingStep().ProcessItems(tweets).Wait();

                    tweets = null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
                }
                finally
                {
                    try
                    {
                        if (userStream != null && Math.Abs((lastCallBackTimeUserStream - DateTime.Now).TotalSeconds) > 90) //The User Stream Stalled or was Closed
                        {
                            if (hadUserStreamFailure)
                                Console.WriteLine("{0}: LinqToTwitter User Stream Was Closed Attempting to Reconnect", DateTime.Now);
                            else
                                Console.WriteLine("{0}: LinqToTwitter User Stream Stalled Attempting to Restart It", DateTime.Now);

                            userStreamContext = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname);
                            var task = StartTwitterUserStream(userStreamContext);
                            task.Wait();
                            userStream = task.Result;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
                    }
                    try
                    {
                        if (trackerStream != null && Math.Abs((lastCallBackTimeTrackerStream - DateTime.Now).TotalSeconds) > 90) //The Tracker Stream Stalled or was Closed
                        {
                            if (hadTrackerStreamFailure)
                                Console.WriteLine("{0}: LinqToTwitter Tracker Stream Was Closed Attempting to Reconnect", DateTime.Now);
                            else
                                Console.WriteLine("{0}: LinqToTwitter Tracker Stream Stalled Attempting to Restart It", DateTime.Now);

                            trackerStreamContext = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname);
                            var task = StartTwitterTrackerStream(trackerStreamContext);
                            task.Wait();
                            trackerStream = task.Result;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
                    }

                    queueTimer.Enabled = true;
                    Console.WriteLine("{0}: Completed Processing Queue", DateTime.Now);
                }
            });

            Console.WriteLine("{0}: Processing Queue every {1} milliseconds", DateTime.Now, queueTime);

            queueTimer.Start();
        }

        private static IProcessingStep GetIProcessingStep()
        {
            if (processingStep == null)
            {
                string processingType = ConfigurationManager.AppSettings["IProcessingStep"];
                if (!string.IsNullOrEmpty(processingType) && processingType.Split(';').Length == 2)
                {
                    string assemblyName = processingType.Split(';')[0];
                    string typeName = processingType.Split(';')[1];

                    if (assemblyName.ToLower().EndsWith(".dll")) 
                        assemblyName = string.Join(".", assemblyName.Split('.').Reverse().Skip(1).Reverse());

                    var obj = Activator.CreateInstance(assemblyName, typeName).Unwrap();
                    if (obj is IProcessingStep)
                        processingStep = obj as IProcessingStep;
                }
            }

            if (processingStep != null)
                return processingStep;
            else
                throw new Exception("Could Not Create IProcessingStep from AppSettings");
        }

        private static List<StreamContent> StartTwitterStream(TwitterContext userStreamContext, TwitterContext trackerStreamContext)
        {
            var ut = StartTwitterUserStream(userStreamContext);

            var tt = StartTwitterTrackerStream(trackerStreamContext);

            Task<StreamContent>.WaitAll(new Task<StreamContent>[] { ut, tt });

            if (ut.IsCompleted && !ut.IsFaulted && tt.IsCompleted && !tt.IsFaulted)
                return new List<StreamContent> { ut.Result, tt.Result };
            else
                throw new Exception("Stream Error!");
        }

        private static Task<StreamContent> StartTwitterTrackerStream(TwitterContext context)
        {
            return Task<StreamContent>.Factory.StartNew(() =>
            {
                bool firstWait = true;
                StreamContent sc = null;
                hadTrackerStreamFailure = false;
                List<string> trackList = null;

                context.Log = Console.Out;

                string track = ConfigurationManager.AppSettings["Track"] ?? (UsersCollection.PrimaryUser().Track ?? "");
                string[] ignore = (ConfigurationManager.AppSettings["Ignore"] ?? "").ToLower().Split(',');

                int minFollowers = int.Parse(ConfigurationManager.AppSettings["MinFollowerCount"] ?? "0");

                try
                {
                    if (string.IsNullOrEmpty(track))
                    {
                        Console.WriteLine("{0}: AppSetting or UserCollection Property 'Track' Cannot be Null or Empty if you want to Track Key Words!", DateTime.Now, track);
                        return null;
                    }
                    else
                        Console.WriteLine("{0}: Attempting to Track: {1}", DateTime.Now, track);

                    trackList = track.ToLower().Split(',').ToList();

                    context.StreamingUserName = ConfigurationManager.AppSettings["UserName"];
                    context.StreamingPassword = ConfigurationManager.AppSettings["Password"];

                    context.Streaming
                        .Where(s => s.Type == LinqToTwitter.StreamingType.Filter && s.Track == track)
                        .Select(strm => strm)
                        .StreamingCallback(strm =>
                        {
                            try
                            {
                                lastCallBackTimeTrackerStream = DateTime.Now;
                                sc = strm;
                                if (strm != null)
                                {
                                    if (strm.Status == TwitterErrorStatus.RequestProcessingException)
                                    {
                                        var wex = strm.Error as WebException;
                                        if (wex != null && wex.Status == WebExceptionStatus.ConnectFailure)
                                        {
                                            Console.WriteLine("{0}: LinqToTwitter Stream Connection Failure (TrackerStream)", DateTime.Now);
                                            hadTrackerStreamFailure = true;
                                            //Will Be Restarted By Processing Queue
                                        }
                                    }
                                    else if (!string.IsNullOrEmpty(strm.Content))
                                    {
                                        var status = new Status(LitJson.JsonMapper.ToObject(strm.Content));
                                        if (status != null && !string.IsNullOrEmpty(status.StatusID))
                                        {
                                            string statusText = status.Text.ToLower();
                                            if (
                                                trackList.Any(x => statusText.Contains(x)) && //Looking for exact matches
                                                status.User.FollowersCount >= minFollowers && //Meets the follower cutoff
                                                !ignore.Any(x => x != "" && statusText.Contains(x)) //Ignore these
                                                )
                                            {
                                                var tweet = new Tweet(string.IsNullOrEmpty(status.RetweetedStatus.StatusID) ? status : status.RetweetedStatus);
                                                lock (queue_lock)
                                                {
                                                    queue.Add(tweet);
                                                }
                                                Console.WriteLine("{0}: Added Item to Queue (TrackerStream): @{1} said [{2}]", DateTime.Now, tweet.User.Identifier.ScreenName, tweet.TweetText);
                                            }
                                        }
                                        else
                                            Console.WriteLine("{0}: Unhandled Item in Stream (TrackerStream): {1}", DateTime.Now, strm.Content);
                                    }
                                    else
                                        Console.WriteLine("{0}: Twitter Keep Alive (TrackerStream)", DateTime.Now);
                                }
                                else
                                    throw new ArgumentNullException("strm", "This value should never be null!");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("{0}: Error (TrackerStream): {1}", DateTime.Now, ex.ToString());
                            }
                        }).SingleOrDefault();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("{0}: Error (TrackerStream): {1}", DateTime.Now, ex.ToString());
                }

                while (sc == null)
                {
                    if (firstWait)
                    {
                        Console.WriteLine("{0}: Waiting On Twitter Connection (TrackerStream)", DateTime.Now);
                        firstWait = false;
                    }
                    System.Threading.Thread.Sleep(1000);
                }

                Console.WriteLine("{0}: Twitter Connection Established (TrackerStream)", DateTime.Now);

                return sc;
            });
        }

        private static Task<StreamContent> StartTwitterUserStream(TwitterContext context)
        {
            return Task<StreamContent>.Factory.StartNew(() =>
            {
                bool firstWait = true;
                StreamContent sc = null;
                hadUserStreamFailure = false;

                context.Log = Console.Out;

                try
                {
                    context.UserStream
                        .Where(s => s.Type == LinqToTwitter.UserStreamType.User)
                        .Select(strm => strm)
                        .StreamingCallback(strm =>
                        {
                            try
                            {
                                lastCallBackTimeUserStream = DateTime.Now;
                                sc = strm;
                                if (strm != null)
                                {
                                    if (strm.Status == TwitterErrorStatus.RequestProcessingException)
                                    {
                                        var wex = strm.Error as WebException;
                                        if (wex != null && wex.Status == WebExceptionStatus.ConnectFailure)
                                        {
                                            Console.WriteLine("{0}: LinqToTwitter UserStream Connection Failure (UserStream)", DateTime.Now);
                                            hadUserStreamFailure = true;
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
                                            Console.WriteLine("{0}: Added Item to Queue (UserStream): {1}", DateTime.Now, tweet.TweetText);
                                        }
                                        else
                                            Console.WriteLine("{0}: Unhandled Item in Stream (UserStream): {1}", DateTime.Now, strm.Content);
                                    }
                                    else
                                        Console.WriteLine("{0}: Twitter Keep Alive (UserStream)", DateTime.Now);
                                }
                                else
                                    throw new ArgumentNullException("strm", "This value should never be null!");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("{0}: Error (UserStream): {1}", DateTime.Now, ex.ToString());
                            }
                        }).SingleOrDefault();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("{0}: Error (UserStream): {1}", DateTime.Now, ex.ToString());
                }

                while (sc == null)
                {
                    if (firstWait)
                    {
                        Console.WriteLine("{0}: Waiting On Twitter Connection (UserStream)", DateTime.Now);
                        firstWait = false;
                    }
                    System.Threading.Thread.Sleep(1000);
                }

                Console.WriteLine("{0}: Twitter Connection Established (UserStream)", DateTime.Now);

                return sc;
            });
        }
        
        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.StreamMonitor." + UsersCollection.PrimaryUser().TwitterScreenName, out result);

            return result;
        }
    }
}
