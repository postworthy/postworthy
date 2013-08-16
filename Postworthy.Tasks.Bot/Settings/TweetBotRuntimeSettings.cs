using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Postworthy.Models.Repository;
using Postworthy.Models.Twitter;
using Postworthy.Tasks.Bot.Communication;

namespace Postworthy.Tasks.Bot.Settings
{
    public class TweetBotRuntimeSettings : RepositoryEntity
    {
        private const string PAST_TWEETS = "PAST_TWEETS_";
        private const string POTENTIAL_TWEETS = "POTENTIAL_TWEETS_";
        private const string POTENTIAL_RETWEETS = "POTENTIAL_RETWEETS_";
        private SimpleRepository<Tweet> tweetRepo = new SimpleRepository<Tweet>();

        private List<Tweet> pastTweets = null;
        private List<Tweet> potentialTweets = null;
        private List<Tweet> potentialReTweets = null;

        public const int SIMULATION_MODE_HOURS = 48;

        public Guid SettingsGuid { get; set; }

        public long TotalTweetsProcessed { get; set; }
        public DateTime BotFirstStart { get; set; }
        public double AverageWeight { get; set; }
        public DateTime LastTweetTime { get; set; }
        public bool TweetOrRetweet { get; set; }
        public int TweetsSentSinceLastFriendRequest { get; set; }
        public List<CountableItem> Keywords { get; set; }
        public List<CountableItem> KeywordSuggestions { get; set; }
        public List<string> KeywordsToIgnore { get; set; }
        public List<string> KeywordsManuallyAdded { get; set; }
        public List<string> KeywordsManuallyIgnored { get; set; }
        public List<CountableItem<Tweep>> PotentialFriendRequests { get; set; }
        public List<Tweep> TwitterFollowSuggestions { get; set; }

        public double MinimumRetweetLevel
        {
            get
            {
                var pastTweets = this.GetPastTweets();
                if (
                    ///TODO: FIND A BETTER WAY TO DETERMINE MINIMUM RETWEET LEVEL
                    false && //SHORTED OUT FOR NOW...
                    pastTweets != null && pastTweets.Length > 5)
                {
                    double less = Math.Max((60.0 - ((DateTime.Now - LastTweetTime).TotalMinutes)) / 100.0, 0.1); //Allows us to progressivly lower the bar of what we accept over time
                    double stdev = 0;
                    var values = pastTweets.Select(x => x.RetweetCount);
                    double avg = values.Average();
                    //Get Standard Deviation
                    stdev = Math.Sqrt(values.Sum(d => (d - avg) * (d - avg)) / values.Count());

                    return values.Where(x => x <= (avg + stdev * 2) && x >= (avg - stdev * 2)).Average() * less;
                }

                return 2.0;
            }
        }

        public bool IsSimulationMode
        {
            get
            {
                return BotFirstStart.AddHours(SIMULATION_MODE_HOURS) > DateTime.Now;
            }
        }

        public TweetBotRuntimeSettings()
        {
            BotFirstStart = DateTime.Now;
            SettingsGuid = Guid.NewGuid();
            PotentialFriendRequests = new List<CountableItem<Tweep>>();
            LastTweetTime = DateTime.MaxValue;
            Keywords = new List<CountableItem>();
            KeywordSuggestions = new List<CountableItem>();
            KeywordsToIgnore = new List<string>();
            KeywordsManuallyAdded = new List<string>();
            KeywordsManuallyIgnored = new List<string>();
            TwitterFollowSuggestions = new List<Tweep>();
        }

        public Tweet[] GetPotentialTweets(bool retweets = false)
        {
            if (!retweets)
            {
                if (potentialTweets == null)
                    potentialTweets = tweetRepo.Query(POTENTIAL_TWEETS + SettingsGuid, 0, 0).ToList();
                return potentialTweets.ToArray();
            }
            else
            {
                if(potentialReTweets == null)
                    potentialReTweets = tweetRepo.Query(POTENTIAL_RETWEETS + SettingsGuid, 0, 0).ToList();
                return potentialReTweets.ToArray();
            }
        }

        public void AddPotentialTweets(List<Tweet> tweets, bool retweets = false)
        {
            GetPotentialTweets(retweets); //Loads them up if they are not already loaded up

            if (!retweets)
            {
                GetPotentialTweets(retweets);
                potentialTweets.AddRange(tweets);
                tweetRepo.Save(POTENTIAL_TWEETS + SettingsGuid, tweets);
            }
            else
            {
                potentialReTweets.AddRange(tweets);
                tweetRepo.Save(POTENTIAL_RETWEETS + SettingsGuid, tweets);
            }
        }

        public void RemovePotentialTweet(Tweet tweet, bool retweet = false)
        {
            GetPotentialTweets(retweet); //Loads them up if they are not already loaded up

            if (!retweet)
            {
                var remove = potentialTweets.Where(x => x.UniqueKey == tweet.UniqueKey).FirstOrDefault();
                if (remove != null)
                    potentialTweets.Remove(remove);

                tweetRepo.Delete(POTENTIAL_TWEETS + SettingsGuid, tweet);
            }
            else
            {
                var remove = potentialReTweets.Where(x => x.UniqueKey == tweet.UniqueKey).FirstOrDefault();
                if (remove != null)
                    potentialReTweets.Remove(remove);

                tweetRepo.Delete(POTENTIAL_RETWEETS + SettingsGuid, tweet);
            }
        }


        public Tweet[] GetPastTweets(int size = 50)
        {
            if(pastTweets == null || pastTweets.Count < size)
                pastTweets = tweetRepo.Query(PAST_TWEETS + SettingsGuid, 0, size).ToList();

            return pastTweets.ToArray();
        }

        public void AddPastTweet(Tweet tweet)
        {
            pastTweets.Insert(0, tweet);
            tweetRepo.Save(PAST_TWEETS + SettingsGuid, tweet);
        }

        public void RemovePastTweet(Tweet tweet)
        {
            var remove = pastTweets.Where(x => x.UniqueKey == tweet.UniqueKey).FirstOrDefault();
            if (remove != null)
                pastTweets.Remove(remove);

            tweetRepo.Delete(PAST_TWEETS + SettingsGuid, tweet);
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
}
