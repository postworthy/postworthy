using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Postworthy.Models.Account;
using Postworthy.Models.Twitter;
using Postworthy.Tasks.Update.Models;
using LinqToTwitter;
using System.Configuration;
using Postworthy.Models.Repository;
using System.Linq.Expressions;

namespace Postworthy.Tasks.Update
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!EnsureSingleLoad())
            {
                Console.WriteLine("{0}: Another Instance Currently Runing", DateTime.Now);
                return;
            }

            TweetProcessor tp;
            List<Tweet> tweets;

            var start = DateTime.Now;

            Console.WriteLine("{0}: Started", start);

            var primaryUser = UsersCollection.PrimaryUser();

            Console.WriteLine("{0}: Getting Friends for {1}", DateTime.Now, primaryUser.TwitterScreenName);
            Friends.Update();

            Console.WriteLine("{0}: Getting Tweets for {1}", DateTime.Now, primaryUser.TwitterScreenName);
            tweets = StatusTimeline.Get();

            if (tweets != null)
            {
                Console.WriteLine("{0}: Processing {1} Tweets", DateTime.Now, tweets.Count);

                tp = new TweetProcessor(tweets);
                tp.Start();

                Console.WriteLine("{0}: Saving Tweets", DateTime.Now);
                tweets
                    .GroupBy(t => t.User.Identifier.ScreenName)
                    .ToList()
                    .ForEach(g =>
                    {
                        Repository<Tweet>.Instance.Save(g.Key + TwitterModel.TWEETS, g.Select(x => x).ToList());
                        Console.WriteLine("{0}: {1} Tweets Saved for {2}", DateTime.Now, g.Count(), g.Key);
                    });

                Repository<Tweet>.Instance.FlushChanges();
            }
            else
                tweets = new List<Tweet>();

            List<string> screenNames = new List<string>();
            screenNames.AddRange(TwitterModel.Instance.GetRelevantScreenNames(primaryUser.TwitterScreenName));

            Console.WriteLine("{0}: Update Retweet Counts", DateTime.Now);
            List<Tweet> updateTweets = new List<Tweet>();

            foreach (var screenName in screenNames)
            {
                var tweetsToUpdate = Repository<Tweet>.Instance.Query(screenName + TwitterModel.TWEETS, where: t => t.CreatedAt > DateTime.Now.AddHours(-48));
                if (tweetsToUpdate != null && tweetsToUpdate.Count > 1)
                {
                    tweetsToUpdate = tweetsToUpdate.Except(tweets).OrderByDescending(t => t.Status.CreatedAt).ToList();
                    if (tweetsToUpdate != null && tweetsToUpdate.Count > 1)
                    {
                        Console.WriteLine("{0}: Updating Retweet Counts for {1}", DateTime.Now, screenName);
                        var updatedStatuses = StatusTimeline.Get(screenName, tweetsToUpdate.First().StatusID);
                        if (updatedStatuses != null && updatedStatuses.Count > 0)
                        {
                            int tweetsAdded = 0;
                            foreach (var s in updatedStatuses)
                            {
                                var t = tweetsToUpdate.SingleOrDefault(x => x.StatusID == s.StatusID);
                                if (t != null && t.RetweetCount != s.RetweetCount)
                                {
                                    t.Status.RetweetCount = s.RetweetCount;
                                    updateTweets.Add(t);
                                    tweetsAdded++;
                                }
                            }
                            if (tweetsAdded > 0) Console.WriteLine("{0}: {1} Retweet Counts Updated for {2}", DateTime.Now, tweetsAdded, screenName);
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
        
        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.Update." + UsersCollection.PrimaryUser().TwitterScreenName, out result);

            return result;
        }
    }
}
