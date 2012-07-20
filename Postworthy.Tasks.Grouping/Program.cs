using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Account;
using Postworthy.Models.Twitter;
using System.Linq.Expressions;
using Postworthy.Models.Repository;
using Postworthy.Models.Repository.Providers;
using Postworthy.Models.Core;

namespace Postworthy.Tasks.Grouping
{
    class Program
    {
        private const string TWEETS = "_tweets";
        private const string GROUPING = "GROUPING_RESULTS";

        static void Main(string[] args)
        {
            List<string> screenNames = null;

            var user = UsersCollection.PrimaryUser();

            screenNames = TwitterModel.Instance.GetRelevantScreenNames(user.TwitterScreenName);

            int RetweetThreshold = UsersCollection.PrimaryUser().RetweetThreshold;

            Expression<Func<Tweet, bool>> where = t =>
                //Should everything be displayed or do you only want content
                (user.OnlyTweetsWithLinks == false || (t.Links != null && t.Links.Count > 0)) &&
                    //Minumum threshold applied so we get results worth seeing (if it is your own tweet it gets a pass on this step)
                ((t.RetweetCount > RetweetThreshold /*&& t.CreatedAt > DateTime.Now.AddHours(-48)*/) || t.User.Identifier.ScreenName.ToLower() == user.TwitterScreenName.ToLower());

            var start = DateTime.Now;
            Console.WriteLine("Starting Grouping Procedure @ {0}", start);

            var tweets = screenNames
                //For each screen name (i.e. - you and your friends if included) select the most recent tweets
                .SelectMany(x => Repository<Tweet>.Instance.Query(x + TWEETS, limit: Repository<Tweet>.Limit.Limit100, where: where) ?? new List<Tweet>())
                //Order all tweets based on rank
                .OrderByDescending(t => t.TweetRank)
                //Group similar tweets (the ordering is done first so that the earliest tweet gets credit)
                .GroupSimilar()
                //Convert groups into something we can display
                .Select(g => new TweetGroup(g));

            var results = tweets.ToList();

            var end = DateTime.Now;
            Console.WriteLine("Grouping Procedure Completed @ {0} and took {1} minutes to complete", end, (end - start).TotalMinutes);

            Console.WriteLine("Storing Data in Distributed Shared Cache");
            if (results != null && results.Count > 0)
            {
                var shared = new DistributedSharedCache<RepositorySingleton<List<TweetGroup>>>();
                shared.Store(GROUPING, new RepositorySingleton<List<TweetGroup>>()
                    {
                        Key = GROUPING,
                        RepositoryKey = GROUPING,
                        Data = results
                    });
            }
        }
    }
}
