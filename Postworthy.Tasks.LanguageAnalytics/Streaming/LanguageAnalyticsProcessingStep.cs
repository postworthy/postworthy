using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using Postworthy.Models.Twitter;
using Postworthy.Models.Account;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using Postworthy.Models.Repository;
using Postworthy.Models.Core;
using System.Runtime.ConstrainedExecution;
using Postworthy.Models.Streaming;
using System.Text.RegularExpressions;
using Postworthy.Tasks.LanguageAnalytics.Models;

namespace Postworthy.Tasks.LanguageAnalytics.Streaming
{
    public class LanguageAnalyticsProcessingStep : IProcessingStep, ITweepProcessingStep, IKeywordSuggestionStep
    {
        private const string RUNTIME_REPO_KEY = "LanguageAnalytics";
        public const int MINIMUM_KEYWORD_COUNT = 30;
        private const int MINIMUM_NEW_KEYWORD_LENGTH = 2;
        private const int MAX_KEYWORD_SUGGESTIONS = 400;
        private const int KEYWORD_FALLOUT_MINUTES = 15;
        private const int MAX_PHRASE_WORD_COUNT = 5;
        private int saveCount = 0;
        private List<string> NoTweetList = new List<string>();
        private TextWriter log = null;
        private Tweep PrimaryTweep;
        private IEnumerable<string> StopWords = null;
        private Regex PunctuationRegex = new Regex(@"(\p{P})|\t|\n|\r", RegexOptions.Compiled);
        private Regex WhiteSpaceRegex = new Regex(@"\s{2,}", RegexOptions.Compiled);
        private DateTime LastUpdatedTwitterSuggestedFollows = DateTime.MinValue;
        private LanguageAnalyticsResults LanguageAnalyticsResults = null;
        private CachedRepository<LanguageAnalyticsResults> settingsRepo;
        private List<WordNode> WordNodes = new List<WordNode>();
        private List<Phrase> Phrases = new List<Phrase>();

        private string RuntimeRepoKey
        {
            get
            {
                return PrimaryTweep.ScreenName + "_" + RUNTIME_REPO_KEY;
            }
        }

        public void Init(string screenname, TextWriter log)
        {
            this.log = log;

            PrimaryTweep = new Tweep(UsersCollection.Single(screenname), Tweep.TweepType.None);

            settingsRepo = CachedRepository<LanguageAnalyticsResults>.Instance(screenname);

            LanguageAnalyticsResults = (settingsRepo.Query(RuntimeRepoKey)
                ?? new List<LanguageAnalyticsResults> { new LanguageAnalyticsResults() }).FirstOrDefault()
                ?? new LanguageAnalyticsResults();

            NoTweetList.Add(screenname);
        }

        public Task<IEnumerable<Tweet>> ProcessItems(IEnumerable<Tweet> tweets)
        {
            return Task<IEnumerable<Tweet>>.Factory.StartNew(new Func<IEnumerable<Tweet>>(() =>
                {
                    LanguageAnalyticsResults.TotalTweetsProcessed += tweets.Count();

                    //FindKeywordsFromCurrentTweets(tweets);

                    ExtractWordRelationships(tweets);

                    DebugConsoleLog();

                    if (saveCount++ > 20)
                    {
                        SaveRuntimeSettings();
                        saveCount = 0;
                    }

                    return tweets;
                }));
        }

        public void Shutdown()
        {
            SaveRuntimeSettings();
        }

        private void SaveRuntimeSettings()
        {
            settingsRepo.Save(RuntimeRepoKey, LanguageAnalyticsResults);
        }

        private void DebugConsoleLog()
        {
            log.WriteLine("****************************");
            log.WriteLine("****************************");

            //log.WriteLine("{0}: Keywords: {1}",
            //    DateTime.Now,
            //    LanguageAnalyticsResults.Keywords.Count);

            //foreach (var k in LanguageAnalyticsResults.KeywordSuggestions)
            //{
            //    log.WriteLine("{0}: Keyword Suggestion: {1,10}\t{2}", DateTime.Now, k.Count, k.Key);
            //}

            var topWords = WordNodes.OrderByDescending(x => x.Count).Take(50);
            foreach (var wordNode in topWords)
            {
                var pre = wordNode.Pre.OrderByDescending(x => x.Count).Select(x => x.Key.Word).FirstOrDefault() ?? "N/A";
                var post = wordNode.Post.OrderByDescending(x => x.Count).Select(x => x.Key.Word).FirstOrDefault() ?? "N/A";
                log.WriteLine("{0}: Keyword: {1,5}\t{2}\t\t{3}\t\t{4}", DateTime.Now, wordNode.Count, wordNode.Word, pre + " " + wordNode.Word, wordNode.Word + " " + post);
            }

            var topPhrases = Phrases.OrderByDescending(x => x.Count).Take(50);
            foreach (var phrase in topPhrases)
            {
                log.WriteLine("{0}: Phrase: {1,5}\t{2}", DateTime.Now, phrase.Count, string.Join(" ", phrase.Words.Select(x => x.Word)));
            }

            log.WriteLine("****************************");
            log.WriteLine("****************************");
        }

