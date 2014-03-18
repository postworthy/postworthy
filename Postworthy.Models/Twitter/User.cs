using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Models.Twitter
{
    public class User
    {
        public ulong UserID { get; set; }
        public string ScreenName { get; set; }
        public string Name { get; set; }
        public bool Following { get; set; }
        public int FollowersCount { get; set; }
        public string Url { get; set; }
        public string Description { get; set; }
        public string LangResponse { get; set; }
        public int FriendsCount { get; set; }
        public string ProfileImageUrl { get; set; }
        public User() 
        {
            
        }
        public User(LinqToTwitter.User user)
        {
            UserID = ulong.Parse(user.UserIDResponse);
            ScreenName = user.ScreenNameResponse;
            Name = user.Name;
            Following = user.Following;
            FollowersCount = user.FollowersCount;
            Url = user.Url;
            Description = user.Description;
            LangResponse = user.LangResponse;
            FriendsCount = user.FriendsCount;
            ProfileImageUrl = user.ProfileImageUrl;
        }
    }
}
