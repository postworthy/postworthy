using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Postworthy.Models.Core;
using Postworthy.Models.Twitter;

namespace Postworthy.Models.Streaming
{
    public interface ITweepProcessingStep
    {
        Task<IEnumerable<LazyLoader<Tweep>>> ProcessTweeps(IEnumerable<LazyLoader<Tweep>> tweeps);
    }
}
