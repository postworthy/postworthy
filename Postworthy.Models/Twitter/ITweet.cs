using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LinqToTwitter;
using Postworthy.Models.Core;

namespace Postworthy.Models.Twitter
{
    public interface ITweet
    {
        ulong StatusID { get; }
        List<UriEx> Links { get; }
        string TweetText { get; }
        int RetweetCount { get; }
        string TweetTime { get; }
        DateTime CreatedAt { get; }
        double TweetRank { get; }
        User User { get; }
    }
}
