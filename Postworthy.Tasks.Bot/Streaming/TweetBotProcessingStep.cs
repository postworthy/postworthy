using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using Postworthy.Models.Twitter;
using Postworthy.Models.Account;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using Postworthy.Models.Repository;
using Postworthy.Models.Core;
using System.Runtime.ConstrainedExecution;
using Postworthy.Models.Streaming;
using Postworthy.Tasks.Bot.Settings;
using System.Text.RegularExpressions;
using Postworthy.Tasks.Bot.Models;

namespace Postworthy.Tasks.Bot.Streaming
{
    public class TweetBotProcessingStep : IProcessingStep, IKeywordSuggestionStep
    {
        private const string RUNTIME_REPO_KEY = "TweetBotRuntimeSettings";
        private const int POTENTIAL_TWEET_BUFFER_MAX = 10;
        private const int POTENTIAL_TWEEP_BUFFER_MAX = 50;
        private const int MIN_TWEEP_NOTICED = 5;
        private const int TWEEP_NOTICED_AUTOMATIC = 25;
        private const int MAX_TIME_BETWEEN_TWEETS = 3;
        private const int SIMULATION_MODE_HOURS = 48;
        private const int MINIMUM_KEYWORD_COUNT = 30;
        private const int MINIMUM_NEW_KEYWORD_LENGTH = 3;
        private int saveCount = 0;
        private List<string> NoTweetList = new List<string>();
        private string[] Messages = null;
        private bool OnlyWithMentions = false;
        private TextWriter log = null;
        private Tweep PrimaryTweep = new Tweep(UsersCollection.PrimaryUser(), Tweep.TweepType.None);
        private TweetBotRuntimeSettings RuntimeSettings = null;
        private Repository<TweetBotRuntimeSettings> repo = Repository<TweetBotRuntimeSettings>.Instance;
        private bool ForceSimulationMode = false;
        private bool hasNewKeywordSuggestions = false;
        private List<string> StopWords = new List<string>();

        public bool SimulationMode
        {
            get
            {
                return ForceSimulationMode || (RuntimeSettings != null && RuntimeSettings.BotFirstStart.AddHours(SIMULATION_MODE_HOURS) > DateTime.Now);
            }
        }

        public void Init(TextWriter log)
        {
            this.log = log;

            RuntimeSettings = (repo.Query(RUNTIME_REPO_KEY)
                ?? new List<TweetBotRuntimeSettings> { new TweetBotRuntimeSettings() }).FirstOrDefault()
                ?? new TweetBotRuntimeSettings();

            NoTweetList.Add(UsersCollection.PrimaryUser().TwitterScreenName.ToLower());
            Messages = TweetBotSettings.Get.Messages.Count == 0 ?
                null :
                Enumerable.Range(0, TweetBotSettings.Get.Messages.Count - 1)
                    .Select(i => TweetBotSettings.Get.Messages[i].Value).ToArray();
            OnlyWithMentions = TweetBotSettings.Get.Filters["OnlyWithMentions"] != null ?
                TweetBotSettings.Get.Filters["OnlyWithMentions"].Value :
                false;

            ForceSimulationMode = TweetBotSettings.Get.Settings["IsSimulationMode"] != null ?
                TweetBotSettings.Get.Settings["IsSimulationMode"].Value :
                false;

            if (ForceSimulationMode)
                log.WriteLine("{0}: Running in forced simulation mode. No actions will be taken.",
                    DateTime.Now);
            else if (SimulationMode)
                log.WriteLine("{0}: Running in automatic simulation mode to aquire a baseline. No actions will be taken for {1}hrs.",
                    DateTime.Now,
                    SIMULATION_MODE_HOURS);

            if (Messages == null)
                log.WriteLine("{0}: 'TweetBotSettings' configuration section is missing Messages. No responses will be sent.",
                    DateTime.Now);
            else
            {
                log.WriteLine("{0}: TweetBot will respond with: {1}",
                    DateTime.Now,
                    Environment.NewLine + string.Join(Environment.NewLine, Messages));
            }

            try
            {
                StopWords = File.OpenText("Resources/stopwords.txt")
                    .ReadToEnd()
                    .Split('\n').Select(x=>x.Replace("\r","").ToLower())
                    .ToList();
                log.WriteLine("{0}: Stop Words: {1}",
                    DateTime.Now,
                    string.Join(",", StopWords));
            }
            catch { }
        }

