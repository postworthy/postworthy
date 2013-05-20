using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Postworthy.Models.Account;
using Postworthy.Models.Repository;
using Postworthy.Models.Twitter;
using Postworthy.Tasks.Bot.Settings;

namespace Postworthy.Web.Bot.Models
{
    public class DashboardModel
    {
        private const string RUNTIME_REPO_KEY = "TweetBotRuntimeSettings";

        public PostworthyUser User { get; set; }
        public DateTime BotStartupTime { get; set; }
        public TimeSpan Runtime { get { return DateTime.Now - BotStartupTime; } }
        public DateTime LastTweetTime { get; set; }
        public int TweetsSentSinceLastFriendRequest { get; set; }
        public List<Tweet> PotentialTweets { get; set; }
        public List<Tweet> PotentialReTweets { get; set; }
        public List<Tweet> Tweeted { get; set; }
        public List<KeyValuePair<string, int>> KeywordSuggestions { get; set; }
        public List<KeyValuePair<Tweep, int>> PotentialFriendRequests { get; set; }
        public double TweetsPerHour { get; set; }
        public double[] TweetsPerHourLast24 { get; set; }
        public List<KeyValuePair<Tweep, int>> TopFriendTweetCounts { get; set; }
        public int MinimumRetweetLevel { get; set; }
        public int CurrentClout { get; set; }
        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }
        public List<KeyValuePair<string, int>> KeywordsWithOccurrenceCount { get; set; }
        public List<KeyValuePair<string, int>> PotentialKeywordsWithOccurrenceCount { get; set; }
        public double TwitterStreamVolume { get; set; }


        public DashboardModel(PostworthyUser user)
        {
            User = user;
            PotentialTweets = new List<Tweet>();
            PotentialReTweets = new List<Tweet>();
            Tweeted = new List<Tweet>();
            PotentialFriendRequests = new List<KeyValuePair<Tweep, int>>();
            LastTweetTime = DateTime.MaxValue;
            KeywordSuggestions = new List<KeyValuePair<string, int>>();
            TweetsPerHourLast24 = new double[24];
            TopFriendTweetCounts = new List<KeyValuePair<Tweep, int>>();
            KeywordsWithOccurrenceCount = new List<KeyValuePair<string, int>>();
            PotentialKeywordsWithOccurrenceCount = new List<KeyValuePair<string, int>>();

            LoadFromRepository();
        }

        private string RepoKey
        {
            get
            {
                return User.TwitterScreenName + "_" + RUNTIME_REPO_KEY;
            }
        }

        private void LoadFromRepository()
        {
            Repository<TweetBotRuntimeSettings> repo = Repository<TweetBotRuntimeSettings>.Instance;

            var runtimeSettings = (repo.Query(RepoKey) ?? new List<TweetBotRuntimeSettings> { new TweetBotRuntimeSettings() }).FirstOrDefault();

            if (runtimeSettings != null)
            {
                PotentialTweets = runtimeSettings.PotentialTweets;
                PotentialReTweets = runtimeSettings.PotentialReTweets;
                Tweeted = runtimeSettings.Tweeted;
                PotentialFriendRequests = runtimeSettings.PotentialFriendRequests.Select(x => new KeyValuePair<Tweep, int>(x.Key, x.Count)).ToList();
                LastTweetTime = runtimeSettings.LastTweetTime;
                ///TODO: Add the rest...

                //Load data from repository (i.e. TweetBotRuntimeSettings)
            }
        }
    }
}