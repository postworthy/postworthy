using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LinqToTwitter;
using System.Configuration;
using Postworthy.Models.Repository;
using Postworthy.Models.Account;
using System.Linq.Expressions;
using Postworthy.Models.Twitter;

namespace Postworthy.Tasks.Update.Models
{
    public static class StatusTimeline
    {
        public static List<Tweet> Get()
        {
            return Get(UsersCollection.PrimaryUser().TwitterScreenName, 0);

        }

        public static List<Tweet> Get(string screenname, ulong maxStatusID)
        {

            int fetchMultiplier = int.Parse(
                !string.IsNullOrEmpty(ConfigurationManager.AppSettings["FetchMultiplier"]) ?
                    ConfigurationManager.AppSettings["FetchMultiplier"] : "10");

            List<string> screenNames = new List<string>();
            screenNames.AddRange(TwitterModel.Instance.GetRelevantScreenNames(screenname));

            List<Tweet> tweets = new List<Tweet>();
            screenNames.ForEach(name =>
            {
                var t = Repository<Tweet>.Instance.Query(name + TwitterModel.TWEETS);
                if (t != null) tweets.AddRange(t);
            });
            if (tweets != null)
                tweets = tweets.OrderByDescending(t => t.Status.CreatedAt).ToList();
            if (tweets == null ||
                tweets.Count() < 5 ||
                !tweets.Select(t => t.Status.CreatedAt).IsWithinAverageRecurrenceInterval(multiplier: fetchMultiplier))
            {

                var lastStatusID = (tweets != null && tweets.Count() > 0) ? ulong.Parse(tweets.First().Status.StatusID) : 0;
                var user = UsersCollection.Single(screenname) ?? UsersCollection.PrimaryUser();
                if (user.CanAuthorize)
                {
                    try
                    {
                        Expression<Func<Status, bool>> where;
                        if (maxStatusID > 0 && lastStatusID > 0)
                            where = (s => s.MaxID == maxStatusID &&
                                s.SinceID == lastStatusID &&
                                s.ScreenName == screenname &&
                                s.IncludeEntities == true &&
                                s.Type == StatusType.User &&
                                s.Count == 50);
                        else if (lastStatusID > 0)
                            where = (s => s.SinceID == lastStatusID &&
                                s.ScreenName == screenname &&
                                s.IncludeEntities == true &&
                                s.Type == StatusType.Home &&
                                s.Count == 200);
                        else
                            where = (s => s.ScreenName == screenname &&
                                s.IncludeEntities == true &&
                                s.Type == StatusType.Home &&
                                s.Count == 200);

                        var statuses = TwitterModel.Instance.GetAuthorizedTwitterContext(user.TwitterScreenName)
                            .Status
                            .Where(where)
                            .ToList();

                        List<Tweet> results;

                        if (statuses != null && statuses.Count > 0)
                            results = statuses.Select(s => new Tweet(s)).ToList();
                        else
                            results = null;

                        return results;
                    }
                    catch { return null; }
                }
            }
            return null;
        }
    }
}
