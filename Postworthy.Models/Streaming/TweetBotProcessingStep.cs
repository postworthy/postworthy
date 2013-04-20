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

namespace Postworthy.Models.Streaming
{
    public class TweetBotProcessingStep : IProcessingStep
    {
        private const string RUNTIME_REPO_KEY = "TweetBotRuntimeSettings";
        private const int POTENTIAL_TWEET_BUFFER_MAX = 10;
        private const int POTENTIAL_TWEEP_BUFFER_MAX = 50;
        private List<string> NoTweetList = new List<string>();
        private string[] Messages = null;
        private bool OnlyWithMentions = false;
        private TextWriter log = null;
        private Tweep PrimaryTweep = new Tweep(UsersCollection.PrimaryUser(), Tweep.TweepType.None);
        private TweetBotRuntimeSettings RuntimeSettings = null;
        private Repository<TweetBotRuntimeSettings> repo = Repository<TweetBotRuntimeSettings>.Instance;

        public void Init(TextWriter log)
        {
            this.log = log;

            RuntimeSettings = (repo.Query(RUNTIME_REPO_KEY)
                ?? new List<TweetBotRuntimeSettings> { new TweetBotRuntimeSettings() }).FirstOrDefault();

            NoTweetList.Add(UsersCollection.PrimaryUser().TwitterScreenName.ToLower());
            Messages = TweetBotSettings.Settings.Messages.Count == 0 ?
                null :
                Enumerable.Range(0, TweetBotSettings.Settings.Messages.Count - 1)
                    .Select(i => TweetBotSettings.Settings.Messages[i].Value).ToArray();
            OnlyWithMentions = TweetBotSettings.Settings.Filters["OnlyWithMentions"] != null ?
                TweetBotSettings.Settings.Filters["OnlyWithMentions"].Value :
                false;
            if (Messages == null)
                log.WriteLine("{0}: 'TweetBotSettings' configuration section is missing Messages. No responses will be sent.",
                    DateTime.Now);
            else
            {
                log.WriteLine("{0}: TweetBot will respond with: {1}",
                    DateTime.Now,
                    Environment.NewLine + string.Join(Environment.NewLine, Messages));
            }
        }

        public Task<IEnumerable<Tweet>> ProcessItems(IEnumerable<Tweet> tweets)
        {
            return Task<IEnumerable<Tweet>>.Factory.StartNew(new Func<IEnumerable<Tweet>>(() =>
                {
                    IEnumerable<Tweet> respondedTo = null;
                    if (Messages != null)
                        respondedTo = RespondToTweets(tweets);

                    FindPotentialTweets(tweets);

                    FindTweepsToFollow(tweets);

                    UpdateAverageWeight(tweets);

                    SendTweets();

                    SendFriendRequests();

                    DebugConsoleLog();

                    try
                    {
                        repo.Save(RUNTIME_REPO_KEY, RuntimeSettings);
                    }
                    catch (Enyim.Caching.Memcached.MemcachedException mcex) {
                        RuntimeSettings.Tweeted = RuntimeSettings.Tweeted.Skip(RuntimeSettings.Tweeted.Count() / 2).ToList();
                        repo.Save(RUNTIME_REPO_KEY, RuntimeSettings);
                    }

                    return tweets;
                }));
        }

        private void SendTweets()
        {
            if (RuntimeSettings.TweetOrRetweet)
            {
                RuntimeSettings.TweetOrRetweet = !RuntimeSettings.TweetOrRetweet;
                if (RuntimeSettings.PotentialTweets.Count == POTENTIAL_TWEET_BUFFER_MAX)
                {
                    var tweet = RuntimeSettings.PotentialTweets.First();
                    RuntimeSettings.Tweeted = RuntimeSettings.Tweeted.Union(new List<Tweet> { tweet }, Tweet.GetTweetTextComparer()).ToList();
                    RuntimeSettings.TweetsSentSinceLastFriendRequest++;
                    RuntimeSettings.PotentialTweets.Remove(tweet);

                    RuntimeSettings.PotentialTweets.RemoveAll(x=> x.RetweetCount < (tweet.RetweetCount * 0.8));
                }
            }
            else
            {
                RuntimeSettings.TweetOrRetweet = !RuntimeSettings.TweetOrRetweet;
                if (RuntimeSettings.PotentialReTweets.Count > 2)
                {
                    var tweet = RuntimeSettings.PotentialReTweets.First();
                    RuntimeSettings.Tweeted = RuntimeSettings.Tweeted.Union(new List<Tweet> { tweet }, Tweet.GetTweetTextComparer()).ToList();
                    RuntimeSettings.TweetsSentSinceLastFriendRequest++;
                    RuntimeSettings.PotentialReTweets.Remove(tweet);

                    RuntimeSettings.PotentialReTweets.RemoveAll(x => x.RetweetCount < (tweet.RetweetCount * 0.3));
                }
            }
        }

