using Postworthy.Models.Twitter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Models.Streaming
{
    public class PassThroughProcessingStep : IProcessingStep
    {
        public void Init(string screenname, System.IO.TextWriter LogStream)
        {
            
        }

        public Task<IEnumerable<Tweet>> ProcessItems(IEnumerable<Tweet> tweets)
        {
            return Task<IEnumerable<Tweet>>.Factory.StartNew(new Func<IEnumerable<Tweet>>(() =>
                {
                    return tweets;
                }));
        }

        public void Shutdown()
        { 

        }
    }
}
