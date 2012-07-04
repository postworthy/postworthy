using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Net;
using System.Text.RegularExpressions;
using LinqToTwitter;
using System.Text;
using System.Web.Caching;
using System.Configuration;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq.Expressions;
using Postworthy.Models.Repository;
using Postworthy.Models.Account;
using Postworthy.Models.Core;

namespace Postworthy.Models.Twitter
{
    public sealed class TwitterModel
    {
        private static volatile TwitterModel instance;
        private static object instance_lock = new object();
        private static readonly object locker_tweets = new object();
        private static readonly object locker_friends = new object();

        private const string TWEETS = "_tweets";
        private const string FRIENDS = "_friends";

        private TwitterModel()
        {

            #region Tweets
            Repository<Tweet>.Instance.RefreshData += new Func<string,List<Tweet>>(key => 
                {
                    Repository<Tweet>.Instance.UpdateLocalCache(key);
                    return null;
                });
            #endregion

            #region Friends
            Repository<Tweep>.Instance.RefreshData += new Func<string, List<Tweep>>(key =>
            {
                Repository<Tweep>.Instance.UpdateLocalCache(key);
                return null;
            });
            #endregion
        }

        public static TwitterModel Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (instance_lock)
                    {
                        if (instance == null)
                            instance = new TwitterModel();
                    }
                }
                return instance;
            }
        }

        public List<ITweet> Tweets(string screenname, bool includeRelevantScreenNames = true)
        {
            List<string> screenNames = null;

            var user = UsersCollection.Single(screenname);

            if (includeRelevantScreenNames)
                screenNames = GetRelevantScreenNames(screenname);
            else
                screenNames = new List<string> { screenname.ToLower() };
            /*
            var tweets = screenNames.SelectMany(x => Repository<Tweet>.Instance.Find(x + TWEETS) ?? new List<Tweet>())
                .Where(x => user.OnlyTweetsWithLinks == false || (x.Links != null && x.Links.Count > 0))
                .GroupBy(x => x.Status.User.Identifier.ScreenName)
                .SelectMany(g => g.OrderByDescending(t => t.TweetRank).Take(15))
                .OrderByDescending(t => t.TweetRank)
                .Take(100)
                .OrderByDescending(x => x.Status.CreatedAt)
                .ToList();
            */

            Expression<Func<Tweet, bool>> where = t => 
                (user.OnlyTweetsWithLinks == false || (t.Links != null && t.Links.Count > 0)) && 
                ((t.RetweetCount > 5 && t.CreatedAt > DateTime.Now.AddHours(-48)) || t.User.Identifier.ScreenName.ToLower() == screenname.ToLower());

            var tweets = screenNames.SelectMany(x => Repository<Tweet>.Instance.Query(x + TWEETS, where: where) ?? new List<Tweet>())
                .OrderBy(t => t.Status.CreatedAt)
                .GroupSimilar()
                .Select(g => new TweetGroup(g));

            return tweets.Cast<ITweet>().ToList();
        }

        public List<string> GetRelevantScreenNames(string screenname)
        {
            var screenNames = new List<string> { screenname.ToLower() };

            if (UsersCollection.Single(screenname).IncludeFriends)
                screenNames.AddRange((Friends(screenname) ?? new List<Tweep>()).Where(f => f.Type != Tweep.TweepType.Follower).Select(f => f.User.Identifier.ScreenName.ToLower()));

            return screenNames;
        }

        public List<Tweep> Friends(string screenname)
        {
            var friends = Repository<Tweep>.Instance.Query(screenname + FRIENDS);
            /*
            if (friends == null)
            {
                LoadFriendCache();
                friends = Repository<Tweep>.Instance.Find(screenname + FRIENDS);
            }
             * */
            return friends;
        }

        public void UpdateStatus(string statusText, string screenname)
        {
            var status = GetAuthorizedTwitterContext(screenname).UpdateStatus(statusText);
            status = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname)
                            .Status
                            .Where(s => s.StatusID == status.StatusID && s.ScreenName == screenname && s.IncludeEntities == true && s.Type == StatusType.User && s.Count == 1)
                            .ToList().FirstOrDefault();

            if (status != null)
            {
                var tweet = new Tweet(status);
                var tp = new TweetProcessor(new List<Tweet> { tweet }, true);
                tp.Start();
                Repository<Tweet>.Instance.Save(screenname + TWEETS, tweet);
            }
        }

        public TwitterContext GetAuthorizedTwitterContext(string screenname)
        {
            var pm = UsersCollection.Single(screenname);
            if (pm.CanAuthorize)
            {
                return new TwitterContext(new MvcAuthorizer()
                {
                    Credentials = new LinqToTwitter.InMemoryCredentials()
                    {
                        AccessToken = pm.AccessToken,
                        ConsumerKey = ConfigurationManager.AppSettings["TwitterCustomerKey"],
                        ConsumerSecret = ConfigurationManager.AppSettings["TwitterCustomerSecret"],
                        OAuthToken = pm.OAuthToken
                    }
                });
            }
            else
            {
                return new TwitterContext();
            }

        }
    }
}