using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Repository;
using LinqToTwitter;
using Postworthy.Models.Core;
using System.Text.RegularExpressions;
using System.Drawing;
using Newtonsoft.Json;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Postworthy.Models.Twitter
{
    public class Tweet : RepositoryEntity, ISimilarText, ISimilarImage, ISimilarLinks, ITweet, IEquatable<Tweet>
    {
        private Status _Status;

        public Status Status { get { return _Status; } set { SetNotifyingProperty("Status", ref _Status, value); } }

        public Tweet()
        {
            Links = new List<UriEx>();
            //WordLetterPairHash = Enumerable.Range(0, 9).Select(x => Guid.NewGuid().ToString().GetHashCode()).ToArray();
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
            StatusID = ulong.Parse(this.Status.StatusID);
        }

        public ulong StatusID { get; set; }

        public override bool IsEqual(RepositoryEntity other)
        {
            if (other is Tweet)
            {
                var otherTweet = other as Tweet;
                if (this.StatusID == otherTweet.StatusID)
                    return true;
                /*
                 * Here we want to compute the text as fast as possible
                 * to do this we compare the precomputed letter hashes
                 * if we mis we return as soon as possible.
                 */
                else if (this.WordLetterPairHash.Length == otherTweet.WordLetterPairHash.Length)
                {
                    for (int i = 0; i < this.WordLetterPairHash.Length; i++)
                    {
                        if (this.WordLetterPairHash[i] != otherTweet.WordLetterPairHash[i])
                            return false;
                    }
                    return true;
                }
            }
            
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
            int index = 0;
            string[] words = text.ToLower().Split(' ').Where(x=>x.Length >= MIN_LENGTH).ToArray();
            WordLetterPairHash = new int[words.Sum(x => x.Length - (MIN_LENGTH - 1))];
            for (int w = 0; w < words.Length; w++)
            {
                int numPairs = words[w].Length - (MIN_LENGTH - 1);
                for (int j = 0; j < numPairs; j++)
                {
                    WordLetterPairHash[index++] = words[w].Substring(j, MIN_LENGTH).GetHashCode();
                }
            }
        }

        private int[] _WordLetterPairHash;
        public int[] WordLetterPairHash
        {
            get
            {
                if (_WordLetterPairHash == null && Status != null)
                    InitializeWordLetterPairHash(Status.Text);

                return _WordLetterPairHash;
            }
            set { SetNotifyingProperty("WordLetterPairHash", ref _WordLetterPairHash, value); }
        }

        #endregion

        #region ISimilarImage Members

        private void EncodeImage(Bitmap bmp)
        {
            Bitmap result = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage((Image)result))
            {
                g.DrawImage(bmp, 0, 0, 16, 16);
            }
            bmp.Dispose();

            var stream = new MemoryStream();
            result.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
            ImageBase64Encoded = Convert.ToBase64String(stream.ToArray());
        }

        private Bitmap DecodeImage(string ImageBase64Encoded)
        {
            if(_Image == null && !string.IsNullOrEmpty(ImageBase64Encoded))
                _Image = (Bitmap)Bitmap.FromStream(new MemoryStream(Convert.FromBase64String(ImageBase64Encoded)));

            return _Image;
        }

        public string ImageBase64Encoded 
        { 
            get; set; 
        }

        private Bitmap _Image;
        [JsonIgnore]
        public Bitmap Image
        {
            get { return DecodeImage(ImageBase64Encoded); }
            set { EncodeImage(value); }
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
                    Status.RetweetCount + Links.Sum(l=>l.ShareCount) : 0; 
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
                else if ((int)timespan.TotalMinutes >= 2)
                {
                    int nMinutes = (int)timespan.TotalMinutes;
                    sb.Append(nMinutes);
                    if (nMinutes == 1)
                        sb.Append(" minute");
                    else
                        sb.Append(" minutes");
                }
                // Moments Ago
                else
                {
                    sb.Append("moments");
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
                var score = Status.RetweetCount * 0.7 + Links.Sum(l => l.ShareCount) * 0.3;
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

        #region IEquatable<Tweet> Members

        public bool Equals(Tweet other)
        {
            return IsEqual(other);
        }

        #endregion

        public void PopulateExtendedData()
        {
            if (Links == null || Links.Count == 0)
            {
                var tp = new TweetProcessor(new List<Tweet> { this }, true);
                tp.Start();
            }
        }

        public static IEqualityComparer<ITweet> GetITweetTextComparer()
        {
            return GenericEqualityComparerFactory<ITweet>.Build(
                (x, y) => x.TweetText == y.TweetText,
                x => x.TweetText.GetHashCode());
        }

        public static IEqualityComparer<Tweet> GetTweetTextComparer()
        {
            return GenericEqualityComparerFactory<Tweet>.Build(
                (x, y) => x.IsEqual(y),
                x => x.TweetText.GetHashCode());
        }

        public Tweet Clone()
        {
            return JsonConvert.DeserializeObject<Tweet>(JsonConvert.SerializeObject(this));
        }
    }
}
