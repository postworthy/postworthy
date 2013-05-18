﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Postworthy.Models.Repository;
using Postworthy.Models.Twitter;

namespace Postworthy.Tasks.Bot.Settings
{
    public class TweetBotRuntimeSettings : RepositoryEntity
    {
        public Guid SettingsGuid { get; set; }

        public DateTime BotFirstStart { get; set; }
        public double AverageWeight { get; set; }
        public DateTime LastTweetTime { get; set; }
        public bool TweetOrRetweet { get; set; }
        public int TweetsSentSinceLastFriendRequest { get; set; }
        public List<Tweet> PotentialTweets { get; set; }
        public List<Tweet> PotentialReTweets { get; set; }
        public List<Tweet> Tweeted { get; set; }
        public List<Tuple<int, string>> KeywordSuggestions { get; set; }
        public List<Tuple<int, Tweep>> PotentialTweeps { get; set; }

        public TweetBotRuntimeSettings()
        {
            BotFirstStart = DateTime.Now;
            SettingsGuid = Guid.NewGuid();
            PotentialTweets = new List<Tweet>();
            PotentialReTweets = new List<Tweet>();
            Tweeted = new List<Tweet>();
            PotentialTweeps = new List<Tuple<int, Tweep>>();
            LastTweetTime = DateTime.MaxValue;
            KeywordSuggestions = new List<Tuple<int, string>>();
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
