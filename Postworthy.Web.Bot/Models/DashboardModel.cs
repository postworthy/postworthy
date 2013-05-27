using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Postworthy.Models.Account;
using Postworthy.Models.Repository;
using Postworthy.Models.Twitter;
using Postworthy.Tasks.Bot.Settings;
using Postworthy.Models.Core;

namespace Postworthy.Web.Bot.Models
{
    public class DashboardModel
    {
        private const string RUNTIME_REPO_KEY = "TweetBotRuntimeSettings";

        public PostworthyUser User { get; set; }
        public DateTime BotStartupTime { get; set; }
        public TimeSpan Runtime { get { return DateTime.Now - BotStartupTime; } }
        public Int32 UpTime { get { return Runtime.Days; } }
        public DateTime LastTweetTime { get; set; }
        public int TweetsSentSinceLastFriendRequest { get; set; }
        public List<Tweet> PotentialTweets { get; set; }
        public List<Tweet> PotentialReTweets { get; set; }
        public List<Tweet> Tweeted { get; set; }
        public List<KeyValuePair<string, int>> KeywordSuggestions { get; set; }
        public List<KeyValuePair<Tweep, int>> PotentialFriendRequests { get; set; }
        public double TweetsPerHour { get; set; }
        public int[] TweetsPerHourLast24 { get; set; }
        public List<KeyValuePair<Tweep, int>> TopFriendTweetCounts { get; set; }
        public int MinimumRetweetLevel { get; set; }
        public int CurrentClout { get; set; }
        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }
        public List<KeyValuePair<string, int>> KeywordsWithOccurrenceCount { get; set; }
        public int KeywordOccurenceTotal { 
            get 
            {   int total = 0;
                foreach (var i in KeywordsWithOccurrenceCount) {
                    total += i.Value;
                }
                return total;
            } 
        }
        public List<KeyValuePair<string, int>> PotentialKeywordsWithOccurrenceCount { get; set; }
        public int PotentialKeywordOccurenceTotal {   
            get
            {   int total = 0;
                foreach (var i in PotentialKeywordsWithOccurrenceCount)
                {
                    total += i.Value;
                }
                return total;
            }
        }

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
            TweetsPerHourLast24 = new int[24];
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
            var me = new Tweep(User, Tweep.TweepType.None);
            Repository<TweetBotRuntimeSettings> repo = Repository<TweetBotRuntimeSettings>.Instance;

            var runtimeSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<TweetBotRuntimeSettings>(System.IO.File.OpenText("c:\\temp\\runtimesettings.demo.json.txt").ReadToEnd());

            //var runtimeSettings = (repo.Query(RepoKey) ?? new List<TweetBotRuntimeSettings> { new TweetBotRuntimeSettings() }).FirstOrDefault();

            if (runtimeSettings != null)
            {
                BotStartupTime = runtimeSettings.BotFirstStart;
                LastTweetTime = runtimeSettings.LastTweetTime;
                TweetsSentSinceLastFriendRequest = runtimeSettings.TweetsSentSinceLastFriendRequest;
                TweetsPerHour = runtimeSettings.Tweeted.Count() > 2 ? runtimeSettings.Tweeted.Count() / (1.0 * (runtimeSettings.Tweeted.Max(x => x.CreatedAt) - runtimeSettings.Tweeted.Min(x => x.CreatedAt)).TotalHours) : 0;
                MinimumRetweetLevel = (int)Math.Ceiling(runtimeSettings.MinimumRetweetLevel);
                CurrentClout = me.Followers().Count();
                FollowerCount = me.User.FollowersCount;
                FollowingCount = me.User.FriendsCount;
                TwitterStreamVolume = runtimeSettings.TotalTweetsProcessed / (1.0 * Runtime.TotalMinutes);
                    

                PotentialTweets = runtimeSettings.PotentialTweets;
                PotentialReTweets = runtimeSettings.PotentialReTweets;
                Tweeted = runtimeSettings.Tweeted;
                PotentialFriendRequests = runtimeSettings.PotentialFriendRequests
                    .Select(x => new KeyValuePair<Tweep, int>(x.Key, x.Count)).ToList();
                KeywordSuggestions = runtimeSettings.KeywordSuggestions
                    .Select(x => new KeyValuePair<string, int>(x.Key, x.Count)).ToList();
                TweetsPerHourLast24 = runtimeSettings.Tweeted
                    .Where(t => t.CreatedAt.AddHours(24) >= DateTime.Now)
                    .GroupBy(t => t.CreatedAt.Hour)
                    .Select(g => new { date = g.FirstOrDefault().CreatedAt, count = g.Count() })
                    .OrderBy(x => x.date)
                    .Select(x => x.count)
                    .ToArray();
                TopFriendTweetCounts = runtimeSettings.Tweeted
                    .Where(t => me.Followers().Select(f => f.ID).Contains(t.User.UserID))
                    .GroupBy(t => t.User.UserID)
                    .Select(g => new KeyValuePair<Tweep, int>(new Tweep(g.FirstOrDefault().User, Tweep.TweepType.None), g.Count()))
                    .ToList();
                KeywordsWithOccurrenceCount = runtimeSettings.Keywords
                    .Select(x => new KeyValuePair<string, int>(x.Key, x.Count)).ToList();
                PotentialKeywordsWithOccurrenceCount = runtimeSettings.KeywordSuggestions
                    .Select(x => new KeyValuePair<string, int>(x.Key, x.Count)).ToList();

            }
        }
    }
}