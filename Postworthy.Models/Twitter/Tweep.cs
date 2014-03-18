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
            Suggested,
            IgnoreAlways
        }

        public User User { get; set; }
        public TweepType Type { get; set; }

        public string ScreenName { get { return User.ScreenName; } }

        public Tweep() { }

        public Tweep(LinqToTwitter.User user, TweepType type)
        {
            User = new User(user);
            if (type == TweepType.Follower && user.Following)
                Type = TweepType.Mutual;
            else
                Type = type;
        }

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
            var model = TwitterModel.Instance(null);
            var context = model.GetAuthorizedTwitterContext(model.PrimaryUser.TwitterScreenName);
            var tempUser = context.User.Where(x => x.ScreenName == postworthyUser.TwitterScreenName && x.Type == UserType.Lookup).ToList().FirstOrDefault();
            if (tempUser != null)
                User = new User(tempUser);
            else 
                throw new Exception("Could not Find Twitter User!");
            
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
                return this.User.UserID == otherTweep.User.UserID;
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            return this.UniqueKey.GetHashCode();
        }

        public override string UniqueKey
        {
            get
            {
                return "tweep_" + this.User.UserID;
            }
        }
        #endregion

        private List<LazyLoader<Tweep>> _Followers = null;
        public List<LazyLoader<Tweep>> Followers(bool forceRefresh = false)
        {
            if (_Followers == null || forceRefresh)
                _Followers = TwitterModel.Instance(null).GetFollowersWithLazyLoading(User.ScreenName) ?? new List<LazyLoader<Tweep>>();

            return _Followers;
        }
        public void OverrideFollowers(List<LazyLoader<Tweep>> tweeps)
        {
            _Followers = tweeps;
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
            return this.User.ScreenName.PadRight(15) + "\t" + Enum.GetName(typeof(TweepType), this.Type).PadRight(10) + "\t" + this.User.FollowersCount.ToString().PadLeft(10, '0');
        }
    }
}
