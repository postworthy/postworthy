using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models;
using LinqToTwitter;
using System.IO;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Diagnostics;
using Postworthy.Models.Repository;
using Postworthy.Models.Account;
using Postworthy.Models.Twitter;
using Postworthy.Models.Core;
using System.Configuration;

namespace UpdateRepository
{
    class Program
    {
        private const string TWEETS = "_tweets";
        private const string FRIENDS = "_friends";

        private class BetweenStatuses
        {
            public ulong MinStatusID { get; set; }
            public ulong MaxStatusID { get; set; }

            public BetweenStatuses(ulong min, ulong max)
            {
                MinStatusID = min;
                MaxStatusID = max;
            }
        }

        static void Main(string[] args)
        {
            try
            {
                using (var lockFile = File.Open(Path.GetTempPath() + "/UpdateRepository.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    var start = DateTime.Now;

                    Console.WriteLine("{0}: Started", start);

                    var primaryUser = UsersCollection.PrimaryUser();

                    List<Tweet> RecentTweets = new List<Tweet>();
                    List<Tweet> tweets;
                    List<Tweep> tweeps;

                    List<string> screenNames = new List<string>();

                    Console.WriteLine("{0}: Getting Friends for {1}", DateTime.Now, primaryUser);
                    tweeps = GetFriends(primaryUser.TwitterScreenName);

                    if (tweeps != null)
                        Repository<Tweep>.Instance.Save(primaryUser.TwitterScreenName + FRIENDS, tweeps);

                    screenNames.AddRange(TwitterModel.Instance.GetRelevantScreenNames(primaryUser.TwitterScreenName));

                    foreach (var screenName in screenNames)
                    {
                        Console.WriteLine("{0}: Getting Tweets for {1}", DateTime.Now, screenName);
                        var statuses = GetStatuses(screenName);

                        if (statuses != null && statuses.Count > 0)
                            tweets = statuses.Select(s => new Tweet(s)).ToList();
                        else
                            tweets = null;

                        if (tweets != null)
                        {
                            RecentTweets.AddRange(tweets);
                            Console.WriteLine("{0}: {1} Tweets Added for {2}", DateTime.Now, tweets.Count, screenName);
                            //Repository<Tweet>.Instance.Save(screenName + TWEETS, tweets);
                        }
                    }

                    Console.WriteLine("{0}: Processing {1} Tweets", DateTime.Now, RecentTweets.Count);

                    TweetProcessor tp = new TweetProcessor(RecentTweets);

                    tp.Start();

                    Console.WriteLine("{0}: Saving Tweets", DateTime.Now);
                    RecentTweets
                        .GroupBy(t => t.Status.ScreenName)
                        .ToList()
                        .ForEach(g =>
                        {
                            Repository<Tweet>.Instance.Save(g.Key + TWEETS, g.Select(x => x).ToList());
                        });

                    Repository<Tweep>.Instance.FlushChanges();
                    Repository<Tweet>.Instance.FlushChanges();

                    Console.WriteLine("{0}: Update Retweet Counts", DateTime.Now);
                    List<Tweet> updateTweets = new List<Tweet>();
                    foreach (var screenName in screenNames)
                    {
                        var tweetsToUpdate = Repository<Tweet>.Instance.Query(screenName + TWEETS, where: t => t.CreatedAt > DateTime.Now.AddHours(-48));
                        if (tweetsToUpdate != null && tweetsToUpdate.Count > 1)
                        {
                            tweetsToUpdate = tweetsToUpdate.Except(RecentTweets).OrderByDescending(t => t.Status.CreatedAt).ToList();
                            if (tweetsToUpdate != null && tweetsToUpdate.Count > 1)
                            {
                                Console.WriteLine("{0}: Updating Retweet Counts for {1}", DateTime.Now, screenName);
                                var updatedStatuses = GetStatuses(screenName, new BetweenStatuses(ulong.Parse(tweetsToUpdate.Last().Status.StatusID), ulong.Parse(tweetsToUpdate.First().Status.StatusID)));
                                if (updatedStatuses != null && updatedStatuses.Count > 0)
                                {
                                    int tweetsAdded = 0;
                                    foreach (var s in updatedStatuses)
                                    {
                                        var t = tweetsToUpdate.SingleOrDefault(x => x.Status.StatusID == s.StatusID);
                                        if (t != null && t.Status.RetweetCount != s.RetweetCount)
                                        {
                                            t.Status.RetweetCount = s.RetweetCount;
                                            ///TODO: This is a temporary addition and will be removed later
                                            var temp = t.WordLetterPairHash; //Forces an update of the WLPH if it has not been created already
                                            ///END
                                            updateTweets.Add(t);
                                            tweetsAdded++;
                                        }
                                    }
                                    if(tweetsAdded > 0) Console.WriteLine("{0}: {1} Retweet Counts Updated for {2}", DateTime.Now, tweetsAdded, screenName);
                                }
                            }
                        }
                    }

                    if (updateTweets.Count > 0)
                    {
                        Console.WriteLine("{0}: Processing {1} Tweets with New Retweet Counts", DateTime.Now, updateTweets.Count);
                        tp = new TweetProcessor(updateTweets);
                        tp.Start();
                        Repository<Tweet>.Instance.FlushChanges();
                    }
                    var end = DateTime.Now;
                    Console.WriteLine("{0}: Finished in {1} minutes", end, (end - start).TotalMinutes);
                }
            }
            catch (System.IO.IOException ioex)
            {
                if (ioex.Message.ToLower().Contains("the process cannot access the file"))
                    Console.WriteLine("{0}: Process already running", DateTime.Now);
                else throw;
            }
        }

        private static List<Status> GetStatuses(string screenname, BetweenStatuses between = null)
        {
            var tweets = Repository<Tweet>.Instance.Query(screenname + TWEETS, where: t => t.CreatedAt > DateTime.Now.AddHours(-48));
            if (tweets != null) 
                tweets = tweets.OrderByDescending(t => t.Status.CreatedAt).ToList();
            if (between != null || tweets == null || tweets.Count() < 10 || !tweets.Select(t => t.Status.CreatedAt).IsWithinAverageDifference())
            {
                if (between == null)
                {
                    between = new BetweenStatuses(0, 0);
                    between.MinStatusID = (tweets != null && tweets.Count() > 0) ? ulong.Parse(tweets.First().Status.StatusID) : 0;
                }
                var user = UsersCollection.Single(screenname) ?? UsersCollection.Single("postworthy");
                if (user.CanAuthorize)
                {
                    try
                    {
                        Expression<Func<Status, bool>> where;
                            if(between.MaxStatusID > 0 && between.MinStatusID > 0)
                                where = (s => s.MaxID == between.MaxStatusID && s.SinceID == between.MinStatusID && s.ScreenName == screenname && s.IncludeEntities == true && s.Type == StatusType.User && s.Count == 50);
                            else if (between.MinStatusID > 0)
                                where = (s => s.SinceID == between.MinStatusID && s.ScreenName == screenname && s.IncludeEntities == true && s.Type == StatusType.User && s.Count == 50);
                            else
                                where = (s => s.ScreenName == screenname && s.IncludeEntities == true && s.Type == StatusType.User && s.Count == 10);

                        return TwitterModel.Instance.GetAuthorizedTwitterContext(user.TwitterScreenName)
                            .Status
                            .Where(where)
                            .ToList();
                    }
                    catch { return null; }
                }
            }
            return null;
        }

        private static List<Tweep> GetFriends(string screenname)
        {
            var user = UsersCollection.Single(screenname);
            if (user != null && user.CanAuthorize)
            {
                var context = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname);

                try
                {
                    var friends = context
                        .SocialGraph
                        .Where(g => g.ScreenName == screenname && g.Type == SocialGraphType.Followers && g.Cursor == "-1")
                        .SelectMany(g => g.IDs)
                        .Select(s => new Tweep(context.User.Where(u => u.Type == UserType.Show && u.UserID == s).First(), Tweep.TweepType.Follower))
                        .ToList();

                    friends.AddRange(context
                        .SocialGraph
                        .Where(g => g.ScreenName == screenname && g.Type == SocialGraphType.Friends && g.Cursor == "-1")
                        .SelectMany(g => g.IDs)
                        .Except(friends.Select(u => u.User.UserID))
                        .Select(s => new Tweep(context.User.Where(u => u.Type == UserType.Show && u.UserID == s).First(), Tweep.TweepType.Following)));

                    if (Repository<Tweep>.Instance.ContainsKey(screenname + FRIENDS))
                    {
                        var repoFriends = Repository<Tweep>.Instance.Query(screenname + FRIENDS);
                        friends = friends.Except(repoFriends).ToList();
                    }

                    return friends;
                }
                catch { return null; }
            }
            return null;
        }
    }
}
