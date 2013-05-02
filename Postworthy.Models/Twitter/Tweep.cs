using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Repository;
using LinqToTwitter;
using Postworthy.Models.Account;
using Postworthy.Models.Core;

namespace Postworthy.Models.Twitter
{
    public class Tweep : RepositoryEntity, IEquatable<Tweep>
    {
        public enum TweepType
        {
            Mutual,
            Follower,
            Following,
            Target,
            Ignore,
            None,
        }

        private User _User;
        private TweepType _Type;

        public User User { get { return _User; } set { SetNotifyingProperty("User", ref _User, value); } }
        public TweepType Type { get { return _Type; } set { SetNotifyingProperty("Type", ref _Type, value); } }

        public Tweep() { }

        public Tweep(User user, TweepType type)
        {
            User = user;
            if (type == TweepType.Follower && user.Following)
                Type = TweepType.Mutual;
            else
                Type = type;
        }

        public Tweep(PostworthyUser postworthyUser, TweepType type)
        {
            var context = TwitterModel.Instance.GetAuthorizedTwitterContext(UsersCollection.PrimaryUser().TwitterScreenName);
            User = context.User.Where(x => x.ScreenName == postworthyUser.TwitterScreenName && x.Type == UserType.Lookup).ToList().FirstOrDefault();
            if (User == null) throw new Exception("Could not Find Twitter User!");
            if (type == TweepType.Follower && User.Following)
                Type = TweepType.Mutual;
            else
                Type = type;
        }

        #region RepositoryEntity Members
        public override bool IsEqual(RepositoryEntity other)
        {
            if (other is Tweep)
            {
                var otherTweep = other as Tweep;
                return this.User.Identifier.UserID == otherTweep.User.Identifier.UserID;
            }
            else
                return false;
        }

        public override string UniqueKey
        {
            get
            {
                return "tweep_" + this.User.Identifier.UserID;
            }
        }
        #endregion

        private List<LazyLoader<Tweep>> _Followers = null;
        public List<LazyLoader<Tweep>> Followers(bool forceRefresh = false)
        {
            if (_Followers == null || forceRefresh)
                _Followers = Friends.GetFollowersWithLazyLoading(User.Identifier.ScreenName) ?? new List<LazyLoader<Tweep>>();

            return _Followers;
        }

        public int Clout()
        {
            var clout = this.User.FollowersCount;

            return clout;
        }

        #region IEquatable<Tweep> Members

        public bool Equals(Tweep other)
        {
            return IsEqual(other);
        }

        #endregion

        public override string ToString()
        {
            return this.User.Identifier.ScreenName + " (" + Enum.GetName(typeof(TweepType), this.Type) + ") (" + this.User.FollowersCount + ")";
        }
    }
}
