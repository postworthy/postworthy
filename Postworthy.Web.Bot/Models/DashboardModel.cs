using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Postworthy.Models.Account;
using Postworthy.Models.Repository;
using Postworthy.Models.Twitter;
using Postworthy.Tasks.Bot.Settings;
using Postworthy.Models.Core;
using Postworthy.Tasks.Bot.Streaming;
using Postworthy.Tasks.Bot.Models;

namespace Postworthy.Web.Bot.Models
{
    public class DashboardModel
    {
        private const string RUNTIME_REPO_KEY = "TweetBotRuntimeSettings";
        private const string COMMAND_REPO_KEY = "BotCommands";

        public PostworthyUser User { get; set; }
        public bool IsSimulationMode { get; set; }
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
        public double TweetsPerHourMax { get; set; }
        public int[] TweetsLastThirtyDays { get; set; }
        public List<KeyValuePair<Tweep, int>> TopFriendTweetCounts { get; set; }
        public int MinimumRetweetLevel { get; set; }
        public int CurrentClout { get; set; }
        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }
        public List<KeyValuePair<string, int>> KeywordsWithOccurrenceCount { get; set; }
        public List<Tweep> TwitterFollowSuggestions { get; set; }
        public List<KeyValuePair<string, int>> PotentialKeywordsWithOccurrenceCount { get; set; }
        public List<string> SeededKeywords { get; set; }
        public List<string> PendingKeywordAdd { get; set; }
        public List<string> PendingKeywordIgnore { get; set; }
        public List<string> PendingTweetRemoval { get; set; }

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
            TweetsLastThirtyDays = new int[31];
            TopFriendTweetCounts = new List<KeyValuePair<Tweep, int>>();
            KeywordsWithOccurrenceCount = new List<KeyValuePair<string, int>>();
            PotentialKeywordsWithOccurrenceCount = new List<KeyValuePair<string, int>>();
            TwitterFollowSuggestions = new List<Tweep>();
            PendingKeywordAdd = new List<string>();
            PendingKeywordIgnore = new List<string>();
            PendingTweetRemoval = new List<string>();


