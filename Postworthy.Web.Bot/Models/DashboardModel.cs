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
        public DateTime LastTweetTime { get; set; }
        public int TweetsSentSinceLastFriendRequest { get; set; }
        public List<Tweet> PotentialTweets { get; set; }
        public List<Tweet> PotentialReTweets { get; set; }
        public List<Tweet> Tweeted { get; set; }
        public List<KeyValuePair<string, int>> KeywordSuggestions { get; set; }
        public List<KeyValuePair<Tweep, int>> PotentialTweeps { get; set; } ///TODO: Replace Tuple with CountableItem

        public DashboardModel()
        {
            PotentialTweets = new List<Tweet>();
            PotentialReTweets = new List<Tweet>();
            Tweeted = new List<Tweet>();
            PotentialTweeps = new List<KeyValuePair<Tweep, int>>();
            LastTweetTime = DateTime.MaxValue;
            KeywordSuggestions = new List<KeyValuePair<string, int>>();

            LoadFromRepository();
        }

        private void LoadFromRepository()
        {
            //Load data from repository (i.e. TweetBotRuntimeSettings)
        }
    }
}