        public Task<IEnumerable<Tweet>> ProcessItems(IEnumerable<Tweet> tweets)
        {
            return Task<IEnumerable<Tweet>>.Factory.StartNew(new Func<IEnumerable<Tweet>>(() =>
                {
                    RespondToTweets(tweets);

                    FindPotentialTweets(tweets);

                    FindTweepsToFollow(tweets);

                    FindKeywords(tweets);

                    UpdateAverageWeight(tweets);

                    SendTweets();

                    EstablishTargets();

                    DebugConsoleLog();

                    if (saveCount++ > 20)
                    {
                        SaveRuntimeSettings();
                        saveCount = 0;
                    }

                    return tweets;
                }));
        }

        public void Shutdown()
        {
            SaveRuntimeSettings();
        }

        private void SaveRuntimeSettings()
        {
            bool saved = false;
            while (!saved)
            {
                try
                {
                    repo.Save(RUNTIME_REPO_KEY, RuntimeSettings);
                    saved = true;
                }
                catch (Enyim.Caching.Memcached.MemcachedException mcex)
                {
                    RuntimeSettings.Tweeted = RuntimeSettings.Tweeted
                        .OrderByDescending(x => x.TweetRank)
                        .Take(RuntimeSettings.Tweeted.Count() / 2)
                        .ToList();
                    saved = false;
                }
            }
        }

        private void SendTweets()
        {
            if (RuntimeSettings.TweetOrRetweet)
            {
                RuntimeSettings.TweetOrRetweet = !RuntimeSettings.TweetOrRetweet;
                if (RuntimeSettings.PotentialTweets.Count >= POTENTIAL_TWEET_BUFFER_MAX ||
                    //Because we default the LastTweetTime to the max value this will only be used after the tweet buffer initially loads up
                    (DateTime.Now >= RuntimeSettings.LastTweetTime.AddHours(MAX_TIME_BETWEEN_TWEETS) && RuntimeSettings.PotentialTweets.Count > 0))
                {
                    var tweet = RuntimeSettings.PotentialTweets.First();
                    var groups = RuntimeSettings.Tweeted
                        .Union(new List<Tweet> { tweet }, Tweet.GetTweetTextComparer())
                        .GroupSimilar(0.45m, log)
                        .Select(g => new TweetGroup(g))
                        .Where(g => g.GroupStatusIDs.Count() > 1);
                    var matches = groups.Where(x => x.GroupStatusIDs.Contains(tweet.StatusID));
                    if (matches.Count() > 0)
                    {
                        //Ignore Tweets that are very similar
                        RuntimeSettings.PotentialTweets.Remove(tweet);
                    }
                    else
                    {
                        if (SendTweet(tweet, false))
                        {
                            RuntimeSettings.Tweeted = RuntimeSettings.Tweeted.Union(new List<Tweet> { tweet }, Tweet.GetTweetTextComparer()).ToList();
                            RuntimeSettings.TweetsSentSinceLastFriendRequest++;
                            RuntimeSettings.LastTweetTime = DateTime.Now;
                            RuntimeSettings.PotentialTweets.Remove(tweet);
                            RuntimeSettings.PotentialTweets.RemoveAll(x => x.RetweetCount < GetMinRetweets());
                        }
                        else
                            RuntimeSettings.PotentialReTweets.Remove(tweet);
                    }
                }
            }
            else
            {
                RuntimeSettings.TweetOrRetweet = !RuntimeSettings.TweetOrRetweet;
                if (RuntimeSettings.PotentialReTweets.Count >= POTENTIAL_TWEET_BUFFER_MAX ||
                    //Because we default the LastTweetTime to the max value this will only be used after the tweet buffer initially loads up
                    (DateTime.Now >= RuntimeSettings.LastTweetTime.AddHours(MAX_TIME_BETWEEN_TWEETS) && RuntimeSettings.PotentialReTweets.Count > 0))
                {
                    var tweet = RuntimeSettings.PotentialReTweets.First();
                    var groups = RuntimeSettings.Tweeted.Union(new List<Tweet> { tweet }, Tweet.GetTweetTextComparer())
                       .GroupSimilar(0.45m, log)
                       .Select(g => new TweetGroup(g))
                       .Where(g => g.GroupStatusIDs.Count() > 1);
                    var matches = groups.Where(x => x.GroupStatusIDs.Contains(tweet.StatusID));
                    if (matches.Count() > 0)
                    {
                        //Ignore Tweets that are very similar
                        RuntimeSettings.PotentialReTweets.Remove(tweet);
                    }
                    else
                    {
                        if (SendTweet(tweet, true))
                        {
                            RuntimeSettings.Tweeted = RuntimeSettings.Tweeted.Union(new List<Tweet> { tweet }, Tweet.GetTweetTextComparer()).ToList();
                            RuntimeSettings.TweetsSentSinceLastFriendRequest++;
                            RuntimeSettings.LastTweetTime = DateTime.Now;
                            RuntimeSettings.PotentialReTweets.Remove(tweet);
                            RuntimeSettings.PotentialReTweets.RemoveAll(x => x.RetweetCount < GetMinRetweets());
                        }
                        else
                            RuntimeSettings.PotentialReTweets.Remove(tweet);
                    }
                }
            }
        }