            LoadFromRepository();
        }

        private string RuntimeRepoKey
        {
            get
            {
                return User.TwitterScreenName + "_" + RUNTIME_REPO_KEY;
            }
        }

        private string CommandRepoKey
        {
            get
            {
                return User.TwitterScreenName + "_" + COMMAND_REPO_KEY;
            }
        }

        private void LoadFromRepository()
        {
            var me = new Tweep(User, Tweep.TweepType.None);
            CachedRepository<TweetBotRuntimeSettings> settingsRepo = CachedRepository<TweetBotRuntimeSettings>.Instance;
            SimpleRepository<BotCommand> commandRepo = new SimpleRepository<BotCommand>();

            //var runtimeSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<TweetBotRuntimeSettings>(System.IO.File.OpenText("c:\\temp\\runtimesettings.demo.json.txt").ReadToEnd());

            var runtimeSettings = (settingsRepo.Query(RuntimeRepoKey) ?? new List<TweetBotRuntimeSettings> { new TweetBotRuntimeSettings() }).FirstOrDefault();

            if (runtimeSettings != null)
            {
                IsSimulationMode = runtimeSettings.IsSimulationMode;
                BotStartupTime = runtimeSettings.BotFirstStart;
                LastTweetTime = runtimeSettings.LastTweetTime;
                TweetsSentSinceLastFriendRequest = runtimeSettings.TweetsSentSinceLastFriendRequest;
                TweetsPerHour = runtimeSettings.Tweeted.Count() > 1 ? runtimeSettings.Tweeted
                    .GroupBy(x => x.CreatedAt.ToShortDateString())
                    .SelectMany(y => y.GroupBy(z => z.CreatedAt.Hour))
                    .Select(x => x.Count())
                    .Average() : 0;
                TweetsPerHourMax = runtimeSettings.Tweeted.Count() > 2 ? runtimeSettings.Tweeted
                    .GroupBy(x => x.CreatedAt.ToShortDateString())
                    .SelectMany(y => y.GroupBy(z => z.CreatedAt.Hour))
                    .Select(x => x.Count())
                    .Max() : 0;
                MinimumRetweetLevel = (int)Math.Ceiling(runtimeSettings.MinimumRetweetLevel);
                CurrentClout = me.User.FollowersCount;
                FollowerCount = me.User.FollowersCount;
                FollowingCount = me.User.FriendsCount;
                TwitterStreamVolume = runtimeSettings.TotalTweetsProcessed / (1.0 * Runtime.TotalMinutes);
                    
                TwitterFollowSuggestions = runtimeSettings.TwitterFollowSuggestions;
                PotentialTweets = runtimeSettings.PotentialTweets;
                PotentialReTweets = runtimeSettings.PotentialReTweets;
                Tweeted = runtimeSettings.Tweeted;
                PotentialFriendRequests = runtimeSettings.PotentialFriendRequests
                    .Select(x => new KeyValuePair<Tweep, int>(x.Key, x.Count)).ToList();
                KeywordSuggestions = runtimeSettings.KeywordSuggestions
                    .Select(x => new KeyValuePair<string, int>(x.Key, x.Count)).ToList();
                runtimeSettings.Tweeted
                    .Where(t => t.CreatedAt.AddDays(30) >= DateTime.Now)
                    .GroupBy(t => t.CreatedAt.Day)
                    .Select(g => new { i = g.FirstOrDefault().CreatedAt.Day - 1, date = g.FirstOrDefault().CreatedAt, count = g.Count() })
                    .ToList()
                    .ForEach(x => TweetsLastThirtyDays[x.i] = x.count);
                TopFriendTweetCounts = runtimeSettings.Tweeted
                    .Where(t => me.Followers().Select(f => f.ID).Contains(t.User.UserID))
                    .GroupBy(t => t.User.UserID)
                    .Select(g => new KeyValuePair<Tweep, int>(new Tweep(g.FirstOrDefault().User, Tweep.TweepType.None), g.Count()))
                    .ToList();
                SeededKeywords = runtimeSettings.KeywordsToIgnore;
                KeywordsWithOccurrenceCount = runtimeSettings.Keywords
                    //.Concat(runtimeSettings.KeywordSuggestions.Where(x => x.Count >= TweetBotProcessingStep.MINIMUM_KEYWORD_COUNT))
                    .OrderByDescending(x => x.Count)
                    .ThenByDescending(x => x.Key)
                    .Select(x => new KeyValuePair<string, int>(x.Key, x.Count))
                    .ToList();
                PotentialKeywordsWithOccurrenceCount = runtimeSettings.KeywordSuggestions
                    //.Where(x => x.Count < TweetBotProcessingStep.MINIMUM_KEYWORD_COUNT)
                    .Select(x => new KeyValuePair<string, int>(x.Key, x.Count)).ToList();
            }

            var commands = commandRepo.Query(CommandRepoKey, where: x => !x.HasBeenExecuted);

            if (commands != null)
            {
                PendingKeywordAdd = commands.Where(c => c.Command == BotCommand.CommandType.AddKeyword && !c.HasBeenExecuted).Select(c => c.Value).Distinct().ToList();
                PendingKeywordIgnore = commands.Where(c => c.Command == BotCommand.CommandType.IgnoreKeyword && !c.HasBeenExecuted).Select(c => c.Value).Distinct().ToList();
                PendingTweetRemoval = commands.Where(c => (c.Command == BotCommand.CommandType.RemovePotentialTweet || c.Command == BotCommand.CommandType.RemovePotentialRetweet) && !c.HasBeenExecuted).Select(c => c.Value).Distinct().ToList();
            }
        }
    }
}