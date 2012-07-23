using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Repository;
using LinqToTwitter;
using Postworthy.Models.Core;
using System.Text.RegularExpressions;

namespace Postworthy.Models.Twitter
{
    public class Tweet : RepositoryEntity, ISimilarText, ITweet
    {
        private Status _Status;

        public Status Status { get { return _Status; } set { SetNotifyingProperty("Status", ref _Status, value); } }

        public Tweet()
        {
            Links = new List<UriEx>();
            /*
            Links.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler((x, y) =>
                {
                    if (y.NewItems != null)
                    {
                        foreach (var item in y.NewItems)
                        {
                            (item as UriEx).PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler((x2, y2) => { base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Links")); });
                        }
                    }
                    base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Links"));
                });
             */
        }

        public Tweet(Status status)
        {
            if (status == null) throw new ArgumentException("ctor Tweet(Status status) can not be passed a null value!");
            Links = new List<UriEx>();
            /*
            Links.CollectionChanged += new System.Collections.Specialized.NotifyCollectionChangedEventHandler((x,y) => 
                {
                    if (y.NewItems != null)
                    {
                        foreach (var item in y.NewItems)
                        {
                            (item as UriEx).PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler((x2, y2) => { base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Links")); });
                        }
                    }
                    base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Links"));
                });
            */
            Status = status;
            InitializeWordLetterPairHash(status.Text);
            StatusID = long.Parse(this.Status.StatusID);
        }

        public long StatusID { get; set; }

        public override bool IsEqual(RepositoryEntity other)
        {
            if (other is Tweet)
            {
                var otherTweet = other as Tweet;
                return this.StatusID == otherTweet.StatusID;
            }
            else
                return false;
        }

        public override string UniqueKey
        {
            get
            {
                return "tweet_" + StatusID;
            }
        }

        #region ISimilarText Members

        private void InitializeWordLetterPairHash(string text)
        {
            text = Regex.Replace(text, @"(http|ftp|https)://([\w+?\.\w+])+([a-zA-Z0-9\~\!\@\#\$\%\^\&\*\(\)_\-\=\+\\\/\?\.\:\;\'\,]*)?", "");
            text = text.Trim();
            int MIN_LENGTH = 3;
            WordLetterPairHash = new List<int>(text.Length);
            string[] words = text.ToLower().Split(' ');
            for (int w = 0; w < words.Length; w++)
            {
                if (words[w].Length >= MIN_LENGTH)
                {
                    int numPairs = words[w].Length - (MIN_LENGTH - 1);
                    for (int j = 0; j < numPairs; j++)
                    {
                        WordLetterPairHash.Add(words[w].Substring(j, MIN_LENGTH).GetHashCode());
                    }
                }
            }
        }

        private List<int> _WordLetterPairHash;
        public List<int> WordLetterPairHash
        {
            get
            {
                if (_WordLetterPairHash == null && Status != null)
                    InitializeWordLetterPairHash(Status.Text);

                return _WordLetterPairHash;
            }
            protected set { SetNotifyingProperty("WordLetterPairHash", ref _WordLetterPairHash, value); }
        }

        #endregion

        #region ITweet Members

        private List<UriEx> _Links;
        private string _TweetText;

        public List<UriEx> Links { get { return _Links; } set { SetNotifyingProperty("Links", ref _Links, value); } }
        public string TweetText
        {
            get
            {
                if (!string.IsNullOrEmpty(_TweetText))
                    return _TweetText;
                else if (Status != null)
                    return Status.Text;
                else return "";
            }
            set { SetNotifyingProperty("TweetText", ref _TweetText, value); }
        }

        public int RetweetCount
        {
            get 
            { 
                return Status != null ? 
                    Status.RetweetCount + Links.Sum(l=>l.UrlTweetCount) : 0; 
            }
        }

        public string TweetTime
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                TimeSpan timespan = DateTime.Now.ToUniversalTime() - Status.CreatedAt.ToUniversalTime();

