using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Postworthy.Models.Twitter
{
    public static class ITweetExtensions
    {
        public static IEnumerable<ITweet> OrderByTweetRank(this IEnumerable<ITweet> tweets)
        {
            return tweets.GroupBy(t => t.User.Identifier.ScreenName)
                .SelectMany(tg => tg.OrderByDescending(t => t.TweetText).Select((t, i) => new { WeightedTweetRank = Math.Exp(-i / 25) * t.TweetRank, Tweet = t }))
                .OrderByDescending(x => x.WeightedTweetRank)
                .Select(x => x.Tweet);
        }
    }
}
