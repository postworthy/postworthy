using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Account;
using Postworthy.Models.Twitter;
using LinqToTwitter;
using System.Configuration;
using System.Timers;
using Postworthy.Models.Repository;

namespace Postworthy.Tasks.Streaming
{
    class Program
    {
        private const string TWEETS = "_tweets";
        private static object queue_lock = new object();
        private static List<Tweet> queue = new List<Tweet>();
        private static Tweet[] tweets;
        static void Main(string[] args)
        {
            Console.WriteLine("{0}: Listening to Stream", DateTime.Now);
            var screenname = UsersCollection.PrimaryUser().TwitterScreenName;
            
            var context = TwitterModel.Instance.GetAuthorizedTwitterContext(screenname);
            
            var stream = context
                .UserStream
                .Where(s=>s.Type == LinqToTwitter.UserStreamType.User)
                .Select(strm=>strm)
                .StreamingCallback(strm=>
                {
                    try
                    {
                        var status = new Status(LitJson.JsonMapper.ToObject(strm.Content));
                        if (status != null && !string.IsNullOrEmpty(status.StatusID))
                        {
                            var tweet = new Tweet(string.IsNullOrEmpty(status.RetweetedStatus.StatusID) ? status : status.RetweetedStatus);
                            lock (queue_lock)
                            {
                                queue.Add(tweet);
                            }
                            Console.WriteLine("{0}: Added Item to Queue: {1}", DateTime.Now, tweet.TweetText);
                        }
                    }
                    catch { }
                }).SingleOrDefault();

            var timer = new Timer(60000);
            timer.Elapsed += new ElapsedEventHandler((x, y) =>
                {
                    timer.Enabled = false;
                    try
                    {
                        lock (queue_lock)
                        {
                            if (queue.Count == 0) return;
                            tweets = new Tweet[queue.Count];
                            queue.CopyTo(tweets);
                            queue.Clear();
                        }

                        Console.WriteLine("{0}: Processing {1} Items from Queue", DateTime.Now, tweets.Length);

                        var tp = new TweetProcessor(tweets, true);
                        tp.Start();

                        tweets
                            .GroupBy(t => t.User.Identifier.ScreenName)
                            .ToList()
                            .ForEach(g =>
                        {
                            Repository<Tweet>.Instance.Save(g.Key + TWEETS, g.OrderBy(t => t.CreatedAt).Select(t => t).ToList());
                            Console.WriteLine("{0}: {1} Tweets Saved for {2}", DateTime.Now, g.Count(), g.Key);
                        });

                        Repository<Tweet>.Instance.FlushChanges();

                        tweets = null;

                        Console.WriteLine("{0}: Completed Processing Queue", DateTime.Now);
                    }
                    catch { }
                    finally
                    {
                        timer.Enabled = true;
                    }
                });
            timer.Start();
            Console.ReadLine();
        }
    }
}