        private bool SendTweet(Tweet tweet, bool isRetweet)
        {
            if (!SimulationMode)
            {
                if (!isRetweet)
                {
                    tweet.PopulateExtendedData();
                    var link = tweet.Links.OrderByDescending(x => x.ShareCount).FirstOrDefault();
                    if (link != null)
                    {
                        string statusText = link.ToString() == link.Title ?
                            link.Title.Substring(0, 116) + " " + link.Uri.ToString()
                            :
                            link.Uri.ToString();
                        TwitterModel.Instance.UpdateStatus(statusText, processStatus: false);
                        return true;
                    }
                }
                else
                {
                    TwitterModel.Instance.Retweet(tweet.StatusID.ToString());
                    return true;
                }

                return false;
            }
            else
                return true;
        }

        private void EstablishTargets()
        {
            if (RuntimeSettings.TweetsSentSinceLastFriendRequest >= 2)
            {
                RuntimeSettings.TweetsSentSinceLastFriendRequest = 0;

                var tweeps = RuntimeSettings.PotentialTweeps
                    .Where(x => x.Item1 > MIN_TWEEP_NOTICED)
                    .Where(x => x.Item2.Type == Tweep.TweepType.None);

                tweeps.ToList().ForEach(x =>
                {
                    var followers = x.Item2.Followers().Select(y => y.ID);
                    var primaryFollowers = PrimaryTweep.Followers().Select(y => y.ID);

                    if (x.Item1 > TWEEP_NOTICED_AUTOMATIC ||
                        followers.Union(primaryFollowers).Count() != (followers.Count() + primaryFollowers.Count()))
                    {
                        x.Item2.Type = Tweep.TweepType.Target;

                        if (!SimulationMode)
                        {
                            var follow = TwitterModel.Instance.CreateFriendship(x.Item2);

                            if (follow.Type == Tweep.TweepType.Following)
                            {
                                PrimaryTweep.Followers(true);
                                RuntimeSettings.PotentialTweeps.Remove(x);
                            }
                        }
                    }
                    else
                        x.Item2.Type = Tweep.TweepType.Ignore;
                });
            }
        }

