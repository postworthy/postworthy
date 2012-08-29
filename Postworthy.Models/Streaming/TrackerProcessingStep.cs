using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Repository;
using Postworthy.Models.Twitter;

namespace Postworthy.Models.Streaming
{
    public class TrackerProcessingStep : StandardProcessingStep, IProcessingStep
    {
        protected override void StoreInRepository(IEnumerable<Tweet> tweets)
        {
            Repository<Tweet>.Instance.Save(TwitterModel.TRACKER + TwitterModel.TWEETS, tweets.OrderBy(t => t.CreatedAt).Select(t => t).ToList());
            log.WriteLine("{0}: {1} Tweets Saved for {2}", DateTime.Now, tweets.Count(), TwitterModel.TRACKER);

            Repository<Tweet>.Instance.FlushChanges();
        }
    }
}
