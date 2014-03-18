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
using System.Net;
using System.Threading.Tasks;
using System.Runtime.ConstrainedExecution;
using System.IO;
using Postworthy.Models.Core;

namespace Postworthy.Tasks.StreamMonitor
{
    public class DualStreamMonitor
    {
        private const int MAX_TRACK = 400;

        private TextWriter log;

        private object queue_lock = new object();
        private List<Tweet> queue = new List<Tweet>();
        private Tweet[] tweets;
        private StreamContent userStream = null;
        private StreamContent trackerStream = null;
        private Task userStreamTask = null;
        private Task trackerStreamTask = null;
        private DateTime lastCallBackTimeUserStream = DateTime.Now;
        private DateTime lastCallBackTimeTrackerStream = DateTime.Now;
        private bool hadUserStreamFailure = false;
        private bool hadTrackerStreamFailure = false;
        private IProcessingStep processingStep = null;
        private PostworthyUser PrimaryUser = null;

        private Timer queueTimer = null;

        public DualStreamMonitor(PostworthyUser primaryUser, TextWriter log)
        {
            this.log = log;
            PrimaryUser = primaryUser;
        }
        public void Start()
        {
            log.WriteLine("{0}: Started", DateTime.Now);

            log.WriteLine("{0}: Initializing IProcessingStep", DateTime.Now);
            GetIProcessingStep().Init(PrimaryUser.TwitterScreenName,log);

            var screenname = PrimaryUser.TwitterScreenName;

            //log.WriteLine("{0}: Getting Friends for {1}", DateTime.Now, screenname);
            //TwitterModel.Instance.UpdateFriendsForPrimaryUser();
            //log.WriteLine("{0}: Finished Getting Friends for {1}", DateTime.Now, screenname);

            log.WriteLine("{0}: Listening to Stream", DateTime.Now);


            var userStreamContext = TwitterModel.Instance(PrimaryUser.TwitterScreenName).GetAuthorizedTwitterContext(screenname);
            var trackerStreamContext = TwitterModel.Instance(PrimaryUser.TwitterScreenName).GetAuthorizedTwitterContext(screenname);
            
            StartTwitterStream(userStreamContext, trackerStreamContext);

            StartProcessingQueue(userStreamContext, trackerStreamContext);
        }
        public void Stop()
        {
            if (trackerStream != null) trackerStream.CloseStream();
            if (userStream != null) userStream.CloseStream();
            if (processingStep != null)
                processingStep.Shutdown();

            log.WriteLine("{0}: Exiting", DateTime.Now);
        }
        private void StartProcessingQueue(TwitterContext userStreamContext, TwitterContext trackerStreamContext)
        {
            var screenname = PrimaryUser.TwitterScreenName;
            var queueTime = int.Parse(ConfigurationManager.AppSettings["QueueTime"] ?? "60000");

            queueTimer = new Timer(queueTime);

            queueTimer.Elapsed += new ElapsedEventHandler((x, y) =>
            {
                queueTimer.Enabled = false;
                bool restartTracker = false;
                try
                {
                    log.WriteLine("{0}: Processing Queue", DateTime.Now);

                    lock (queue_lock)
                    {
                        if (queue.Count == 0)
                        {
                            log.WriteLine("{0}: No Items to Process", DateTime.Now);
                            return;
                        }
                        tweets = new Tweet[queue.Count];
                        queue.CopyTo(tweets);
                        queue.Clear();
                    }

                    tweets = tweets.Distinct().ToArray();

                    log.WriteLine("{0}: Processing {1} Items from Queue", DateTime.Now, tweets.Length);

                    //Currently there is only one step but there could potentially be multiple user defined steps
                    GetIProcessingStep().ProcessItems(tweets).Wait();

                    if (processingStep is IKeywordSuggestionStep)
                    {
                        try { restartTracker = (processingStep as IKeywordSuggestionStep).HasNewKeywordSuggestions(); }
                        catch { }
                    }

                    tweets = null;
                }
                catch (Exception ex)
                {
                    log.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
                }
                finally
                {
                    try
                    {
                        if (userStream != null && Math.Abs((lastCallBackTimeUserStream - DateTime.Now).TotalSeconds) > 90) //The User Stream Stalled or was Closed
                        {
                            if (hadUserStreamFailure)
                                log.WriteLine("{0}: LinqToTwitter User Stream Was Closed Attempting to Reconnect", DateTime.Now);
                            else
                                log.WriteLine("{0}: LinqToTwitter User Stream Stalled Attempting to Restart It", DateTime.Now);

                            userStreamContext = TwitterModel.Instance(PrimaryUser.TwitterScreenName).GetAuthorizedTwitterContext(screenname);
                            StartTwitterUserStream(userStreamContext);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
                    }
                    try
                    {
                        if (restartTracker ||  //The tracker should be restarted because we have new potential keywords to track
                            (trackerStream != null && Math.Abs((lastCallBackTimeTrackerStream - DateTime.Now).TotalSeconds) > 90)) //The Tracker Stream Stalled or was Closed
                        {
                            if (hadTrackerStreamFailure)
                                log.WriteLine("{0}: LinqToTwitter Tracker Stream was Closed Attempting to Reconnect", DateTime.Now);
                            else if (restartTracker)
                                log.WriteLine("{0}: LinqToTwitter Tracker Stream will be Restarted to Track more Keywords", DateTime.Now);
                            else
                                log.WriteLine("{0}: LinqToTwitter Tracker Stream Stalled Attempting to Restart It", DateTime.Now);

                            if (trackerStream != null)
                                trackerStream.CloseStream();

                            trackerStreamContext = TwitterModel.Instance(PrimaryUser.TwitterScreenName).GetAuthorizedTwitterContext(screenname);
                            StartTwitterTrackerStream(trackerStreamContext);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
                    }

                    queueTimer.Enabled = true;
                    log.WriteLine("{0}: Completed Processing Queue", DateTime.Now);
                }
            });

            log.WriteLine("{0}: Processing Queue every {1} milliseconds", DateTime.Now, queueTime);

            queueTimer.Start();
        }
        private IProcessingStep GetIProcessingStep()
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
        private void StartTwitterStream(TwitterContext userStreamContext, TwitterContext trackerStreamContext)
        {
            Task.Factory.StartNew(() =>
            {
                StartTwitterUserStream(userStreamContext);
            });
            Task.Factory.StartNew(() =>
            {
                StartTwitterTrackerStream(trackerStreamContext);
            });
        }

        private void StartTwitterTrackerStream(TwitterContext context)
        {
            hadTrackerStreamFailure = false;
            List<string> trackList = null;

            context.Log = log;

            string track = ConfigurationManager.AppSettings["Track"] ?? (PrimaryUser.Track ?? "");
            string[] ignore = (ConfigurationManager.AppSettings["Ignore"] ?? "").ToLower().Split(',');

            int minFollowers = int.Parse(ConfigurationManager.AppSettings["MinFollowerCount"] ?? "0");

            try
            {
                if (string.IsNullOrEmpty(track) && !(processingStep is IKeywordSuggestionStep))
                {
                    log.WriteLine("{0}: To track keywords one of the following must be true: \n\t1) AppSetting Property 'Track' Cannot be Null or Empty.\n\t2) UserCollection Property 'Track' Cannot be Null or Empty.\n\t3) ProcessingStep must Implement IKeywordSuggestionStep.", DateTime.Now);
                    return;
                }
                else
                {
                    var processIgnoreWords = !string.IsNullOrEmpty(track) ? track.ToLower().Split(',').ToList() : new List<string>();
                    trackList = !string.IsNullOrEmpty(track) ? track.ToLower().Split(',').ToList() : new List<string>();

                    if (processingStep is IKeywordSuggestionStep)
                    {
                        var keywordSuggestionStep = processingStep as IKeywordSuggestionStep;
                        if (keywordSuggestionStep != null)
                        {
                            /* I have chosen to wrap these calls in seperate try catch statements incase one fails
                             * the other can still run. This way if the get fails we may still have hope of a reset.
                             */
                            try { keywordSuggestionStep.SetIgnoreKeywords(processIgnoreWords); }
                            catch { }
                            try { trackList.AddRange(keywordSuggestionStep.GetKeywordSuggestions().Select(x => x.ToLower()).ToList()); }
                            catch { }
                            try { keywordSuggestionStep.ResetHasNewKeywordSuggestions(); }
                            catch { }
                        }
                    }

                    if (trackList.Count == 0)
                    {
                        log.WriteLine("{0}: No Keywords to Track at this time.", DateTime.Now);
                        return;
                    }
                    else
                    {
                        if (trackList.Count > MAX_TRACK)
                        {
                            trackList = trackList.OrderByDescending(x => x.Length).Take(400).ToList();
                            log.WriteLine("{0}: Tracking List Exceeds Max {1} Reducing List", DateTime.Now, MAX_TRACK);
                        }
                        log.WriteLine("{0}: Attempting to Track: {1}", DateTime.Now, string.Join(",", trackList));
                        log.WriteLine("{0}: Ignoring : {1}", DateTime.Now, string.Join(",", ignore));
                    }

                    trackerStreamTask = context.Streaming
                        .Where(s => s.Type == LinqToTwitter.StreamingType.Filter && s.Track == string.Join(",", trackList.Distinct()))
                        .Select(strm => strm)
                        .StartAsync(async strm =>
                        {
                            await Task.Run(() =>
                                {
                                    try
                                    {
                                        lastCallBackTimeTrackerStream = DateTime.Now;
                                        if (trackerStream == null)
                                            log.WriteLine("{0}: Twitter Connection Established (TrackerStream)", DateTime.Now);
                                        trackerStream = strm;
                                        if (strm != null)
                                        {
                                            /* LinqToTwitter v3.0 no longer has *.Status
                                            if (strm.Status == TwitterErrorStatus.RequestProcessingException)
                                            {
                                                var wex = strm.Error as WebException;
                                                if (wex != null && wex.Status == WebExceptionStatus.ConnectFailure)
                                                {
                                                    log.WriteLine("{0}: LinqToTwitter Stream Connection Failure (TrackerStream)", DateTime.Now);
                                                    hadTrackerStreamFailure = true;
                                                    //Will Be Restarted By Processing Queue
                                                }
                                            }
                                            else 
                                            */
                                            if (!string.IsNullOrEmpty(strm.Content))
                                            {
                                                var status = new LinqToTwitter.Status(LitJson.JsonMapper.ToObject(strm.Content));
                                                if (status != null && status.StatusID > 0)
                                                {
                                                    string statusText = status.Text.ToLower();
                                                    if (
                                                        trackList.Any(x => statusText.Contains(x)) && //Looking for exact matches
                                                        status.User.FollowersCount >= minFollowers && //Meets the follower cutoff
                                                        !ignore.Any(x => x != "" && statusText.Contains(x)) //Ignore these
                                                        )
                                                    {
                                                        var tweet = new Tweet(status.RetweetedStatus.StatusID == 0 ? status : status.RetweetedStatus);
                                                        lock (queue_lock)
                                                        {
                                                            queue.Add(tweet);
                                                        }
                                                        log.WriteLine("{0}: Added Item to Queue (TrackerStream): @{1} said [{2}]", DateTime.Now, tweet.User.ScreenName, tweet.TweetText);
                                                    }
                                                }
                                                else
                                                    log.WriteLine("{0}: Unhandled Item in Stream (TrackerStream): {1}", DateTime.Now, strm.Content);
                                            }
                                            else
                                                log.WriteLine("{0}: Twitter Keep Alive (TrackerStream)", DateTime.Now);
                                        }
                                        else
                                            throw new ArgumentNullException("strm", "This value should never be null!");
                                    }
                                    catch (Exception ex)
                                    {
                                        log.WriteLine("{0}: Error (TrackerStream): {1}", DateTime.Now, ex.ToString());
                                    }
                                });
                        });
                }
            }
            catch (Exception ex)
            {
                log.WriteLine("{0}: Error (TrackerStream): {1}", DateTime.Now, ex.ToString());
            }
        }
        private void StartTwitterUserStream(TwitterContext context)
        {
            hadUserStreamFailure = false;
            context.Log = log;

            try
            {
                userStreamTask = context.Streaming
                    .Where(s => s.Type == LinqToTwitter.StreamingType.User)
                    .Select(strm => strm)
                    .StartAsync(async strm =>
                    {
                        await Task.Run(() =>
                        {
                            try
                            {
                                lastCallBackTimeUserStream = DateTime.Now;
                                if (userStream == null)
                                    log.WriteLine("{0}: Twitter Connection Established (UserStream)", DateTime.Now);
                                userStream = strm;
                                if (strm != null)
                                {
                                    /* LinqToTwitter v3.0 no longer has *.Status
                                    if (strm.Status == TwitterErrorStatus.RequestProcessingException)
                                    {
                                        var wex = strm.Error as WebException;
                                        if (wex != null && wex.Status == WebExceptionStatus.ConnectFailure)
                                        {
                                            log.WriteLine("{0}: LinqToTwitter UserStream Connection Failure (UserStream)", DateTime.Now);
                                            hadUserStreamFailure = true;
                                            //Will Be Restarted By Processing Queue
                                        }
                                    }
                                    else 
                                    */
                                    if (!string.IsNullOrEmpty(strm.Content))
                                    {
                                        var status = new LinqToTwitter.Status(LitJson.JsonMapper.ToObject(strm.Content));
                                        if (status != null && status.StatusID > 0)
                                        {
                                            var tweet = new Tweet(status.RetweetedStatus.StatusID == 0 ? status : status.RetweetedStatus);
                                            lock (queue_lock)
                                            {
                                                queue.Add(tweet);
                                            }
                                            log.WriteLine("{0}: Added Item to Queue (UserStream): {1}", DateTime.Now, tweet.TweetText);
                                        }
                                        else
                                        {
                                            //If you can handle friends we will look for them
                                            if (processingStep is ITweepProcessingStep)
                                            {
                                                var jsonDataFriends = LitJson.JsonMapper.ToObject(strm.Content)
                                                    .FirstOrDefault(x => x.Key == "friends");

                                                //If this is a friends collection update we will notify you
                                                if (!jsonDataFriends.Equals(default(KeyValuePair<string, LitJson.JsonData>)) &&
                                                    jsonDataFriends.Value != null &&
                                                    jsonDataFriends.Value.IsArray)
                                                {
                                                    var friends = new List<LazyLoader<Tweep>>();
                                                    for (int i = 0; i < jsonDataFriends.Value.Count; i++)
                                                    {
                                                        friends.Add(TwitterModel.Instance(PrimaryUser.TwitterScreenName).GetLazyLoadedTweep(ulong.Parse(jsonDataFriends.Value[i].ToString()), Tweep.TweepType.Follower));
                                                    }

                                                    (processingStep as ITweepProcessingStep).ProcessTweeps(friends);
                                                }
                                                else
                                                    log.WriteLine("{0}: Unhandled Item in Stream (UserStream): {1}", DateTime.Now, strm.Content);
                                            }
                                            else
                                                log.WriteLine("{0}: Unhandled Item in Stream (UserStream): {1}", DateTime.Now, strm.Content);
                                        }
                                    }
                                    else
                                        log.WriteLine("{0}: Twitter Keep Alive (UserStream)", DateTime.Now);
                                }
                                else
                                    throw new ArgumentNullException("strm", "This value should never be null!");
                            }
                            catch (Exception ex)
                            {
                                log.WriteLine("{0}: Error (UserStream): {1}", DateTime.Now, ex.ToString());
                            }
                        });
                    });
            }
            catch (Exception ex)
            {
                log.WriteLine("{0}: Error (UserStream): {1}", DateTime.Now, ex.ToString());
            }
        }
    }
}
