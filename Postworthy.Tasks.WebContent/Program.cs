using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Account;
using Postworthy.Models.Twitter;
using System.Linq.Expressions;
using Postworthy.Models.Repository;
using Postworthy.Models.Repository.Providers;
using Postworthy.Models.Core;
using System.IO;
using System.Threading.Tasks;
using System.Drawing;
using System.Text.RegularExpressions;
using Postworthy.Models.Web;

namespace Postworthy.Tasks.WebContent
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!EnsureSingleLoad())
            {
                Console.WriteLine("{0}: Another Instance Currently Runing", DateTime.Now);
                return;
            }

            var start = DateTime.Now;
            Console.WriteLine("{0}: Started", start);

            var users = UsersCollection.PrimaryUsers() ?? new List<PostworthyUser>();

            users.AsParallel().ForAll(u =>
            {
                var repoIndex = new SimpleRepository<ArticleStubIndex>(u.TwitterScreenName);
                var repoPage = new SimpleRepository<ArticleStubPage>(u.TwitterScreenName);
                ArticleStubIndex articleStubIndex = null;
                string dayTag = "";
                DateTime day = DateTime.MinValue;
                if (args.Length > 0)
                {
                    if (DateTime.TryParse(args[0], out day))
                    {
                        day = day.StartOfDay();

                        dayTag = "_" + day.ToShortDateString();
                        articleStubIndex = repoIndex.Query(TwitterModel.Instance(u.TwitterScreenName).CONTENT_INDEX).FirstOrDefault() ?? new ArticleStubIndex();
                        if (articleStubIndex.ArticleStubPages.Where(x => x.Key == day.ToFileTimeUtc()).Count() == 0)
                            articleStubIndex.ArticleStubPages.Add(new KeyValuePair<long, string>(day.ToFileTimeUtc(), day.ToShortDateString()));
                        else
                            articleStubIndex = null;
                    }
                }
                else
                {
                    articleStubIndex = repoIndex.Query(TwitterModel.Instance(u.TwitterScreenName).CONTENT_INDEX).FirstOrDefault() ?? new ArticleStubIndex();
                    day = DateTime.Now.AddDays(-1);
                    day = day.StartOfDay();
                    if (articleStubIndex.ArticleStubPages.Where(x => x.Key == day.ToFileTimeUtc()).Count() == 0)
                    {
                        dayTag = "_" + day.ToShortDateString();
                        articleStubIndex.ArticleStubPages.Add(new KeyValuePair<long, string>(day.ToFileTimeUtc(), day.ToShortDateString()));
                    }
                    else
                    {
                        articleStubIndex = null;
                        day = DateTime.MinValue;
                        dayTag = "";
                    }
                }


                var groupingResults = CreateGroups(u, day == DateTime.MinValue ? null : (DateTime?)day);
                var contentTask = CreateContent(groupingResults);
                contentTask.Wait();
                var articleStubPage = new ArticleStubPage(1, contentTask.Result.Take(100));

                var existing = repoPage.Query(TwitterModel.Instance(u.TwitterScreenName).CONTENT + dayTag).FirstOrDefault();

                if (existing != null && existing.ExcludedArticleStubs.Count > 0)
                {
                    articleStubPage.ExcludedArticleStubs = existing.ExcludedArticleStubs.Where(e=> articleStubPage.ArticleStubs.Contains(e)).ToList();
                }

                Console.WriteLine("{0}: Deleting old data from files from storage", DateTime.Now);
                repoPage.Delete(TwitterModel.Instance(u.TwitterScreenName).CONTENT + dayTag);

                Console.WriteLine("{0}: Storing data in repository", DateTime.Now);
                repoPage.Save(TwitterModel.Instance(u.TwitterScreenName).CONTENT + dayTag, articleStubPage);

                if (articleStubIndex != null)
                    repoIndex.Save(TwitterModel.Instance(u.TwitterScreenName).CONTENT_INDEX, articleStubIndex);

            });

            var end = DateTime.Now;
            Console.WriteLine("{0}: Ending and it took {1} minutes to complete", end, (end - start).TotalMinutes);
        }

        private static async Task<List<ArticleStub>> CreateContent(List<TweetGroup> groupingResults)
        {
            var contentItems = new List<ArticleStub>();
            foreach (var result in groupingResults)
            {
                var images = new List<Bitmap>();
                foreach (var link in result.Links.Where(l => l.Image != null))
                {
                    try
                    {
                        using (var resp = await link.Image.GetWebRequest().GetResponseAsync())
                        {
                            var img = new Bitmap(resp.GetResponseStream());
                            images.Add(img);
                        }
                    }
                    catch (Exception ex) { }
                }
                var links = result.Links.OrderByDescending(x => x.ShareCount);
                foreach (var uriex in links)
                {
                    if (uriex.IsHtmlContentUrl)
                    {
                        var doc = new HtmlAgilityPack.HtmlDocument();
                        try
                        {
                            var req = uriex.Uri.GetWebRequest();
                            using (var resp = await req.GetResponseAsync())
                            {
                                using (var reader = new StreamReader(resp.GetResponseStream(), true))
                                {
                                    doc.Load(reader);
                                }
                            }
                        }
                        catch (Exception ex) { }

                        if (doc.DocumentNode != null)
                        {
                            var image = images.OrderByDescending(i => i.Width * i.Height).FirstOrDefault();

                            float scaleFactor = 0;
                            int width = 0;
                            int height = 0;

                            if (image != null)
                            {
                                scaleFactor = (image.Width > 200) ? ((float)200 / (float)image.Width) : 1;
                                width = (int)(image.Width * scaleFactor);
                                height = (int)(image.Height * scaleFactor);
                            }

                            var content = new ArticleStub
                            {
                                Title = uriex.Title,
                                SubTitle = uriex.Description,
                                Link = uriex.Uri,
                                Image = image == null ? null :
                                    ImageManipulation.EncodeImage(image, width, height),
                                Summary = ExtractSummary(uriex.Title + " " + uriex.Description, doc),
                                Video = uriex.Video
                            };

                            contentItems.Add(content);
                            break;
                        }
                    }
                }
            }
            return contentItems;
        }

        private static string ExtractSummary(string title, HtmlAgilityPack.HtmlDocument doc)
        {
            Regex PunctuationRegex = new Regex(@"(\p{P})|\t|\n|\r", RegexOptions.Compiled);
            Regex WhiteSpaceRegex = new Regex(@"\s{2,}", RegexOptions.Compiled);

            var matchWords = WhiteSpaceRegex.Replace(PunctuationRegex.Replace(title, " "), " ").ToLower().Split(' ').Distinct();

            var scripts = doc.DocumentNode.SelectNodes("//script");
            if (scripts != null)
                foreach (var node in scripts)
                    node.Remove();

            var styles = doc.DocumentNode.SelectNodes("//style");
            if (styles != null)
                foreach (var node in styles)
                    node.Remove();

            var textNodes = doc.DocumentNode
                .SelectNodes("//body//*[not(self::script or self::style)]/text()");

            Func<HtmlAgilityPack.HtmlNode, double> recursiveSum = null;
            recursiveSum = new Func<HtmlAgilityPack.HtmlNode, double>(n =>
            {
                if (n.ChildNodes.Count > 0)
                    return n.ChildNodes.Sum(c => recursiveSum(c));
                else
                {
                    if (matchWords.Count() > 0)
                    {
                        var nodeWords = WhiteSpaceRegex.Replace(PunctuationRegex.Replace(n.InnerText, " "), " ").ToLower().Split(' ');
                        return (matchWords.Where(w => nodeWords.Contains(w)).Count() / (1.0 * matchWords.Count())) * nodeWords.Count();
                    }
                    else
                        return 0;
                }
            });

            if (textNodes != null)
            {
                var node = textNodes
                    .OrderByDescending(n => recursiveSum(n))
                    .Select(n => n)
                    .FirstOrDefault();
                if (node != null)
                {
                    var summary = node.InnerText ?? " ";

                    var prevSibling = node.PreviousSibling;
                    while (prevSibling != null)
                    {
                        summary = (prevSibling.InnerText ?? "") + " " + summary;
                        prevSibling = prevSibling.PreviousSibling;
                    }

                    var nextSibling = node.NextSibling;
                    while (nextSibling != null)
                    {
                        summary = summary + " " + (nextSibling.InnerText ?? "");
                        nextSibling = nextSibling.NextSibling;
                    }


                    Regex regex = new Regex(@"</?\w+((\s+\w+(\s*=\s*(?:"".*?""|'.*?'|[^'"">\s]+))?)+\s*|\s*)/?>", RegexOptions.Singleline);

                    summary = regex.Replace(summary, "");

                    return summary;
                }
            }
            return "";
        }

        private static List<TweetGroup> CreateGroups(PostworthyUser user, DateTime? day)
        {
            var repoTweets = new SimpleRepository<Tweet>(user.TwitterScreenName);
            List<string> screenNames = null;

            screenNames = TwitterModel.Instance(user.TwitterScreenName).GetRelevantScreenNames(user.TwitterScreenName);

            int RetweetThreshold = user.RetweetThreshold;


            DateTime start = day == null ? DateTime.Now.AddHours(-48) : day.Value.StartOfDay();
            DateTime end = day == null ? DateTime.Now : day.Value.EndOfDay();

            Func<Tweet, bool> where = t =>
                t != null &&
                    //Should everything be displayed or do you only want content
                (user.OnlyTweetsWithLinks == false || (t.Links != null && t.Links.Count > 0)) &&
                    //Minumum threshold applied so we get results worth seeing (if it is your own tweet it gets a pass on this step)
                ((t.RetweetCount > RetweetThreshold || t.User.ScreenName.ToLower() == user.TwitterScreenName.ToLower()) &&
                    //Apply Date Range
                (t.CreatedAt >= start && t.CreatedAt <= end));

            var startGrouping = DateTime.Now;
            Console.WriteLine("{0}: Starting grouping procedure", startGrouping);

            var tweets = screenNames
                //For each screen name (i.e. - you and your friends if included) select the most recent tweets
                .SelectMany(x => repoTweets.Query(x + TwitterModel.Instance(user.TwitterScreenName).TWEETS, where: where) ?? new List<Tweet>())
                //Order all tweets based on rank (TweetRank takes into acount many important factors, i.e. - time, mentions, hotness, ect.)
                .OrderByDescending(t => t.TweetRank)
                //Just to make sure we are not trying to group a very very large number of items
                .Take(5000);

            var groups = tweets
                //Group similar tweets (the ordering is done first so that the earliest tweet gets credit)
                .GroupSimilar(log: Console.Out)
                //Convert groups into something we can display
                .Select(g => new TweetGroup(g) { RepositoryKey = TwitterModel.Instance(user.TwitterScreenName).CONTENT })
                //Order by TweetRank
                .OrderByDescending(g => g.TweetRank)
                //Only the top 500
                .Take(500);

            List<TweetGroup> results = null;

            if (groups != null && groups.Count() > 0)
            {
                //Get Standard Deviation
                double stdev = 0;
                var values = groups.Select(x => x.TweetRank);
                double avg = values.Average();
                stdev = Math.Sqrt(values.Sum(d => (d - avg) * (d - avg)) / values.Count());

                //Filter groups that are way high...
                groups = groups.Where(x => x.TweetRank < (avg + stdev));

                results = groups.ToList();
            }

            var endGrouping = DateTime.Now;
            Console.WriteLine("{0}: Grouping procedure completed and it took {1} minutes to complete", endGrouping, (endGrouping - startGrouping).TotalMinutes);

            return results ?? new List<TweetGroup>();
        }

        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.WebContent", out result);

            return result;
        }
    }
}
