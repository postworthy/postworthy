using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LinqToTwitter;
using Postworthy.Models.Core;
using Postworthy.Models.Repository;

namespace Postworthy.Models.Twitter
{
    public class TweetGroup : RepositoryEntity, ITweet
    {
        public DateTime CreatedOn { get; set; }
        public List<ulong> GroupStatusIDs { get; set; }
        public int LinkRetweetCount { get; set; }
        public int LinkFacebookShareCount { get; set; }
        public int ShareCount { get { return LinkRetweetCount + LinkFacebookShareCount; } }

        public TweetGroup() 
        {
            CreatedOn = DateTime.Now;
        }
        public TweetGroup(IGrouping<Tweet, Tweet> tg)
        {
            GroupStatusIDs = tg.Select(g => g.StatusID).ToList();
            CreatedOn = DateTime.Now;
            StatusID = tg.Key.StatusID;
            TweetText = tg.Key.TweetText;
            CreatedAt = tg.Key.CreatedAt;
            TweetTime = tg.Key.TweetTime;
            RetweetCount = tg.Key.RetweetCount + tg.Where(t => t.User.Name != tg.Key.User.Name).Sum(t => t.RetweetCount);
            LinkRetweetCount = tg.SelectMany(x => x.Links).Sum(x => x.UrlTweetCount);
            LinkFacebookShareCount = tg.SelectMany(x => x.Links).Sum(x => x.UrlFacebookShareCount);
            User = tg.Key.Status.User;
            Links = tg.Where(t => t.User.Name != tg.Key.User.Name).SelectMany(x => x.Links).ToList();
            Links.AddRange(tg.Key.Links.Where(l => l.Image != null || l.Video != null));
        }

        #region ITweet Members

        public ulong StatusID { get; set; }

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
                var score = (ShareCount - ShareCount) * 0.7 - ShareCount * 0.3;
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

        public override string UniqueKey
        {
            get { return "tweetgroup_" + StatusID; }
        }

        public override bool IsEqual(RepositoryEntity other)
        {
            if (other is TweetGroup)
            {
                var otherTweet = other as TweetGroup;
                return this.StatusID == otherTweet.StatusID;
            }
            else
                return false;
        }
    }
}
