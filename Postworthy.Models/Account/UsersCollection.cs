using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.IO;
using System.Xml.Serialization;
using System.Configuration;
using Postworthy.Models.Repository;

namespace Postworthy.Models.Account
{
    public class UsersCollection
    {
        private static readonly object locker = new object();

        public static PostworthyUser PrimaryUser()
        {
            var userName = ConfigurationManager.AppSettings["PrimaryUser"];

            if(string.IsNullOrEmpty(userName) && HttpContext.Current != null)
                userName = HttpContext.Current.User.Identity.Name;

            if (!string.IsNullOrEmpty(userName))
                return Single(userName);
            else 
                return null;
        }

        public static PostworthyUser Single(string ScreenName, bool force = false, bool addIfNotFound = false)
        {
            return All(force).SingleOrDefault(x => x.TwitterScreenName.ToLower() == ScreenName.ToLower()) ?? ((addIfNotFound) ? Add(ScreenName) : null);
        }

        private static bool IsPullingFromRepo()
        {
            var path = ConfigurationManager.AppSettings["UsersCollection"] ?? "";
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                return false;
            else
                return true;
        }

        private static List<PostworthyUser> All(bool force = false)
        {
            if (force) HttpRuntime.Cache.Remove("PostworthyUsers");
            var model = HttpRuntime.Cache["PostworthyUsers"] as List<PostworthyUser>;
            if (model == null)
            {
                lock (locker)
                {
                    model = HttpRuntime.Cache["PostworthyUsers"] as List<PostworthyUser>;
                    if (model == null)
                    {
                        if (IsPullingFromRepo())
                            model = CachedRepository<PostworthyUser>.Instance.Query("UsersCollection", 0, 0).ToList();
                        else
                        {
                            var serializer = new XmlSerializer(typeof(List<PostworthyUser>));
                            using (var fs = new FileStream(ConfigurationManager.AppSettings["UsersCollection"], FileMode.Open))
                            {
                                try
                                {
                                    model = (List<PostworthyUser>)serializer.Deserialize(fs);
                                }
                                catch { }
                            }
                        }

                        HttpRuntime.Cache["PostworthyUsers"] = model;
                    }
                }
            }
            return model;
        }

        private static PostworthyUser Add(string ScreenName)
        {
            var users = HttpRuntime.Cache["PostworthyUsers"] as List<PostworthyUser>;
            var newUser = new PostworthyUser() { TwitterScreenName = ScreenName, SiteName = ScreenName };
            users.Add(newUser);
            return newUser;
        }

        public static void Save()
        {
            lock (locker)
            {
                var model = HttpRuntime.Cache["PostworthyUsers"] as List<PostworthyUser>;

                if (IsPullingFromRepo())
                {
                    CachedRepository<PostworthyUser>.Instance.Save("UsersCollection", model);
                }
                else
                {
                    var serializer = new XmlSerializer(typeof(List<PostworthyUser>));
                    using (TextWriter writer = new StreamWriter(ConfigurationManager.AppSettings["UsersCollection"]))
                    {
                        serializer.Serialize(writer, model);
                        writer.Flush();
                        writer.Close();
                    }
                }
            }
        }
    }
}