        private void DebugConsoleLog()
        {
            log.WriteLine("****************************");
            log.WriteLine("****************************");
            if (RuntimeSettings.Tweeted.Count() > 0)
            {
                log.WriteLine("####################");
                log.WriteLine("{0}: Past Tweets: {1}",
                    DateTime.Now,
                    Environment.NewLine + "\t" + string.Join(Environment.NewLine + "\t", RuntimeSettings.Tweeted.Select(x => (x.RetweetCount + 1) + ":" + x.TweetText)));
                log.WriteLine("####################");
            }
            if (RuntimeSettings.PotentialTweets.Count() > 0)
            {
                log.WriteLine("####################");
                log.WriteLine("{0}: Potential Tweets: {1}",
                    DateTime.Now,
                    Environment.NewLine + "\t" + string.Join(Environment.NewLine + "\t", RuntimeSettings.PotentialTweets.Select(x => (x.RetweetCount + 1) + ":" + x.TweetText)));
                log.WriteLine("####################");
            }
            if (RuntimeSettings.PotentialReTweets.Count() > 0)
            {
                log.WriteLine("####################");
                log.WriteLine("{0}: Potential Retweets: {1}",
                    DateTime.Now,
                    Environment.NewLine + "\t" + string.Join(Environment.NewLine + "\t", RuntimeSettings.PotentialReTweets.Select(x => (x.RetweetCount + 1) + ":" + x.TweetText)));
                log.WriteLine("####################");
            }
            if (RuntimeSettings.PotentialTweeps.Count() > 0)
            {
                log.WriteLine("####################");
                log.WriteLine("{0}: Potential Tweeps: {1}",
                    DateTime.Now,
                    Environment.NewLine + "\t" + string.Join(Environment.NewLine + "\t", RuntimeSettings.PotentialTweeps
                        .OrderByDescending(x => x.Item2.User.FollowersCount.ToString().Length)
                        .ThenByDescending(x => x.Item1)
                        .ThenBy(x => x.Item2.ScreenName)
                        .Select(x => x.Item1.ToString().PadLeft(3, '0') + "\t" + x.Item2)));
                log.WriteLine("####################");
            }
            if (RuntimeSettings.KeywordSuggestions.Count() > 0)
            {
                log.WriteLine("####################");
                log.WriteLine("{0}: Keyword Suggestions: {1}",
                    DateTime.Now,
                    Environment.NewLine + "\t" + string.Join(Environment.NewLine + "\t", RuntimeSettings.KeywordSuggestions
                        .OrderByDescending(x => x.Count)
                        .ThenByDescending(x => x.Key)
                        .Select(x => x.Count.ToString().PadLeft(3, '0') + "\t" + x.Key)));
                log.WriteLine("####################");
            }

            log.WriteLine("####################");
            log.WriteLine("{0}: Minimum Weight: {1:F5}",
                DateTime.Now,
                GetMinWeight());

            log.WriteLine("{0}: Minimum Retweets: {1:F2}",
                DateTime.Now,
                GetMinRetweets());

            log.WriteLine("{0}: Running {1}",
                DateTime.Now,
                SimulationMode ? "**In Simulation Mode**" : "**In the Wild**");
            log.WriteLine("####################");

            log.WriteLine("****************************");
            log.WriteLine("****************************");
        }

        private void UpdateAverageWeight(IEnumerable<Tweet> tweets)
        {
            var minClout = GetMinClout();
            var minWeight = GetMinWeight();
            var friendsAndFollows = PrimaryTweep.Followers();

            var tweet_tweep_pairs = tweets
                .Select(x =>
                    x.Status.Retweeted ?
                    new
                    {
                        tweet = new Tweet(x.Status.RetweetedStatus),
                        tweep = new Tweep(x.Status.RetweetedStatus.User, Tweep.TweepType.None),
                        weight = x.RetweetCount / (1.0 + new Tweep(x.Status.RetweetedStatus.User, Tweep.TweepType.None).Clout())
                    }
                    :
                    new
                    {
                        tweet = x,
                        tweep = x.Tweep(),
                        weight = x.RetweetCount / (1.0 + x.Tweep().Clout())
                    })
                .Where(x => x.tweep.Clout() > minClout)
                .Where(x => x.weight >= minWeight);

            if (tweet_tweep_pairs.Count() > 0)
            {
                RuntimeSettings.AverageWeight = RuntimeSettings.AverageWeight > 0.0 ?
                    (RuntimeSettings.AverageWeight + tweet_tweep_pairs.Average(x => x.weight)) / 2.0 : tweet_tweep_pairs.Average(x => x.weight);
            }
            else
            {
                RuntimeSettings.AverageWeight = RuntimeSettings.AverageWeight > 0.0 ?
                    (RuntimeSettings.AverageWeight) / 2.0 : 0.0;
            }
        }

