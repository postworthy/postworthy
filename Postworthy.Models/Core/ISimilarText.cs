using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Postworthy.Models.Core
{
    public interface ISimilarText
    {
        int[] WordLetterPairHash { get; }
    }
}