        private void ExtractWordRelationships(IEnumerable<Tweet> tweets)
        {
            var cleanedTweets = CleanTweets(tweets);
            ExtractWordNodes(cleanedTweets);
            ConnectWordNodes(cleanedTweets);
            CreatePhrases();
            SearchForPhrases(cleanedTweets);
        }

        private IEnumerable<string[]> CleanTweets(IEnumerable<Tweet> tweets)
        {
            long nothing;
            var cleanedTweets = tweets.Select(t => WhiteSpaceRegex.Replace(PunctuationRegex.Replace(t.TweetText, " "), " ").ToLower()).ToList();
            return cleanedTweets.Select(t =>
                t.Split(' ')
                .Select(w => w.Trim())
                .Where(w => !long.TryParse(w, out nothing)) //Ignore Numbers
                .Where(x => x.Length >= MINIMUM_NEW_KEYWORD_LENGTH) //Must be Minimum Length
                .Where(x => !x.StartsWith("http")) //No URLs
                .Where(x => Encoding.UTF8.GetByteCount(x) == x.Length) //Only ASCII for me...
                .ToArray()
                );
        }

        private void ExtractWordNodes(IEnumerable<string[]> wordCollections)
        {
            var words = wordCollections.SelectMany(x => x);
            foreach (var word in words)
            {
                int hash = word.GetHashCode();
                var wordNode = WordNodes.Where(x => x.Hash == hash).FirstOrDefault() ?? new WordNode() { Word = word };
                wordNode.Count++;
                if (wordNode.Count == 1)
                    WordNodes.Add(wordNode);
            }
        }

        private void ConnectWordNodes(IEnumerable<string[]> wordCollections)
        {
            foreach (var wordCollection in wordCollections)
            {
                if (wordCollection.Length > 1)
                {
                    // wordCollection.Length - 1 --> Because by the time we reach the last node it has been assigned
                    for (int i = 0; i < wordCollection.Length - 1; i++)
                    {
                        int hash = wordCollection[i].GetHashCode();
                        int hashPost = wordCollection[i + 1].GetHashCode();
                        var pre = WordNodes.Where(x => x.Hash == hash).FirstOrDefault();
                        var post = WordNodes.Where(x => x.Hash == hashPost).FirstOrDefault();

                        var found = pre.Post.Where(x => x.Key == post).FirstOrDefault();

                        if (found == null)
                            pre.Post.Add(new CountableItem<WordNode>(post, 1));
                        else
                            found.Count++;

                        found = post.Pre.Where(x => x.Key == pre).FirstOrDefault();

                        if (found == null)
                            post.Pre.Add(new CountableItem<WordNode>(pre, 1));
                        else
                            found.Count++;
                    }
                }
            }
        }

        private void CreatePhrases()
        {
            var totalWords = WordNodes.Count;
            if (totalWords > 100 && Phrases.Count == 0)
            {
                int depth = MAX_PHRASE_WORD_COUNT - 1;
                var wordNodes = WordNodes.OrderByDescending(x => x.Count).Take(5); //Get Top 10%
                foreach (var wordNode in wordNodes)
                {
                    Phrases.AddRange(wordNode.Phrases(depth).Select(x => new Phrase() { Words = x }));
                }
            }
        }

        private void SearchForPhrases(IEnumerable<string[]> cleanedTweets)
        {
            //throw new NotImplementedException();
        }

