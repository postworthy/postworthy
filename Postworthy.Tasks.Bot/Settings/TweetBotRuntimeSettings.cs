using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Postworthy.Models.Repository;
using Postworthy.Models.Twitter;
using Postworthy.Tasks.Bot.Models;

namespace Postworthy.Tasks.Bot.Settings
{
    public class TweetBotRuntimeSettings : RepositoryEntity
    {
        public Guid SettingsGuid { get; set; }

        public long TotalTweetsProcessed { get; set; }
        public DateTime BotFirstStart { get; set; }
        public double AverageWeight { get; set; }
        public DateTime LastTweetTime { get; set; }
        public bool TweetOrRetweet { get; set; }
        public int TweetsSentSinceLastFriendRequest { get; set; }
        public List<Tweet> PotentialTweets { get; set; }
        public List<Tweet> PotentialReTweets { get; set; }
        public List<Tweet> Tweeted { get; set; }
        public List<CountableItem> Keywords { get; set; }
        public List<CountableItem> KeywordSuggestions { get; set; }
        public List<string> KeywordsToIgnore { get; set; }
        public List<CountableItem<Tweep>> PotentialFriendRequests { get; set; }

        public double MinimumRetweetLevel
        {
            get
            {
                if (this.Tweeted != null && this.Tweeted.Count > 5)
                {
                    double stdev = 0;
                    var values = this.Tweeted.Select(x => x.RetweetCount);
                    double avg = values.Average();
                    //Get Standard Deviation
                    stdev = Math.Sqrt(values.Sum(d => (d - avg) * (d - avg)) / values.Count());

                    return values.Where(x => x <= (avg + stdev * 2) && x >= (avg - stdev * 2)).Average() * 0.65;
                }

                return 2.0;
            }
        }

        public TweetBotRuntimeSettings()
        {
            BotFirstStart = DateTime.Now;
            SettingsGuid = Guid.NewGuid();
            PotentialTweets = new List<Tweet>();
            PotentialReTweets = new List<Tweet>();
            Tweeted = new List<Tweet>();
            PotentialFriendRequests = new List<CountableItem<Tweep>>();
            LastTweetTime = DateTime.MaxValue;
            Keywords = new List<CountableItem>();
            KeywordSuggestions = new List<CountableItem>();
            KeywordsToIgnore = new List<string>();
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
