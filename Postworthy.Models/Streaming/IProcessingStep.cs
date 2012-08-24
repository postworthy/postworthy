using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Twitter;
using System.Threading.Tasks;

namespace Postworthy.Models.Streaming
{
    public interface IProcessingStep
    {
        Task<IEnumerable<Tweet>> ProcessItems(IEnumerable<Tweet> tweets);
    }
}