        private void FindKeywordsFromCurrentTweets(IEnumerable<Tweet> tweets)
        {
            long nothing;
            //Strip Punctuation, Strip Stop Words, Force Lowercase
            var cleanedTweets = tweets.Select(t => WhiteSpaceRegex.Replace(PunctuationRegex.Replace(t.TweetText, " "), " ").ToLower()).ToList();


            //Get All Words Added by User (From Config & Dashboard)
            var manuallyAddedWords = LanguageAnalyticsResults.KeywordsToIgnore.Select(x => x.ToLower());

            //Get Current Keyword Counts
            var currentKeywords = manuallyAddedWords.Select(maw =>
                new
                {
                    Word = maw,
                    Count = tweets.Select(t => t.TweetText).Select(t => new Regex(maw).Matches(t).Count).Sum()
                }).ToList();

            //Update Master Keyword List
            foreach (var w in currentKeywords)
            {
                var item = LanguageAnalyticsResults.Keywords.Where(x => x.Key == w.Word).FirstOrDefault();
                if (item != null)
                    item.Count += w.Count;
                else
                    LanguageAnalyticsResults.Keywords.Add(new CountableItem(w.Word, w.Count));
            }

            //Only words we are tracking from config
            LanguageAnalyticsResults.Keywords = LanguageAnalyticsResults.Keywords.Where(w => manuallyAddedWords.Contains(w.Key)).ToList();

            var words = cleanedTweets
                .SelectMany(t => t.Split(' '))
                .Select(w => w.Trim())
                .Where(w => !long.TryParse(w, out nothing)) //Ignore Numbers
                .Where(x => x.Length >= MINIMUM_NEW_KEYWORD_LENGTH) //Must be Minimum Length
                .Except(LanguageAnalyticsResults.KeywordsToIgnore.SelectMany(y => y.Split(' ').Concat(new string[] { y }))) //Exclude Ignore Words, which are current keywords
                //.Except(StopWords) //Exclude Stop Words
                .Where(x => !x.StartsWith("http")) //No URLs
                .Where(x => Encoding.UTF8.GetByteCount(x) == x.Length) //Only ASCII for me...
                .ToList();

            //Create pairs of words for phrase searching
            var wordPairs = new List<CountableItem<string>>();
            Regex regexObj = new Regex(
                @"(     # Match and capture in backreference no. 1:
                 \w+    # one or more alphanumeric characters
                 \s+    # one or more whitespace characters.
                )       # End of capturing group 1.
                (?=     # Assert that there follows...
                 (\w+)  # another word; capture that into backref 2.
                )       # End of lookahead.",
                RegexOptions.IgnorePatternWhitespace);
            foreach (var t in cleanedTweets)
            {
                var matchResult = regexObj.Match(t);
                while (matchResult.Success)
                {
                    var pair = matchResult.Groups[1].Value + matchResult.Groups[2].Value;
                    var match = wordPairs.SingleOrDefault(x => x.Key == pair);

                    if (match != null)
                        match.Count++;
                    else
                        wordPairs.Add(new CountableItem<string>(pair, 1));

                    matchResult = matchResult.NextMatch();
                }
            }

            var validPairs = wordPairs.Where(x => x.Count > 1).Select(x => x.Key);

            //Remove words that are found in a phrase and union with phrases
            //words = words.Except(validPairs.SelectMany(x => x.Split(' '))).Concat(validPairs).ToList();

            var keywords = words
                .GroupBy(w => w) //Group Similar Words
                .Select(g => new { Word = g.Key, Count = g.Count() }) // Get Keyword Counts
                .ToList();

            //Update Master Keyword List
            foreach (var w in keywords)
            {
                var item = LanguageAnalyticsResults.KeywordSuggestions.Where(x => x.Key == w.Word).FirstOrDefault();
                if (item != null)
                    item.Count += w.Count;
                else
                    LanguageAnalyticsResults.KeywordSuggestions.Add(new CountableItem(w.Word, w.Count));
            }

            LanguageAnalyticsResults.KeywordSuggestions = LanguageAnalyticsResults.KeywordSuggestions
                .Where(x => !long.TryParse(x.Key, out nothing)) //Ignore Numbers
                //.Where(x => !StopWords.Contains(x.Key)) //Exclude Stop Words
                .Where(x => !manuallyAddedWords.SelectMany(y => y.Split(' ').Concat(new string[] { y })).Contains(x.Key)) //Exclude Manually Added Words
                .Where(x => !x.Key.StartsWith("http")) //No URLs
                .Where(x => x.Key.Length >= MINIMUM_NEW_KEYWORD_LENGTH) //Must be Minimum Length
                .Where(x => Encoding.UTF8.GetByteCount(x.Key) == x.Key.Length) //Only ASCII for me...
                .Where(x =>
                    (x.Count >= MINIMUM_KEYWORD_COUNT || x.LastModifiedTime.AddMinutes(KEYWORD_FALLOUT_MINUTES) >= DateTime.Now)  //Fallout if not seen frequently unless beyond threshold
                    && (x.LastModifiedTime.AddHours(24) > DateTime.Now)) //No matter what it must be seen within the last 24hrs
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.LastModifiedTime)
                .ThenByDescending(x => x.Key.Length)
                .Take(MAX_KEYWORD_SUGGESTIONS)
                .ToList();
        }

        public List<string> GetKeywordSuggestions()
        {
            return new List<string>();
        }

        public void ResetHasNewKeywordSuggestions()
        {

        }

        public bool HasNewKeywordSuggestions()
        {
            return false;
        }

        public void SetIgnoreKeywords(List<string> keywords)
        {
            LanguageAnalyticsResults.KeywordsToIgnore = keywords;
        }

        public Task<IEnumerable<LazyLoader<Tweep>>> ProcessTweeps(IEnumerable<LazyLoader<Tweep>> tweeps)
        {
            return Task<IEnumerable<LazyLoader<Tweep>>>.Factory.StartNew(new Func<IEnumerable<LazyLoader<Tweep>>>(() =>
                {
                    PrimaryTweep.OverrideFollowers(tweeps.ToList());
                    return tweeps;
                }));
        }
    }
}

