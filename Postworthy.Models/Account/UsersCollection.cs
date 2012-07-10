using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.IO;
using System.Xml.Serialization;
using System.Configuration;

namespace Postworthy.Models.Account
{
    public class UsersCollection
    {
        private static readonly object locker = new object();

        public static PostworthyUser PrimaryUser()
        {
            return Single(ConfigurationManager.AppSettings["PrimaryUser"]);
        }

        public static PostworthyUser Single(string ScreenName, bool force = false, bool addIfNotFound = false)
        {
            return All(force).SingleOrDefault(x => x.TwitterScreenName.ToLower() == ScreenName.ToLower()) ?? ((addIfNotFound) ? Add(ScreenName) : null);
        }

        public static List<PostworthyUser> All(bool force = false)
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
                var serializer = new XmlSerializer(typeof(List<PostworthyUser>));
                using (TextWriter writer = new StreamWriter(ConfigurationManager.AppSettings["UsersCollection"]))
                {
                    var model = HttpRuntime.Cache["PostworthyUsers"] as List<PostworthyUser>;
                    serializer.Serialize(writer, model);
                    writer.Flush();
                    writer.Close();
                }
            }
        }
    }
}
