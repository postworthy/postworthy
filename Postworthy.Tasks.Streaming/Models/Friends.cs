using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Twitter;
using Postworthy.Models.Account;
using Postworthy.Models.Repository;
using LinqToTwitter;

namespace Postworthy.Tasks.Streaming.Models
{
    public static class Friends
    {
        private const string FRIENDS = "_friends";

        public static void Update()
        {
            string screenname = UsersCollection.PrimaryUser().TwitterScreenName;

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

                    if (friends != null)
                    {
                        Repository<Tweep>.Instance.Save(screenname + FRIENDS, friends);
                        Repository<Tweep>.Instance.FlushChanges();
                    }
                }
                catch {  }
            }
        }
    }
}
