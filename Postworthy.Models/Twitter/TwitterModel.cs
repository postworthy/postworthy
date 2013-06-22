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
using Postworthy.Models.Repository.Providers;
using HtmlAgilityPack;

namespace Postworthy.Models.Twitter
{
    public sealed class TwitterModel
    {
        private static volatile TwitterModel instance;
        private static object instance_lock = new object();
        private static object tweets_lock = new object();

        public const string CACHED_TWEETS = "CachedTweets";
        public const string TRACKER = "PostworthyTracker";
        public const string TWEETS = "_tweets";
        public const string FRIENDS = "_friends";
        private const string GROUPING_RESULTS = "_GROUPING_RESULTS";

        public static string GROUPING 
        {
            get { return UsersCollection.PrimaryUser().TwitterScreenName + GROUPING_RESULTS; }
        }

        private TwitterModel()
        {

            #region Tweets
            Repository<Tweet>.Instance.RefreshData += new Func<string,List<Tweet>>(key => 
                {
                    Repository<Tweet>.Instance.RefreshLocalCache(key);
                    return null;
                });
            #endregion

            #region Friends
            Repository<Tweep>.Instance.RefreshData += new Func<string, List<Tweep>>(key =>
            {
                Repository<Tweep>.Instance.RefreshLocalCache(key);
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

        public List<ITweet> PrimaryUserTweetCache 
        { 
            get
            {
                return HttpRuntime.Cache[UsersCollection.PrimaryUser().TwitterScreenName + "_" + CACHED_TWEETS] as List<ITweet>;
            }
        }

        public List<ITweet> Tweets(string screenname, bool includeRelevantScreenNames = true)
        {
            List<ITweet> returnTweets;
            //Since the grouping can be a somewhat expensive task we will cache the results to gain a speed up.
            var cachedResponse = HttpRuntime.Cache[screenname + "_" + CACHED_TWEETS] as List<ITweet>;
            if (cachedResponse != null && cachedResponse.Count > 0)
                returnTweets = cachedResponse;
            else
            {
                var sharedResult = Repository<TweetGroup>.Instance.Query(GROUPING, (Repository<TweetGroup>.Limit)0);
                //Check to see if we have a recent grouping available (by recent it must be within the last 30 minutes)
                //This is what the Grouping task does for us in the background
                var useShared = sharedResult != null && sharedResult.Count() > 0 && sharedResult[0].CreatedOn.AddMinutes(30) > DateTime.Now;
                if (useShared)
                {
                    lock (tweets_lock)
                    {
                        cachedResponse = HttpRuntime.Cache[screenname + "_" + CACHED_TWEETS] as List<ITweet>;
                        if (cachedResponse != null && cachedResponse.Count > 0)
                            returnTweets = cachedResponse;
                        else
                        {
                            var results = sharedResult.Cast<ITweet>().ToList();
                            results.AddRange(GetTweets(screenname, includeRelevantScreenNames, sharedResult.SelectMany(g => g.GroupStatusIDs).ToList()));
                            HttpRuntime.Cache.Add(screenname + "_" + CACHED_TWEETS, results, null, DateTime.Now.AddMinutes(15), Cache.NoSlidingExpiration, CacheItemPriority.Normal, null);
                            returnTweets = results;
                        }
                    }
                }
                //If there is nothing local and nothing in shared cache then pull something from the repo
                //These results are raw and ungrouped since they are pulled on the fly and grouping can be time consuming
                else
                {
                    lock (tweets_lock)
                    {
                        cachedResponse = HttpRuntime.Cache[screenname + "_" + CACHED_TWEETS] as List<ITweet>;
                        if (cachedResponse != null && cachedResponse.Count > 0)
                            returnTweets = cachedResponse;
                        else
                        {
                            var results = GetTweets(screenname, includeRelevantScreenNames);

                            if (results != null && results.Count > 0)
                                HttpRuntime.Cache.Add(screenname + "_" + CACHED_TWEETS, results, null, DateTime.Now.AddMinutes(5), Cache.NoSlidingExpiration, CacheItemPriority.Normal, null);

                            returnTweets = results;
                        }
                    }
                }
            }

            //Before we return the results we should order the results rank
            return returnTweets.OrderByTweetRank().ToList();
        }

        private List<ITweet> GetTweets(string screenname, bool includeRelevantScreenNames, List<ulong> excludeStatusIDs = null)
        {
            List<string> screenNames = null;

            var user = UsersCollection.Single(screenname);

            if (includeRelevantScreenNames)
                screenNames = GetRelevantScreenNames(screenname);
            else
                screenNames = new List<string> { screenname.ToLower() };

            int RetweetThreshold = UsersCollection.PrimaryUser().RetweetThreshold;

            Expression<Func<Tweet, bool>> where = t =>
                //If there are any IDs we want to filter out
                (excludeStatusIDs == null || !excludeStatusIDs.Contains(t.StatusID)) &&
                //Should everything be displayed or do you only want content
                (user.OnlyTweetsWithLinks == false || (t.Links != null && t.Links.Count > 0)) &&
                //Minumum threshold applied so we get results worth seeing (if it is your own tweet it gets a pass on this step)
                ((t.RetweetCount >= RetweetThreshold /*&& t.CreatedAt > DateTime.Now.AddHours(-48)*/) || t.User.Identifier.ScreenName.ToLower() == screenname.ToLower());

            var tweets = screenNames
                //For each screen name (i.e. - you and your friends if included) select the most recent tweets
                .SelectMany(x => Repository<Tweet>.Instance.Query(x + TWEETS, limit: Repository<Tweet>.Limit.Limit100, where: where) ?? new List<Tweet>())
                //Order all tweets based on rank
                .OrderByDescending(t => t.TweetRank)
                .Distinct(Tweet.GetTweetTextComparer())
                .ToList();

            if(!string.IsNullOrEmpty(UsersCollection.PrimaryUser().Track))
                tweets.AddRange(Repository<Tweet>.Instance.Query(TRACKER + TWEETS, limit: Repository<Tweet>.Limit.Limit1000, where: where) ?? new List<Tweet>());

            return tweets.Cast<ITweet>().ToList();
        }

        public List<string> GetRelevantScreenNames(string screenname)
        {
            var screenNames = new List<string> { screenname.ToLower() };

            if (UsersCollection.Single(screenname) != null && UsersCollection.Single(screenname).IncludeFriends)
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
            return friends ?? new List<Tweep>();
        }

        public Tweep CreateFriendship(Tweep follow, string screenname = null)
        {
            if (string.IsNullOrEmpty(screenname)) screenname = UsersCollection.PrimaryUser().TwitterScreenName;

            var user = GetAuthorizedTwitterContext(screenname).CreateFriendship(follow.User.Identifier.UserID, null, true);

            return new Tweep(user, user.Following ? Tweep.TweepType.Following : Tweep.TweepType.None);
        }

        public void UpdateStatus(string statusText, string screenname = null, bool processStatus = true)
        {
            if (string.IsNullOrEmpty(screenname)) screenname = UsersCollection.PrimaryUser().TwitterScreenName;

            var status = GetAuthorizedTwitterContext(screenname).UpdateStatus(statusText);

            if (processStatus)
            {
                status = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname)
                                .Status
                                .Where(s => s.StatusID == status.StatusID && s.ScreenName == screenname && s.IncludeEntities == true && s.Type == StatusType.User && s.Count == 1)
                                .ToList().FirstOrDefault();

                if (status != null)
                {
                    var tweet = new Tweet(status);
                    tweet.PopulateExtendedData();
                    Repository<Tweet>.Instance.Save(screenname + TWEETS, tweet);
                }
            }
        }

        public void Retweet(string statusId, string screenname = null)
        {
            if (string.IsNullOrEmpty(screenname)) screenname = UsersCollection.PrimaryUser().TwitterScreenName;

            var status = GetAuthorizedTwitterContext(screenname).Retweet(statusId);
        }

        public void UpdateFriendsForPrimaryUser()
        {
            string screenname = UsersCollection.PrimaryUser().TwitterScreenName;

            var user = UsersCollection.Single(screenname);
            if (user != null && user.CanAuthorize)
            {
                try
                {
                    var friends = GetFollowers(screenname);

                    if (friends != null && Repository<Tweep>.Instance.ContainsKey(screenname + FRIENDS))
                    {
                        var repoFriends = Repository<Tweep>.Instance.Query(screenname + FRIENDS);
                        friends = friends.Except(repoFriends).ToList();
                    }

                    if (friends != null)
                    {
                        Repository<Tweep>.Instance.Save(screenname + FRIENDS, friends);
                        Repository<Tweep>.Instance.FlushChanges();
                    }
                }
                catch { }
            }
        }

        public List<Tweep> GetFollowers(string screenname)
        {
            var llf = GetFollowersWithLazyLoading(screenname);
            return llf.Select(x => x.Value).ToList();
        }

        public List<Tweep> GetSuggestedFollowsForPrimaryUser()
        {
            /*
            var context = TwitterModel.Instance.GetAuthorizedTwitterContext(UsersCollection.PrimaryUser().TwitterScreenName);

            var categories = context.User.Where(u => u.Type == UserType.Categories).FirstOrDefault().Categories;
            
            var results = new List<Tweep>();

            //Randomly order the Categories
            categories = categories.OrderBy(c => Guid.NewGuid()).Take(15).ToList();

            foreach(var category in categories)
            {
                try
                {
                    results.AddRange(context
                        .User.Where(u => u.Type == UserType.Category && u.Slug == category.Slug)
                        .SelectMany(x => x.Categories.Select(c => c.Users).SelectMany(u => u).Select(u => new Tweep(u, Tweep.TweepType.Suggested))));
                }
                catch (LinqToTwitter.TwitterQueryException){ }
            }

            return results;
             */

            var result = Postworthy.Models.Twitter.TwitterScraper.GetTwitterUrl("https://twitter.com/i/users/recommendations?limit=100");
            var resultObject = Newtonsoft.Json.JsonConvert.DeserializeAnonymousType(result, new { user_recommendations_html = "" });

            var doc = new HtmlDocument();
            doc.LoadHtml(resultObject.user_recommendations_html);
            var tweeps = doc.DocumentNode
                .SelectNodes("//div[@data-user-id]")
                .Select(x=>x.GetAttributeValue("data-user-id",""))
                .Distinct()
                .Select(x => GetLazyLoadedTweep(x, Tweep.TweepType.Suggested));

            return tweeps.Take(50).Select(x => x.Value).OrderByDescending(x=>x.Clout()).ToList();
        }

        public List<LazyLoader<Tweep>> GetFollowersWithLazyLoading(string screenname)
        {
            var context = TwitterModel.Instance.GetAuthorizedTwitterContext(UsersCollection.PrimaryUser().TwitterScreenName);

            try
            {
                var friends = context
                    .SocialGraph
                    .Where(g => g.ScreenName == screenname && g.Type == SocialGraphType.Followers && g.Cursor == "-1")
                    .SelectMany(g => g.IDs)
                    .Select(s => new LazyLoader<Tweep>(s,
                        (() => new Tweep(context.User.Where(u => u.Type == UserType.Show && u.UserID == s).First(), Tweep.TweepType.Follower))))
                    .ToList();

                friends.AddRange(context
                    .SocialGraph
                    .Where(g => g.ScreenName == screenname && g.Type == SocialGraphType.Friends && g.Cursor == "-1")
                    .SelectMany(g => g.IDs)
                    .Except(friends.Select(x => x.ID))
                    .Select(s => new LazyLoader<Tweep>(s,
                        (() => new Tweep(context.User.Where(u => u.Type == UserType.Show && u.UserID == s).First(), Tweep.TweepType.Following)))));

                return friends;
            }
            catch { return null; }
        }

        public LazyLoader<Tweep> GetLazyLoadedTweep(string userID, Tweep.TweepType tweepType = Tweep.TweepType.None)
        {
            var context = TwitterModel.Instance.GetAuthorizedTwitterContext(UsersCollection.PrimaryUser().TwitterScreenName);

            return new LazyLoader<Tweep>(
                userID,
                (() => new Tweep(context.User.Where(u => u.Type == UserType.Show && u.UserID == userID).First(), tweepType)));
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