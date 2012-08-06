using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Postworthy.Models.Core;
using Postworthy.Models.Twitter;

namespace Postworthy.Web.Models
{
    public class ItemData
    {
        public ITweet Model { get; set; }
        public int index { get; set; }
        public bool isTop10 { get; set; }
        public bool isTop20 { get; set; }
        public bool isTop30 { get; set; }
        public UriEx randomImage { get; set; }
        public bool hasVideo { get; set; }
        public int retweetThreshold { get; set; }
        public string topN { get; set; }
    }
}