using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Postworthy.Models.Twitter;

namespace Postworthy.Web.Bot.Models
{
    public class DashboardModel
    {
        public DateTime BotStartupTime { get; set; }
        public TimeSpan Runtime { get { return DateTime.Now - BotStartupTime; } }
        public DateTime LastTweetTime { get; set; }
        public int TweetsSentSinceLastFriendRequest { get; set; }
        public List<Tweet> PotentialTweets { get; set; }
        public List<Tweet> PotentialReTweets { get; set; }
        public List<Tweet> Tweeted { get; set; }
        public List<KeyValuePair<string, int>> KeywordSuggestions { get; set; }
        public List<KeyValuePair<Tweep, int>> PotentialTweeps { get; set; } ///TODO: Replace Tuple with CountableItem
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


        public DashboardModel()
        {
            PotentialTweets = new List<Tweet>();
            PotentialReTweets = new List<Tweet>();
            Tweeted = new List<Tweet>();
            PotentialTweeps = new List<KeyValuePair<Tweep, int>>();
            LastTweetTime = DateTime.MaxValue;
            KeywordSuggestions = new List<KeyValuePair<string, int>>();
            TweetsPerHourLast24 = new double[24];
            TopFriendTweetCounts = new List<KeyValuePair<Tweep, int>>();
            KeywordsWithOccurrenceCount = new List<KeyValuePair<string, int>>();
            PotentialKeywordsWithOccurrenceCount = new List<KeyValuePair<string, int>>();

            LoadFromRepository();
        }

        private void LoadFromRepository()
        {
            //Load data from repository (i.e. TweetBotRuntimeSettings)
        }
    }
}