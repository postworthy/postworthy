using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Xml.Serialization;
using Postworthy.Models.Repository;

namespace Postworthy.Models.Account
{
    [Serializable]
    public class PostworthyUser : RepositoryEntity
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

        public List<string> PrimaryDomains { get; set; }

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

                var domain = System.Web.HttpContext.Current != null ? System.Web.HttpContext.Current.Request.Url.Authority.ToLower().Replace("www.","") : null;
                if (!string.IsNullOrEmpty(domain) &&
                    PrimaryDomains != null &&
                    PrimaryDomains.Any(x => x.ToLower() == domain))
                    return true;
                else if (!string.IsNullOrEmpty(TwitterScreenName) &&
                    !string.IsNullOrEmpty(ConfigurationManager.AppSettings["PrimaryUser"] ?? "") &&
                    TwitterScreenName.ToLower() == (ConfigurationManager.AppSettings["PrimaryUser"] ?? "").ToLower())
                    return true;
#if (DEBUG)
                else if (!string.IsNullOrEmpty(TwitterScreenName) &&
                    !string.IsNullOrEmpty(ConfigurationManager.AppSettings["PrimaryDebugUser"] ?? "") &&
                    TwitterScreenName.ToLower() == (ConfigurationManager.AppSettings["PrimaryDebugUser"] ?? "").ToLower())
                    return true;
#endif
                else 
                    return false;
            }
        }

        public override string UniqueKey
        {
            get { return this.TwitterScreenName; }
        }

        public override bool IsEqual(RepositoryEntity other)
        {
            return other.UniqueKey == this.UniqueKey;
        }
    }
}
