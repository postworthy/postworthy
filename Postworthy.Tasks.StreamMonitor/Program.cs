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
        private static List<Tweet> queue = new List<Tweet>();
        private static Tweet[] tweets;
        private static StreamContent stream = null;
        private static DateTime lastCallBackTime = DateTime.Now;
        private static bool hadStreamFailure = false;
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

            
            var context = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname);
            stream = StartTwitterStream(context);

            StartProcessingQueue(context);

            while (Console.ReadLine() != "exit") ;
            Console.WriteLine("{0}: Exiting", DateTime.Now);
            stream.CloseStream();
        }

        private static void StartProcessingQueue(TwitterContext context)
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
                        if (Math.Abs((lastCallBackTime - DateTime.Now).TotalSeconds) > 90) //The Stream Stalled or was Closed
                        {
                            if (hadStreamFailure)
                                Console.WriteLine("{0}: LinqToTwitter Stream Was Closed Attempting to Reconnect", DateTime.Now);
                            else
                                Console.WriteLine("{0}: LinqToTwitter Stream Stalled Attempting to Restart It", DateTime.Now);

                            context = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname);
                            stream = StartTwitterStream(context);
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

        private static StreamContent StartTwitterStream(TwitterContext context)
        {
            if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["StreamType"]) && ConfigurationManager.AppSettings["StreamType"].ToLower() == "userstream")
                return StartTwitterUserStream(context);
            else
                return StartTwitterTrackerStream(context);
        }

        private static StreamContent StartTwitterTrackerStream(TwitterContext context)
        {
            bool firstWait = true;
            StreamContent sc = null;
            hadStreamFailure = false;
            List<string> trackList = null;

            context.Log = Console.Out;

            string track = ConfigurationManager.AppSettings["Track"] ?? (UsersCollection.PrimaryUser().Track ?? "");
            string[] ignore = (ConfigurationManager.AppSettings["Ignore"] ?? "").ToLower().Split(',');

            int minFollowers = int.Parse(ConfigurationManager.AppSettings["MinFollowerCount"] ?? "0");

            try
            {
                if (string.IsNullOrEmpty(track)) 
                    throw new ArgumentNullException("AppSetting or UserCollection Property 'Track' Cannot be Null or Empty!");
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
                            lastCallBackTime = DateTime.Now;
                            sc = strm;
                            if (strm != null)
                            {
                                if (strm.Status == TwitterErrorStatus.RequestProcessingException)
                                {
                                    var wex = strm.Error as WebException;
                                    if (wex != null && wex.Status == WebExceptionStatus.ConnectFailure)
                                    {
                                        Console.WriteLine("{0}: LinqToTwitter Stream Connection Failure", DateTime.Now);
                                        hadStreamFailure = true;
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
                                            Console.WriteLine("{0}: Added Item to Queue: @{1} said [{2}]", DateTime.Now, tweet.User.Identifier.ScreenName, tweet.TweetText);
                                        }
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
                if (firstWait)
                {
                    Console.WriteLine("{0}: Waiting On Twitter Connection", DateTime.Now);
                    firstWait = false;
                }
                System.Threading.Thread.Sleep(1000);
            }

            return sc;
        }

        private static StreamContent StartTwitterUserStream(TwitterContext context)
        {
            bool firstWait = true;
            StreamContent sc = null;
            hadStreamFailure = false;

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
                if (firstWait)
                {
                    Console.WriteLine("{0}: Waiting On Twitter Connection", DateTime.Now);
                    firstWait = false;
                }
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
