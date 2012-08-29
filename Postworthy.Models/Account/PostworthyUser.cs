using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Xml.Serialization;

namespace Postworthy.Models.Account
{
    [Serializable]
    public class PostworthyUser
    {
        [Required]
        [Display(Name = "Site Name")]
        public string SiteName { get; set; }

        [Required]
        [Display(Name = "Say Something About Yourself")]
        [DataType(DataType.MultilineText)]
        public string About { get; set; }

        [Required]
        [Display(Name = "Include Friends Tweets in Feed?")]
        public bool IncludeFriends { get; set; }

        [Display(Name = "Only Show Tweets with Links")]
        public bool OnlyTweetsWithLinks { get; set; }

        [Display(Name = "Twitter Screen Name")]
        public string TwitterScreenName { get; set; }

        [Display(Name = "Twitter OAuth Token")]
        public string OAuthToken { get; set; }

        [Display(Name = "Twitter Access Token")]
        public string AccessToken { get; set; }

        [Display(Name = "Analytics Script")]
        [DataType(DataType.MultilineText)]
        public string AnalyticsScript { get; set; }

        [Display(Name = "Ad Script")]
        [DataType(DataType.MultilineText)]
        public string AdScript { get; set; }

        [Display(Name = "Mobile Ad Script")]
        [DataType(DataType.MultilineText)]
        public string MobileAdScript { get; set; }

        [Display(Name = "Minimum Retweeted Count")]
        public int RetweetThreshold { get; set; }

        [Display(Name = "Track These Key Words (Comma Delimited)")]
        public string Track { get; set; }

        public bool CanAuthorize
        {
            get
            {
                return
                    !string.IsNullOrEmpty(OAuthToken) &&
                    !string.IsNullOrEmpty(AccessToken);
            }
        }

        public bool IsPrimaryUser
        {
            get
            {
                return TwitterScreenName.ToLower() == ConfigurationManager.AppSettings["PrimaryUser"].ToLower();
            }
        }
    }
}