                // A year or more?  Do "[Y] years and [M] months ago"
                if ((int)timespan.TotalDays >= 365)
                {
                    // Years
                    int nYears = (int)timespan.TotalDays / 365;
                    sb.Append(nYears);
                    if (nYears > 1)
                        sb.Append(" years");
                    else
                        sb.Append(" year");

                    // Months
                    int remainingDays = (int)timespan.TotalDays - (nYears * 365);
                    int nMonths = remainingDays / 30;
                    if (nMonths == 1)
                        sb.Append(" and ").Append(nMonths).Append(" month");
                    else if (nMonths > 1)
                        sb.Append(" and ").Append(nMonths).Append(" months");
                }
                // More than 60 days? (appx 2 months or 8 weeks)
                else if ((int)timespan.TotalDays >= 60)
                {
                    // Do months
                    int nMonths = (int)timespan.TotalDays / 30;
                    sb.Append(nMonths).Append(" months");
                }
                // Weeks? (7 days or more)
                else if ((int)timespan.TotalDays >= 7)
                {
                    int nWeeks = (int)timespan.TotalDays / 7;
                    sb.Append(nWeeks);
                    if (nWeeks == 1)
                        sb.Append(" week");
                    else
                        sb.Append(" weeks");
                }
                // Days? (1 or more)
                else if ((int)timespan.TotalDays >= 1)
                {
                    int nDays = (int)timespan.TotalDays;
                    sb.Append(nDays);
                    if (nDays == 1)
                        sb.Append(" day");
                    else
                        sb.Append(" days");
                }
                // Hours?
                else if ((int)timespan.TotalHours >= 1)
                {
                    int nHours = (int)timespan.TotalHours;
                    sb.Append(nHours);
                    if (nHours == 1)
                        sb.Append(" hour");
                    else
                        sb.Append(" hours");
                }
                // Minutes?
                else if ((int)timespan.TotalMinutes >= 1)
                {
                    int nMinutes = (int)timespan.TotalMinutes;
                    sb.Append(nMinutes);
                    if (nMinutes == 1)
                        sb.Append(" minute");
                    else
                        sb.Append(" minutes");
                }
                // Seconds?
                else if ((int)timespan.TotalSeconds >= 1)
                {
                    int nSeconds = (int)timespan.TotalSeconds;
                    sb.Append(nSeconds);
                    if (nSeconds == 1)
                        sb.Append(" second");
                    else
                        sb.Append(" seconds");
                }
                // Just say "1 second" as the smallest unit of time
                else
                {
                    sb.Append("1 second");
                }

                sb.Append(" ago");

                // For anything more than 6 months back, put " ([Month] [Year])" at the end, for better reference
                if ((int)timespan.TotalDays >= 30 * 6)
                {
                    sb.Append(" (" + Status.CreatedAt.ToString("MMMM") + " " + Status.CreatedAt.Year + ")");
                }

                return sb.ToString();
            }
        }

        public DateTime CreatedAt { get { return Status != null ? Status.CreatedAt : default(DateTime); } }

        public double TweetRank
        {
            get
            {
                //This algorithm is the Reddit Ranking Algorithm slightly modified for tweets
                //If you are looking at this you should also checkout the reddit code: https://github.com/reddit/reddit/wiki
                var score = RetweetCount;
                var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var twitterStartDate = Convert.ToInt64((new DateTime(2006, 7, 15, 0, 0, 0, DateTimeKind.Utc) - epoch).TotalSeconds);
                var createDate = Convert.ToInt64((Status.CreatedAt.ToUniversalTime() - epoch).TotalSeconds);
                var seconds = createDate - twitterStartDate;
                var order = Math.Log10(Math.Max(Math.Abs(score), 1));
                var sign = (score > 0) ? 1 : 0;
                return Math.Round(order + ((sign * seconds) / 45000.0), 7);
            }
        }

        public User User
        {
            get { return (Status != null) ? Status.User : null; }
        }
        #endregion
    }
}
