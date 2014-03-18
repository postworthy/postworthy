using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Twitter;
using System.Threading.Tasks;
using System.IO;

namespace Postworthy.Models.Streaming
{
    public interface IProcessingStep
    {
        void Init(string screenname, TextWriter LogStream);
        Task<IEnumerable<Tweet>> ProcessItems(IEnumerable<Tweet> tweets);
        void Shutdown();
    }
}