        private void SendFriendRequests()
        {
            if (RuntimeSettings.TweetsSentSinceLastFriendRequest == 20)
            {
                RuntimeSettings.TweetsSentSinceLastFriendRequest = 0;

                log.WriteLine("{0}: Friend Request Sent", DateTime.Now);
            }
        }

        private void DebugConsoleLog()
        {
            log.WriteLine("****************************");
            log.WriteLine("****************************");
            if (RuntimeSettings.PotentialFollows.Count() > 0)
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
            if (RuntimeSettings.Tweeted.Count() > 0)
            {
                log.WriteLine("####################");
                log.WriteLine("{0}: Past Tweets: {1}",
                    DateTime.Now,
                    Environment.NewLine + "\t" + string.Join(Environment.NewLine + "\t", RuntimeSettings.Tweeted.Select(x => (x.RetweetCount + 1) + ":" + x.TweetText)));
                log.WriteLine("####################");
            }
            if (RuntimeSettings.PotentialFollows.Count() > 0)
            {
                log.WriteLine("####################");
                log.WriteLine("{0}: Potential Follows: {1}",
                    DateTime.Now,
                    Environment.NewLine + "\t" + string.Join(Environment.NewLine + "\t", RuntimeSettings.PotentialFollows.Select(x => x.User.Identifier.ScreenName)));
                log.WriteLine("####################");
            }

            log.WriteLine("####################");
            log.WriteLine("{0}: Average Weight: {1:F5}",
                DateTime.Now,
                RuntimeSettings.AverageWeight);
            log.WriteLine("####################");

            log.WriteLine("****************************");
            log.WriteLine("****************************");
        }

