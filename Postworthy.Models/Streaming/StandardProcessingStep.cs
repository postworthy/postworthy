using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Postworthy.Models.Twitter;
using Postworthy.Models.Repository;
using System.IO;
using System.Configuration;
using Postworthy.Models.Account;
using Microsoft.AspNet.SignalR.Client;

namespace Postworthy.Models.Streaming
{
    public class StandardProcessingStep : IProcessingStep, ITweepProcessingStep
    {
        protected static List<Tweet> queue_push = new List<Tweet>();
        protected TextWriter log;
        protected string secret;
        protected string screenName;

        public virtual void Init(string screenname, TextWriter log)
        {
            screenName = screenname;
            this.log = log;
            secret = ConfigurationManager.AppSettings["TwitterCustomerSecret"];
        }

        public virtual Task<IEnumerable<Tweet>> ProcessItems(IEnumerable<Tweet> tweets)
        {
            return Task<IEnumerable<Tweet>>.Factory.StartNew(new Func<IEnumerable<Tweet>>(() =>
            {
                var tp = new TweetProcessor(tweets, 0, true);
                tp.Start();

                StoreInRepository(tweets);

                return tweets;
            }));
        }

        protected virtual void StoreInRepository(IEnumerable<Tweet> tweets)
        {
            tweets
                .GroupBy(t => t.User.ScreenName)
                .ToList()
                .ForEach(g =>
                {
                    CachedRepository<Tweet>.Instance(screenName).Save(g.Key + TwitterModel.Instance(screenName).TWEETS, g.OrderBy(t => t.CreatedAt).Select(t => t).ToList());
                    log.WriteLine("{0}: {1} Tweets Saved for {2}", DateTime.Now, g.Count(), g.Key);
                });
        }

        protected virtual void StoreInRepository(IEnumerable<Tweep> tweeps)
        {
            CachedRepository<Tweep>.Instance(screenName).Save(screenName + TwitterModel.Instance(screenName).FRIENDS, tweeps);
            log.WriteLine("{0}: {1} Freinds Added ({3}) for {2}", DateTime.Now, tweeps.Count(), screenName, string.Join(",", tweeps.Select(x => x.ScreenName)));
        }

        protected void RemoveOldTweeps()
        {
            CachedRepository<Tweep>.Instance(screenName).Delete(screenName + TwitterModel.Instance(screenName).FRIENDS);
            log.WriteLine("{0}: Removed Old Friends for {1}", DateTime.Now, screenName);
        }

        public void Shutdown()
        {

        }

        public Task<IEnumerable<Core.LazyLoader<Tweep>>> ProcessTweeps(IEnumerable<Core.LazyLoader<Tweep>> tweeps)
        {
            return Task<IEnumerable<Core.LazyLoader<Tweep>>>.Factory.StartNew(new Func<IEnumerable<Core.LazyLoader<Tweep>>>(() =>
            {
                RemoveOldTweeps();
                StoreInRepository(tweeps.Select(x => x.Value).ToList());
                return tweeps;
            }));
        }
    }
}