        private void FindPotentialTweets(IEnumerable<Tweet> tweets)
        {
            var minWeight = GetMinWeight();
            var minRetweets = GetMinRetweets();
            var friendsAndFollows = PrimaryTweep.Followers().Select(x => x.ID);

            var tweet_tweep_pairs = tweets
                .Select(x =>
                    x.Status.Retweeted ?
                    new
                    {
                        tweet = new Tweet(x.Status.RetweetedStatus),
                        tweep = new Tweep(x.Status.RetweetedStatus.User, Tweep.TweepType.None),
                        weight = x.RetweetCount / (1.0 + new Tweep(x.Status.RetweetedStatus.User, Tweep.TweepType.None).Clout())
                    }
                    :
                    new
                    {
                        tweet = x,
                        tweep = x.Tweep(),
                        weight = x.RetweetCount / (1.0 + x.Tweep().Clout())
                    })
                    .Where(x => Encoding.UTF8.GetByteCount(x.tweet.TweetText) == x.tweet.TweetText.Length) //Only ASCII for me...
                    .Where(x => x.weight >= minWeight)
                    .Where(x => x.tweet.RetweetCount >= minRetweets);

            if (tweet_tweep_pairs.Count() > 0)
            {
                RuntimeSettings.PotentialReTweets = tweet_tweep_pairs
                    .Where(x => friendsAndFollows.Contains(x.tweep.UniqueKey))
                    .Select(x => x.tweet)
                    .Union(RuntimeSettings.PotentialReTweets, Tweet.GetTweetTextComparer())
                    .OrderByDescending(x => x.RetweetCount)
                    .Take(POTENTIAL_TWEET_BUFFER_MAX)
                    .ToList();

                RuntimeSettings.PotentialTweets = tweet_tweep_pairs
                    .Where(x => !friendsAndFollows.Contains(x.tweep.UniqueKey) &&
                        //x.tweet.Status.Entities.UserMentions.Count() == 0 &&
                        (x.tweet.Status.Entities.UrlMentions.Count() > 0 || x.tweet.Status.Entities.MediaMentions.Count() > 0))
                    .Select(x => x.tweet)
                    .Union(RuntimeSettings.PotentialTweets, Tweet.GetTweetTextComparer())
                    .OrderByDescending(x => x.RetweetCount)
                    .Take(POTENTIAL_TWEET_BUFFER_MAX)
                    .ToList();
            }
        }

