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
    public static class UsersCollection
    {
        private static readonly object locker = new object();

        public static List<PostworthyUser> UsersWithPrimaryDomains()
        {
            return All().Where(x => x.PrimaryDomains != null && x.PrimaryDomains.Count > 0).ToList();
        }

        public static List<PostworthyUser> PrimaryUsers()
        {
            return All().Where(x => x.IsPrimaryUser || (x.PrimaryDomains != null && x.PrimaryDomains.Count > 0)).ToList();
        }

        public static PostworthyUser Single(string ScreenName, bool force = false, bool addIfNotFound = false)
        {
            return All(force)
                .SingleOrDefault(x => x.TwitterScreenName.ToLower() == ScreenName.ToLower()) ?? 
                ((addIfNotFound) ? Add(ScreenName) : null);
        }

        public static List<PostworthyUser> All(bool force = false)
        {
            if (force) HttpRuntime.Cache.Remove("PostworthyUsers");
            var model = HttpRuntime.Cache["PostworthyUsers"] as List<PostworthyUser>;
            if (model == null || model.Count > 0)
            {
                lock (locker)
                {
                    model = HttpRuntime.Cache["PostworthyUsers"] as List<PostworthyUser>;
                    if (model == null)
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
