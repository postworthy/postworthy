using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Twitter;

namespace Postworthy.Models.Streaming
{
    public class StreamItem
    {
        public string Secret { get; set; }
        public IEnumerable<Tweet> Data { get; set; }
    }
}
