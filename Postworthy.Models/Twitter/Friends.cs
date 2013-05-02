using LinqToTwitter;
using Postworthy.Models.Account;
using Postworthy.Models.Core;
using Postworthy.Models.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Postworthy.Models.Twitter
{
    public class Friends
    {
        private const string FRIENDS = "_friends";

        public static void UpdateForPrimaryUser()
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

        public static List<Tweep> GetFollowers(string screenname)
        {
            var llf = GetFollowersWithLazyLoading(screenname);
            return llf.Select(x => x.Value).ToList();
        }

        public static List<LazyLoader<Tweep>> GetFollowersWithLazyLoading(string screenname)
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
    }
}