        private void UpdateAverageWeight(IEnumerable<Tweet> tweets)
        {
            var minClout = GetMinClout();
            var minWeight = GetMinWeight();
            var friendsAndFollows = TwitterModel.Instance.Friends(UsersCollection.PrimaryUser().TwitterScreenName);

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
            var friendsAndFollows = TwitterModel.Instance.Friends(UsersCollection.PrimaryUser().TwitterScreenName);

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
                    .Where(x=>x.tweet.TweetText.Select(y=>y).Where(y=>y == '?').Count() > 3) //Why would you need so many question marks????
                    .Where(x => x.weight >= minWeight);

            if (tweet_tweep_pairs.Count() > 0)
            {
                RuntimeSettings.PotentialReTweets = tweet_tweep_pairs
                    .Where(x => friendsAndFollows.Contains(x.tweep))
                    .Select(x => x.tweet)
                    .Union(RuntimeSettings.PotentialReTweets, Tweet.GetTweetTextComparer())
                    .OrderByDescending(x => x.RetweetCount)
                    .Take(POTENTIAL_TWEET_BUFFER_MAX)
                    .ToList();

                RuntimeSettings.PotentialTweets = tweet_tweep_pairs
                    .Where(x => !friendsAndFollows.Contains(x.tweep) && 
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
            var friendsAndFollows = TwitterModel.Instance.Friends(UsersCollection.PrimaryUser().TwitterScreenName);
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
                .Where(x => !friendsAndFollows.Contains(x.tweep))
                .Where(x => x.tweep.User.LangResponse == PrimaryTweep.User.LangResponse)
                .Where(x => x.tweep.Clout() > minClout)
                .Where(x => x.weight >= minWeight);

            RuntimeSettings.PotentialFollows = tweet_tweep_pairs
                    .Select(x => x.tweep)
                    .Union(RuntimeSettings.PotentialFollows)
                    .OrderByDescending(x => x.Clout())
                    .Take(POTENTIAL_TWEEP_BUFFER_MAX)
                    .ToList();
        }

        private int GetMinClout()
        {
            var friends = TwitterModel.Instance.Friends(UsersCollection.PrimaryUser().TwitterScreenName)
                .Where(x => x.Type == Tweep.TweepType.Follower || x.Type == Tweep.TweepType.Mutual);
            double minClout = friends.Count() + 1.0;
            return (int)Math.Max(minClout, friends.Count() > 0 ? Math.Floor(friends.Average(x => x.Clout())) : 0);
        }

        private double GetMinWeight()
        {
            return RuntimeSettings.AverageWeight;
        }

        private IEnumerable<Tweet> RespondToTweets(IEnumerable<Tweet> tweets)
        {
            var repliedTo = new List<Tweet>();
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
            return repliedTo;
        }
    }

    #region TweetBotSettings
    public class TweetBotSettings : ConfigurationSection
    {
        private static TweetBotSettings settings = ConfigurationManager.GetSection("TweetBotSettings") as TweetBotSettings;

        public static TweetBotSettings Settings { get { return settings; } }

        [ConfigurationProperty("Messages", IsKey = false, IsRequired = true)]
        public MessageCollection Messages { get { return (MessageCollection)base["Messages"]; } }

        [ConfigurationProperty("Filters", IsKey = false, IsRequired = false)]
        public FilterCollection Filters { get { return (FilterCollection)base["Filters"]; } }
    }

    public class FilterCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new Filter();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((Filter)element).Key;
        }

        public Filter this[int idx]
        {
            get
            {
                return (Filter)BaseGet(idx);
            }
        }

        public Filter this[string key]
        {
            get
            {
                return (Filter)BaseGet(key);
            }
        }
    }

    public class MessageCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new Message();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((Message)element).Key;
        }

        public Message this[int idx]
        {
            get
            {
                return (Message)BaseGet(idx);
            }
        }
    }

    public class Filter : ConfigurationElement
    {
        [ConfigurationProperty("key", IsKey = false, IsRequired = true)]
        public string Key { get { return (string)base["key"]; } set { base["key"] = value; } }
        [ConfigurationProperty("value", IsKey = false, IsRequired = true)]
        public bool Value { get { return (bool)base["value"]; } set { base["value"] = value; } }
    }

    public class Message : ConfigurationElement
    {
        [ConfigurationProperty("key", IsKey = true, IsRequired = true)]
        public string Key { get { return (string)base["key"]; } set { base["key"] = value; } }
        [ConfigurationProperty("value", IsKey = false, IsRequired = true)]
        public string Value { get { return (string)base["value"]; } set { base["value"] = value; } }
    }

    public class TweetBotRuntimeSettings : RepositoryEntity
    {
        public Guid SettingsGuid { get; set; }

        public double AverageWeight { get; set; }
        public DateTime LastTweetTime { get; set; }
        public bool TweetOrRetweet { get; set; }
        public int TweetsSentSinceLastFriendRequest { get; set; }
        public List<Tweet> PotentialTweets { get; set; }
        public List<Tweet> PotentialReTweets { get; set; }
        public List<Tweet> Tweeted { get; set; }
        public List<Tweep> PotentialFollows { get; set; }

        public TweetBotRuntimeSettings()
        {
            SettingsGuid = Guid.NewGuid();
            PotentialTweets = new List<Tweet>();
            PotentialReTweets = new List<Tweet>();
            Tweeted = new List<Tweet>();
            PotentialFollows = new List<Tweep>();
        }

        public override string UniqueKey
        {
            get { return SettingsGuid.ToString(); }
        }

        public override bool IsEqual(RepositoryEntity other)
        {
            return other.UniqueKey == this.UniqueKey;
        }
    }

    #endregion
}

