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
using System.Reflection;

namespace Postworthy.Models.Twitter
{
    public sealed class TwitterModel
    {
        [ThreadStatic]
        private static volatile TwitterModel instance;
        private static object instance_lock = new object();
        private static object tweets_lock = new object();

        private const string _CACHED_TWEETS = "CachedTweets";
        private const string _TRACKER = "PostworthyTracker";
        private const string _TWEETS = "_tweets";
        private const string _FRIENDS = "_friends";
        private const string _GROUPING_RESULTS = "_GROUPING_RESULTS";
        private const string _CONTENT_RESULTS = "_CONTENT_RESULTS";
        private const string _CONTENT_INDEX = "_CONTENT_INDEX";
        private const string _ARTICLE_RESULTS = "_ARTICLE_RESULTS";
        private const string _ARTICLE_INDEX = "_ARTICLE_INDEX";

        public readonly PostworthyUser PrimaryUser = null;

        public static string VERSION
        {
            get { return "_v" + Assembly.GetCallingAssembly().GetName().Version.ToString(); }
        }
        public string TRACKER
        {
            get { return PrimaryUser.TwitterScreenName + _TRACKER + _TWEETS + VERSION; }
        }
        public string TWEETS
        {
            get { return _TWEETS + VERSION; }
        }
        public string FRIENDS
        {
            get { return _FRIENDS + VERSION; }
        }

        public string GROUPING
        {
            get { return PrimaryUser.TwitterScreenName + _GROUPING_RESULTS + VERSION; }
        }

        public string CONTENT
        {
            get { return PrimaryUser.TwitterScreenName + _CONTENT_RESULTS + VERSION; }
        }

        public string CONTENT_INDEX
        {
            get { return PrimaryUser.TwitterScreenName + _CONTENT_INDEX + VERSION; }
        }

        public string ARTICLE
        {
            get { return PrimaryUser.TwitterScreenName + _ARTICLE_RESULTS + VERSION; }
        }

        public string ARTICLE_INDEX
        {
            get { return PrimaryUser.TwitterScreenName + _ARTICLE_INDEX + VERSION; }
        }

        private TwitterModel(string screenname)
        {
            PrimaryUser = UsersCollection.Single(screenname);
            if (PrimaryUser == null)
                throw new Exception(screenname + " user not found!");
        }

        public static TwitterModel Instance(string screenname)
        {
            if (instance == null)
            {
                lock (instance_lock)
                {
                    if (instance == null)
                    {
                        if (screenname != null)
                            instance = new TwitterModel(screenname);
                        else
                            throw new Exception("ScreenName cannot be null on first call to TwitterModel.Instance");
                    }
                }
            }
            else if (screenname != null && instance.PrimaryUser.TwitterScreenName.ToLower() != screenname.ToLower())
            {
                instance = null;
                return Instance(screenname);
            }

            return instance;
        }

        public void SetPrimaryUser(string screenname)
        {
            
        }

        public List<ITweet> PrimaryUserTweetCache
        {
            get
            {
                return HttpRuntime.Cache[PrimaryUser.TwitterScreenName + "_" + _CACHED_TWEETS] as List<ITweet>;
            }
        }

