﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LinqToTwitter;
using Postworthy.Models.Core;

namespace Postworthy.Models.Twitter
{
    public class TweetGroup : ITweet
    {
        public TweetGroup() { }
        public TweetGroup(IGrouping<Tweet, Tweet> tg)
        {
            StatusID = tg.Key.StatusID;
            TweetText = tg.Key.TweetText;
            CreatedAt = tg.Key.CreatedAt;
            TweetTime = tg.Key.TweetTime;
            RetweetCount = tg.Key.RetweetCount + tg.Where(t => t.User.Name != tg.Key.User.Name).Sum(t => t.RetweetCount);
            User = tg.Key.Status.User;
            Links = tg.Where(t => t.User.Name != tg.Key.User.Name).SelectMany(x => x.Links).ToList();
            Links.AddRange(tg.Key.Links.Where(l => l.Image != null || l.Video != null));
        }

        #region ITweet Members

        public string StatusID { get; set; }

        public List<UriEx> Links { get; set; }

        public string TweetText { get; set; }

        public int RetweetCount { get; set; }

        public string TweetTime { get; set; }

        public DateTime CreatedAt { get; set; }

        public double TweetRank
        {
            get
            {
                //This algorithm is the Reddit Ranking Algorithm slightly modified for tweets
                //If you are looking at this you should also checkout the reddit code: https://github.com/reddit/reddit/wiki
                var score = RetweetCount;
                var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var twitterStartDate = Convert.ToInt64((new DateTime(2006, 7, 15, 0, 0, 0, DateTimeKind.Utc) - epoch).TotalSeconds);
                var createDate = Convert.ToInt64((CreatedAt.ToUniversalTime() - epoch).TotalSeconds);
                var seconds = createDate - twitterStartDate;
                var order = Math.Log10(Math.Max(Math.Abs(score), 1));
                var sign = (score > 0) ? 1 : 0;
                return Math.Round(order + ((sign * seconds) / 45000.0), 7);
            }
        }

        public User User { get; set; }

        #endregion
    }
}