        private void FindTweepsToFollow(IEnumerable<Tweet> tweets)
        {
            var minClout = GetMinClout();
            var minWeight = GetMinWeight();
            var friendsAndFollows = PrimaryTweep.Followers().Select(x => x.ID);
            var tweet_tweep_pairs = tweets
                .Select(x =>
                    x.Status.Retweeted ?
                    new
                    {
                        tweet = new Tweet(x.Status.RetweetedStatus),
                        tweep = new Tweep(x.Status.RetweetedStatus.User, Tweep.TweepType.None),
                        weight = x.RetweetCount / (1.0 + new Tweep(x.Status.RetweetedStatus.User, Tweep.TweepType.None).Clout())
                    }
                    :
                    new
                    {
                        tweet = x,
                        tweep = x.Tweep(),
                        weight = x.RetweetCount / (1.0 + x.Tweep().Clout())
                    })
                .Where(x => !friendsAndFollows.Contains(x.tweep.UniqueKey))
                .Where(x => x.tweep.User.LangResponse == PrimaryTweep.User.LangResponse)
                .Where(x => x.tweep.Clout() > minClout)
                .Where(x => x.weight >= minWeight);

            //Update Existing
            tweet_tweep_pairs
                    .Select(x => x.tweep)
                    .ToList()
                    .ForEach(x =>
                    {
                        for (int i = 0; i < RuntimeSettings.PotentialTweeps.Count(); i++)
                        {
                            if (RuntimeSettings.PotentialTweeps[i].Item2.Equals(x))
                            {
                                var tweep = RuntimeSettings.PotentialTweeps[i].Item2;

                                if (tweep.Type != Tweep.TweepType.Target)
                                    tweep.Type = Tweep.TweepType.None;

                                RuntimeSettings.PotentialTweeps[i] = new Tuple<int, Tweep>(
                                    RuntimeSettings.PotentialTweeps[i].Item1 + 1,
                                    tweep);
                            }
                        }
                    });

            //Add New
            tweet_tweep_pairs
                    .Select(x => x.tweep)
                    .Except(RuntimeSettings.PotentialTweeps.Select(x => x.Item2))
                    .ToList()
                    .ForEach(x =>
                    {
                        RuntimeSettings.PotentialTweeps.Add(new Tuple<int, Tweep>(1, x));
                    });

            //Limit
            RuntimeSettings.PotentialTweeps = RuntimeSettings.PotentialTweeps
                .OrderByDescending(x => x.Item2.Clout())
                .Take(POTENTIAL_TWEEP_BUFFER_MAX)
                .ToList();
        }

        private int GetMinClout()
        {
            /*
            var friends = PrimaryTweep.Followers().Select(x=>x.Value)
                .Where(x => x.Type == Tweep.TweepType.Follower || x.Type == Tweep.TweepType.Mutual);
            double minClout = friends.Count() + 1.0;
            return (int)Math.Max(minClout, friends.Count() > 0 ? Math.Floor(friends.Average(x => x.Clout())) : 0);
            */
            return PrimaryTweep.Followers().Count();
        }

        private double GetMinWeight()
        {
            if (RuntimeSettings.AverageWeight < 0.00001) RuntimeSettings.AverageWeight = 0.0;
            return RuntimeSettings.AverageWeight;
        }

        private double GetMinRetweets()
        {
            if (RuntimeSettings.Tweeted != null && RuntimeSettings.Tweeted.Count > 5)
            {
                double stdev = 0;
                var values = RuntimeSettings.Tweeted.Select(x => x.RetweetCount);
                double avg = values.Average();
                //Get Standard Deviation
                stdev = Math.Sqrt(values.Sum(d => (d - avg) * (d - avg)) / values.Count());

                return values.Where(x => x <= (avg + stdev*2) && x >= (avg - stdev*2)).Average() * 0.65;
            }
            
                return 2.0;

        }