        public List<ITweet> Tweets(string screenname, bool includeRelevantScreenNames = true)
        {
            List<ITweet> returnTweets;
            //Since the grouping can be a somewhat expensive task we will cache the results to gain a speed up.
            var cachedResponse = HttpRuntime.Cache[screenname + "_" + _CACHED_TWEETS] as List<ITweet>;
            if (cachedResponse != null && cachedResponse.Count > 0)
                returnTweets = cachedResponse;
            else
            {
                var sharedResult = CachedRepository<TweetGroup>.Instance(PrimaryUser.TwitterScreenName).Query(GROUPING, pageSize: 1000);
                //Check to see if we have a recent grouping available (by recent it must be within the last 30 minutes)
                //This is what the Grouping task does for us in the background
                var useShared = sharedResult != null && sharedResult.Count() > 0 && sharedResult.First().CreatedOn.AddMinutes(30) > DateTime.Now;
                if (useShared)
                {
                    lock (tweets_lock)
                    {
                        cachedResponse = HttpRuntime.Cache[screenname + "_" + _CACHED_TWEETS] as List<ITweet>;
                        if (cachedResponse != null && cachedResponse.Count > 0)
                            returnTweets = cachedResponse;
                        else
                        {
                            var results = sharedResult.Cast<ITweet>().ToList();
                            results.AddRange(GetTweets(screenname, includeRelevantScreenNames, sharedResult.SelectMany(g => g.GroupStatusIDs).ToList()));
                            HttpRuntime.Cache.Add(screenname + "_" + _CACHED_TWEETS, results, null, DateTime.Now.AddMinutes(15), Cache.NoSlidingExpiration, CacheItemPriority.Normal, null);
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
                        cachedResponse = HttpRuntime.Cache[screenname + "_" + _CACHED_TWEETS] as List<ITweet>;
                        if (cachedResponse != null && cachedResponse.Count > 0)
                            returnTweets = cachedResponse;
                        else
                        {
                            var results = GetTweets(screenname, includeRelevantScreenNames);

                            if (results != null && results.Count > 0)
                                HttpRuntime.Cache.Add(screenname + "_" + _CACHED_TWEETS, results, null, DateTime.Now.AddMinutes(5), Cache.NoSlidingExpiration, CacheItemPriority.Normal, null);

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

            int RetweetThreshold = PrimaryUser.RetweetThreshold;

            Func<Tweet, bool> where = t =>
                //If there are any IDs we want to filter out
                (excludeStatusIDs == null || !excludeStatusIDs.Contains(t.StatusID)) &&
                    //Should everything be displayed or do you only want content
                (user.OnlyTweetsWithLinks == false || (t.Links != null && t.Links.Count > 0)) &&
                    //Minumum threshold applied so we get results worth seeing (if it is your own tweet it gets a pass on this step)
                ((t.RetweetCount >= RetweetThreshold /*&& t.CreatedAt > DateTime.Now.AddHours(-48)*/) || t.User.ScreenName.ToLower() == screenname.ToLower());

            var tweets = screenNames
                //For each screen name (i.e. - you and your friends if included) select the most recent tweets
                .SelectMany(x => CachedRepository<Tweet>.Instance(PrimaryUser.TwitterScreenName).Query(x + TWEETS, where: where) ?? new List<Tweet>())
                //Order all tweets based on rank
                .OrderByDescending(t => t.TweetRank)
                .Distinct(Tweet.GetTweetTextComparer())
                .ToList();

            if (!string.IsNullOrEmpty(PrimaryUser.Track))
                tweets.AddRange(CachedRepository<Tweet>.Instance(PrimaryUser.TwitterScreenName).Query(TRACKER, pageSize: 1000, where: where) ?? new List<Tweet>());

            return tweets.Cast<ITweet>().ToList();
        }

        public List<string> GetRelevantScreenNames(string screenname)
        {
            var screenNames = new List<string> { screenname.ToLower() };

            if (UsersCollection.Single(screenname) != null && UsersCollection.Single(screenname).IncludeFriends)
                screenNames.AddRange((Friends(screenname) ?? new List<Tweep>()).Where(f => f.Type != Tweep.TweepType.Follower).Select(f => f.User.ScreenName).Where(f => f != null).Select(f => f.ToLower()));

            return screenNames;
        }

        public List<Tweep> Friends(string screenname)
        {
            var friends = CachedRepository<Tweep>.Instance(PrimaryUser.TwitterScreenName).Query(screenname + FRIENDS, pageSize: 1000).ToList();
            /*
            if (friends == null)
            {
                LoadFriendCache();
                friends = CachedRepository<Tweep>.Instance.Find(screenname + FRIENDS);
            }
             * */
            return friends ?? new List<Tweep>();
        }

        public Tweep CreateFriendship(Tweep follow, string screenname = null)
        {
            if (string.IsNullOrEmpty(screenname)) screenname = PrimaryUser.TwitterScreenName;

            var createFriendshipTask = GetAuthorizedTwitterContext(screenname).CreateFriendshipAsync(follow.User.UserID, true);

            createFriendshipTask.Wait();

            var user = createFriendshipTask.Result;

            return new Tweep(user, user.Following ? Tweep.TweepType.Following : Tweep.TweepType.None);
        }

        public void UpdateStatus(string statusText, string screenname = null, bool processStatus = true)
        {
            if (string.IsNullOrEmpty(screenname)) screenname = PrimaryUser.TwitterScreenName;

            var tweetTask = GetAuthorizedTwitterContext(screenname).TweetAsync(statusText);

            tweetTask.Wait();

            var status = tweetTask.Result;

            if (processStatus)
            {
                status = TwitterModel.Instance(PrimaryUser.TwitterScreenName).GetAuthorizedTwitterContext(screenname)
                                .Status
                                .Where(s => s.StatusID == status.StatusID && s.ScreenName == screenname && s.IncludeEntities == true && s.Type == StatusType.User && s.Count == 1)
                                .ToList().FirstOrDefault();

                if (status != null)
                {
                    var tweet = new Tweet(status);
                    tweet.PopulateExtendedData();
                    CachedRepository<Tweet>.Instance(PrimaryUser.TwitterScreenName).Save(screenname + TWEETS, tweet);
                }
            }
        }

        public void Retweet(ulong statusId, string screenname = null)
        {
            if (string.IsNullOrEmpty(screenname)) screenname = PrimaryUser.TwitterScreenName;

            var retweetTask = GetAuthorizedTwitterContext(screenname).RetweetAsync(statusId);

            retweetTask.Wait();

            var status = retweetTask.Result;
        }

        public void UpdateFriendsForPrimaryUser()
        {
            string screenname = PrimaryUser.TwitterScreenName;

            var user = UsersCollection.Single(screenname);
            if (user != null && user.CanAuthorize)
            {
                //try
                //{
                var friends = GetFollowers(screenname);

                if (friends != null && CachedRepository<Tweep>.Instance(PrimaryUser.TwitterScreenName).ContainsKey(screenname + FRIENDS))
                {
                    //var repoFriends = CachedRepository<Tweep>.Instance.Query(screenname + FRIENDS);
                    //friends = friends.Except(repoFriends).ToList();
                }

                if (friends != null)
                {
                    CachedRepository<Tweep>.Instance(PrimaryUser.TwitterScreenName).Save(screenname + FRIENDS, friends);
                    //CachedRepository<Tweep>.Instance.FlushChanges();
                }
                //}
                //catch { }
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
                .Select(x => x.GetAttributeValue("data-user-id", ""))
                .Distinct()
                .Select(x => GetLazyLoadedTweep(ulong.Parse(x), Tweep.TweepType.Suggested));

            return tweeps.Take(50).Select(x => x.Value).OrderByDescending(x => x.Clout()).ToList();
        }

        public List<LazyLoader<Tweep>> GetFollowersWithLazyLoading(string screenname)
        {
            var context = TwitterModel.Instance(PrimaryUser.TwitterScreenName).GetAuthorizedTwitterContext(PrimaryUser.TwitterScreenName);

            //try
            //{
            var friends = context
                .Friendship
                .Where(g => g.ScreenName == screenname && g.Type == FriendshipType.FollowersList && g.Cursor == -1)
                .SelectMany(g => g.Users)
                .Select(s => new LazyLoader<Tweep>(ulong.Parse(s.UserIDResponse),
                    (() => new Tweep(s, Tweep.TweepType.Follower))))
                .ToList();

            friends.AddRange(context
                .Friendship
                .Where(g => g.ScreenName == screenname && g.Type == FriendshipType.FriendsList && g.Cursor == -1)
                .SelectMany(g => g.Users)
                .Select(s => new LazyLoader<Tweep>(ulong.Parse(s.UserIDResponse),
                    (() => new Tweep(s, Tweep.TweepType.Following))))
                .Where(x => !friends.Select(y => y.ID).Contains(x.ID)));


            return friends;
            //}
            //catch { return null; }
        }

        public LazyLoader<Tweep> GetLazyLoadedTweep(ulong userID, Tweep.TweepType tweepType = Tweep.TweepType.None)
        {
            var context = TwitterModel.Instance(PrimaryUser.TwitterScreenName).GetAuthorizedTwitterContext(PrimaryUser.TwitterScreenName);

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
                    CredentialStore = new LinqToTwitter.InMemoryCredentialStore()
                    {
                        OAuthTokenSecret = pm.AccessToken,
                        ConsumerKey = ConfigurationManager.AppSettings["TwitterCustomerKey"],
                        ConsumerSecret = ConfigurationManager.AppSettings["TwitterCustomerSecret"],
                        OAuthToken = pm.OAuthToken
                    }
                });
            }
            else
                throw new Exception("Can Not Authorize User: " + screenname);

        }
    }
}