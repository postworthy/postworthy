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
using System.IO;

namespace Postworthy.Tasks.Grouping
{
    class Program
    {
        private const string TWEETS = "_tweets";
        private const string GROUPING = "GROUPING_RESULTS";

        static void Main(string[] args)
        {
            if (!EnsureSingleLoad())
            {
                Console.WriteLine("{0}: Another Instance Currently Runing", DateTime.Now);
                return;
            }

            var start = DateTime.Now;
            Console.WriteLine("{0}: Started", start);

            Console.WriteLine("{0}: Deleting old groups from files from storage", DateTime.Now);
            Repository<TweetGroup>.Instance.Delete(GROUPING);
            /*
            var fileNames = Directory.GetFiles(FileUtility.GetPath("tweetgroup_*.json"));
            foreach (var fn in fileNames)
            {
                File.Delete(fn);
            }
             * */

            List<string> screenNames = null;

            var user = UsersCollection.PrimaryUser();

            screenNames = TwitterModel.Instance.GetRelevantScreenNames(user.TwitterScreenName);

            int RetweetThreshold = UsersCollection.PrimaryUser().RetweetThreshold;

            Expression<Func<Tweet, bool>> where = t =>
                //Should everything be displayed or do you only want content
                (user.OnlyTweetsWithLinks == false || (t.Links != null && t.Links.Count > 0)) &&
                    //Minumum threshold applied so we get results worth seeing (if it is your own tweet it gets a pass on this step)
                ((t.RetweetCount > RetweetThreshold /*&& t.CreatedAt > DateTime.Now.AddHours(-48)*/) || t.User.Identifier.ScreenName.ToLower() == user.TwitterScreenName.ToLower());

            var startGrouping = DateTime.Now;
            Console.WriteLine("{0}: Starting grouping procedure", startGrouping);

            var tweets = screenNames
                //For each screen name (i.e. - you and your friends if included) select the most recent tweets
                .SelectMany(x => Repository<Tweet>.Instance.Query(x + TWEETS, limit: Repository<Tweet>.Limit.Limit100, where: where) ?? new List<Tweet>())
                //Order all tweets based on rank
                .OrderByDescending(t => t.TweetRank);

            var groups = tweets
                //Group similar tweets (the ordering is done first so that the earliest tweet gets credit)
                .GroupSimilar()
                //Convert groups into something we can display
                .Select(g => new TweetGroup(g) { RepositoryKey = GROUPING })
                //For the sake of space we only want to store groups that have more than 1 item
                .Where(g=>g.GroupStatusIDs.Count > 1);

            var results = groups.ToList();

            var endGrouping = DateTime.Now;
            Console.WriteLine("{0}: Grouping procedure completed and it took {1} minutes to complete", endGrouping, (endGrouping - startGrouping).TotalMinutes);

            Console.WriteLine("Storing data in repository");
            if (results != null && results.Count > 0)
            {
                Repository<TweetGroup>.Instance.Save(GROUPING, results);
            }

            var end = DateTime.Now;
            Console.WriteLine("{0}: Ending and it took {1} minutes to complete", end, (end - start).TotalMinutes);
        }

        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.Grouping", out result);

            return result;
        }
    }
}