        private IEnumerable<Tweet> RespondToTweets(IEnumerable<Tweet> tweets)
        {

            var repliedTo = new List<Tweet>();
            if (Messages != null)
            {
                foreach (var t in tweets)
                {
                    string tweetedBy = t.User.Identifier.ScreenName.ToLower();
                    if (!NoTweetList.Any(x => x == tweetedBy) && //Don't bug people with constant retweets
                        !t.TweetText.ToLower().Contains(NoTweetList[0]) && //Don't bug them even if they are mentioned in the tweet
                        (!OnlyWithMentions || t.Status.Entities.UserMentions.Count > 0) //OPTIONAL: Only respond to tweets that mention someone
                        )
                    {
                        //Dont want to keep hitting the same person over and over so add them to the ignore list
                        NoTweetList.Add(tweetedBy);
                        //If they were mentioned in a tweet they get ignored in the future just in case they reply
                        NoTweetList.AddRange(t.Status.Entities.UserMentions.Where(um => !string.IsNullOrEmpty(um.ScreenName)).Select(um => um.ScreenName));

                        string message = "";
                        if (t.User.FollowersCount > 9999)
                            //TODO: It would be very cool to have the code branch here for custom tweets to popular twitter accounts
                            //IDEA: Maybe have it text you for a response
                            message = Messages.OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                        else
                            //Randomly select response from list of possible responses
                            message = Messages.OrderBy(x => Guid.NewGuid()).FirstOrDefault();

                        //Tweet it
                        try
                        {
                            TwitterModel.Instance.UpdateStatus(message + " RT @" + t.User.Identifier.ScreenName + " " + t.TweetText, processStatus: false);
                        }
                        catch (Exception ex) { log.WriteLine("{0}: TweetBot Error: {1}", DateTime.Now, ex.ToString()); }

                        repliedTo.Add(t);

                        //Wait at least 1 minute between tweets so it doesnt look bot-ish with fast retweets
                        //Add some extra random timing somewhere between 0-2 minutes
                        //The shortest wait will be 1 minute the longest will be 3
                        int randomTime = 60000 + (1000 * Enumerable.Range(0, 120).OrderBy(x => Guid.NewGuid()).FirstOrDefault());
                        System.Threading.Thread.Sleep(randomTime);
                    }
                }
            }
            return repliedTo;
        }

        private void FindKeywords(IEnumerable<Tweet> tweets)
        {
            //For Later
            var oldSuggestionCount = RuntimeSettings.KeywordSuggestions.Where(x => x.Count >= MINIMUM_KEYWORD_COUNT).Count();

            var keywords = tweets
                .SelectMany(t => Regex.Replace(t.TweetText, @"(\p{P})|\t|\n|\r", "").ToLower().Split(' ')) //Strip Punctuation, Force Lowercase, Split Words, Make List
                .Except(StopWords) //Exclude Stop Words
                .Except(RuntimeSettings.KeywordsToIgnore) //Exclude Ignore Words
                .Where(x => !x.StartsWith("http")) //No URLs
                .Where(x => x.Length >= MINIMUM_NEW_KEYWORD_LENGTH) //Must be Minimum Length
                .Where(x => Encoding.UTF8.GetByteCount(x) == x.Length) //Only ASCII for me...
                .GroupBy(w => w) //Group Similar Words
                .Select(g => new { Word = g.Key, Count = g.Count() }) // Get Keyword Counts
                .ToList();

            //Update Master Keyword List
            keywords.ForEach(w=>{
                var item = RuntimeSettings.KeywordSuggestions.Where(x=>x.Key == w.Word).FirstOrDefault();
                if(item != null)
                    item.Count += w.Count;
                else
                    RuntimeSettings.KeywordSuggestions.Add(new CountableItem(w.Word, w.Count));
            });

            RuntimeSettings.KeywordSuggestions = RuntimeSettings.KeywordSuggestions
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.Key.Length)
                .Take(50)
                .ToList();

            //For Comparison
            var newSuggestionCount = RuntimeSettings.KeywordSuggestions.Where(x => x.Count >= MINIMUM_KEYWORD_COUNT).Count();

            //If we have more then set the flag
            if (newSuggestionCount > oldSuggestionCount)
                hasNewKeywordSuggestions = true;
        }

        public List<string> GetKeywordSuggestions()
        {
            return RuntimeSettings.KeywordSuggestions
                .Where(x=>x.Count >= MINIMUM_KEYWORD_COUNT)
                .Select(x=>x.Key)
                .ToList();
        }

        public void ResetHasNewKeywordSuggestions()
        {
            hasNewKeywordSuggestions = false;
        }

        public bool HasNewKeywordSuggestions()
        {
            return hasNewKeywordSuggestions;
        }

        public void SetIgnoreKeywords(List<string> keywords)
        {
            RuntimeSettings.KeywordsToIgnore = keywords;
        }
    }
}

