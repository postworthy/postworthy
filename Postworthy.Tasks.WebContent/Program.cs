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
using System.Security.Cryptography;

namespace Postworthy.Tasks.WebContent
{
    class Program
    {
        private const int MAX_CONTENT = 100;
        static void Main(string[] args)
        {
            if (!EnsureSingleLoad())
            {
                Console.WriteLine("{0}: Another Instance Currently Running", DateTime.Now);
                return;
            }

            var start = DateTime.Now;
            Console.WriteLine("{0}: Started", start);

            var users = UsersCollection.PrimaryUsers() ?? new List<PostworthyUser>();

            var tasks = new List<Task>();

            users.AsParallel().ForAll(u =>
            {
                var tweet = "";
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
                        var domain = u.PrimaryDomains.OrderBy(x => x.Length).FirstOrDefault();
                        if (!string.IsNullOrEmpty(domain) && !domain.StartsWith("beta"))
                            tweet = "Here are the top articles from " + day.ToShortDateString().Replace('/', '-') + " http://" + domain + "/" + day.ToShortDateString().Replace('/', '-');
                    }
                    else
                    {
                        articleStubIndex = null;
                        day = DateTime.MinValue;
                        dayTag = "";
                    }
                }


                var groupingResults = CreateGroups(u, day == DateTime.MinValue ? null : (DateTime?)day);
                var existing = repoPage.Query(TwitterModel.Instance(u.TwitterScreenName).CONTENT + dayTag).FirstOrDefault();
                var contentTask = CreateContent(u, groupingResults, existing);
                Console.WriteLine("{0}: Waiting on content for {1}", DateTime.Now, u.TwitterScreenName);
                var continueTask = contentTask.ContinueWith(task =>
                {
                    Console.WriteLine("{0}: Content completed for {1}", DateTime.Now, u.TwitterScreenName);
                    var stubs = task.Result.Take(MAX_CONTENT);
                    if (stubs.Count() > 0 || !string.IsNullOrEmpty(dayTag))
                    {
                        var articleStubPage = new ArticleStubPage(1, stubs);

                        if (existing != null && existing.ExcludedArticleStubs.Count > 0)
                        {
                            articleStubPage.ExcludedArticleStubs = existing.ExcludedArticleStubs.Where(e => articleStubPage.ArticleStubs.Contains(e)).ToList();
                        }

                        Console.WriteLine("{0}: Deleting old data from files from storage for {1}", DateTime.Now, u.TwitterScreenName);
                        repoPage.Delete(TwitterModel.Instance(u.TwitterScreenName).CONTENT + dayTag);

                        Console.WriteLine("{0}: Storing data in repository for {1}", DateTime.Now, u.TwitterScreenName);
                        repoPage.Save(TwitterModel.Instance(u.TwitterScreenName).CONTENT + dayTag, articleStubPage);

                        if (articleStubIndex != null)
                            repoIndex.Save(TwitterModel.Instance(u.TwitterScreenName).CONTENT_INDEX, articleStubIndex);

                        if (!string.IsNullOrEmpty(tweet))
                        {
                            try
                            {
                                TwitterModel.Instance(u.TwitterScreenName).UpdateStatus(tweet, processStatus: false);
                            }
                            catch(Exception ex) { Console.WriteLine("{0}: Could not tweet message: {1}" + Environment.NewLine + "The following exception was thrown: {2}", DateTime.Now, tweet, ex.ToString()); }
                        }
                    }
                    else
                        Console.WriteLine("{0}: No articles found for {1}", DateTime.Now, u.TwitterScreenName);
                });
                tasks.Add(contentTask);
                tasks.Add(continueTask);
            });

            Task.WaitAll(tasks.ToArray());

            var end = DateTime.Now;
            Console.WriteLine("{0}: Ending and it took {1} minutes to complete", end, (end - start).TotalMinutes);
        }

        private static async Task<List<ArticleStub>> CreateContent(PostworthyUser user, List<TweetGroup> groupingResults, ArticleStubPage existing)
        {
            var startContent = DateTime.Now;
            Console.WriteLine("{0}: Starting content procedure for {1}", startContent, user.TwitterScreenName);

            List<ArticleStub> results = new List<ArticleStub>();

            var contentItems = new List<object>();
            foreach (var result in groupingResults)
            {
                if (contentItems.Count >= MAX_CONTENT)
                    break;

                var existingItem = existing != null ?
                    existing.ArticleStubs.Where(x => result.Links.Select(y => y.Uri).Contains(x.Link)).FirstOrDefault() : null;

                if (existingItem != null)
                {
                    contentItems.Add(existingItem);
                    continue;
                }
                var imageUris = new List<Uri>();
                imageUris = result.Links.Where(l => l.Image != null).Select(l => l.Image).ToList();
                var links = result.Links.OrderByDescending(x => x.ShareCount);
                foreach (var uriex in links)
                {
                    if (uriex.IsHtmlContentUrl)
                    {
                        var doc = new HtmlAgilityPack.HtmlDocument();
                        try
                        {
                            var req = uriex.Uri.GetWebRequest(15000, 15000);
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
                            imageUris.AddRange(ExtractImageUris(uriex, doc));

                            var content = new
                            {
                                Title = uriex.Title,
                                SubTitle = uriex.Description,
                                Link = uriex.Uri,
                                //Image = image == null ? null : ImageManipulation.EncodeImage(image, width, height),
                                Summary = ExtractSummary(uriex.Title + " " + uriex.Description, doc),
                                Video = uriex.Video,
                                Images = imageUris
                            };

                            contentItems.Add(content);
                            break;
                        }
                    }
                }
            }

            var newImages = contentItems
                .Where(x => x.GetType() != typeof(ArticleStub))
                .Select(x => (dynamic)x)
                .SelectMany(x => ((List<Uri>)x.Images).Select(y => new { ID = ((object)x.Title).GetHashCode(), Image = y }))
                .ToList();

            var stubImages = contentItems
                .Where(x => x.GetType() == typeof(ArticleStub))
                .Where(x => ((ArticleStub)x).OriginalImageUri != null)
                .Select(x => new { ID = ((ArticleStub)x).Title.GetHashCode(), Image = ((ArticleStub)x).OriginalImageUri })
                .ToArray();

            if (stubImages != null && stubImages.Length > 0)
                newImages.AddRange(stubImages);

            var allImages = newImages.ToArray();

            var excludedImages = new List<Uri>();
            for (int i = 0; i < allImages.Length - 1; i++)
            {
                var img = allImages[i];
                if (!excludedImages.Contains(img.Image))
                {
                    for (int j = i + 1; j < allImages.Length; j++)
                    {
                        var img2 = allImages[j];
                        if (img.Image == img2.Image && img.ID != img2.ID)
                        {
                            excludedImages.Add(img2.Image);
                            break;
                        }
                    }
                }
            }

            foreach (var obj in contentItems)
            {
                if (obj.GetType() != typeof(ArticleStub))
                {
                    dynamic item = obj;
                    var image = await GetBestImage(((List<Uri>)item.Images ?? new List<Uri>()).Where(y => !excludedImages.Contains(y)));
                    results.Add(new ArticleStub
                    {
                        Title = item.Title,
                        SubTitle = item.SubTitle,
                        Link = item.Link,
                        Image = image != null ? image.Item1 : null,
                        Summary = item.Summary,
                        Video = item.Video,
                        OriginalImageUri = image != null ? image.Item2 : null
                    });
                }
                else if (excludedImages.Contains(((ArticleStub)obj).OriginalImageUri))
                {
                    var item = (ArticleStub)obj;
                    item.Image = null;
                    results.Add(item);
                }
                else
                    results.Add(obj as ArticleStub);
            }
            var endContent = DateTime.Now;
            Console.WriteLine("{0}: Content procedure for {1} completed and it took {2} minutes to complete", endContent, user.TwitterScreenName, (endContent - startContent).TotalMinutes);

            return results;
        }

        private static List<Uri> ExtractImageUris(UriEx uriex, HtmlAgilityPack.HtmlDocument doc)
        {
            var images = new List<Uri>();
            var strongFilter = false;
            var imageNodes = doc.DocumentNode.SelectNodes("//article/descendant-or-self::img");
            if (imageNodes == null && !string.IsNullOrEmpty(uriex.Title))
            {
                var titleNode = doc.DocumentNode.SelectNodes("//body/descendant-or-self::*[starts-with(., '" + uriex.Title.Split('\'')[0] + "')]");
                if (titleNode != null)
                {
                    imageNodes = titleNode.FirstOrDefault().ParentNode.SelectNodes("/descendant-or-self::img");
                    strongFilter = true;
                }
            }

            if (imageNodes != null)
            {
                var imageUrls = imageNodes
                    .Where(i => i.Attributes["src"] != null &&
                        i.Attributes["src"].Value != null)
                    .Select(i => { try { return new Uri(i.Attributes["src"].Value); } catch { return null; } })
                    .Where(x => x != null && (!strongFilter || x.Host == uriex.Uri.Host))
                    .ToList();
                return imageUrls;
            }
            else
                return new List<Uri>();
        }

        private static async Task<List<Bitmap>> ExtractImageContent(UriEx uriex, HtmlAgilityPack.HtmlDocument doc)
        {
            var images = new List<Bitmap>();
            var strongFilter = false;
            var imageNodes = doc.DocumentNode.SelectNodes("//article/descendant-or-self::img");
            if (imageNodes == null && !string.IsNullOrEmpty(uriex.Title))
            {
                var titleNode = doc.DocumentNode.SelectNodes("//body/descendant-or-self::*[starts-with(., '" + uriex.Title.Split('\'')[0] + "')]");
                if (titleNode != null)
                {
                    imageNodes = titleNode.FirstOrDefault().ParentNode.SelectNodes("/descendant-or-self::img");
                    strongFilter = true;
                }
            }

            if (imageNodes != null)
            {
                var imageUrls = imageNodes
                    .Where(i => i.Attributes["src"] != null &&
                        i.Attributes["src"].Value != null)
                    .Select(i => { try { return new Uri(i.Attributes["src"].Value); } catch { return null; } })
                    .Where(x => x != null && (!strongFilter || x.Host == uriex.Uri.Host));
                foreach (var imageUrl in imageUrls)
                {
                    try
                    {
                        using (var resp = await imageUrl.GetWebRequest(15000, 15000).GetResponseAsync())
                        {
                            var img = new Bitmap(resp.GetResponseStream());
                            images.Add(img);
                        }
                    }
                    catch (Exception ex) { }
                }
                return images;
            }
            else
                return new List<Bitmap>();
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

            Console.WriteLine("{0}: Starting grouping procedure for {1}", startGrouping, user.TwitterScreenName);

            Console.WriteLine("{0}: Fetching tweets for {1}", startGrouping, user.TwitterScreenName);

            var tweets = screenNames
                //For each screen name (i.e. - you and your friends if included) select the most recent tweets
                .SelectMany(x => repoTweets.Query(x + TwitterModel.Instance(user.TwitterScreenName).TWEETS, where: where) ?? new List<Tweet>())
                //Order all tweets based on rank (TweetRank takes into acount many important factors, i.e. - time, mentions, hotness, ect.)
                .OrderByDescending(t => t.TweetRank)
                //Just to make sure we are not trying to group a very very large number of items
                .Take(5000)
                .ToList();

            Console.WriteLine("{0}: Grouping tweets by similarity for {1}", DateTime.Now, user.TwitterScreenName);

            var groups = tweets
                //Group similar tweets 
                .GroupSimilar2()
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
            Console.WriteLine("{0}: Grouping procedure for {1} completed and it took {2} minutes to complete", endGrouping, user.TwitterScreenName, (endGrouping - startGrouping).TotalMinutes);

            return results ?? new List<TweetGroup>();
        }

        private static async Task<Tuple<string, Uri>> GetBestImage(IEnumerable<Uri> images)
        {
            if (images == null)
                return null;

            Bitmap image = null;

            Uri selected = null;

            foreach (var img in images)
            {
                try
                {
                    using (var resp = await img.GetWebRequest(15000, 15000).GetResponseAsync())
                    {
                        image = new Bitmap(resp.GetResponseStream());
                        selected = img;
                        break;
                    }
                }
                catch (Exception ex) { }
            }

            if (image == null)
                return null;

            float scaleFactor = 0;
            int width = 0;
            int height = 0;

            if (image != null)
            {
                scaleFactor = (image.Width > 200) ? ((float)200 / (float)image.Width) : 1;
                width = (int)(image.Width * scaleFactor);
                height = (int)(image.Height * scaleFactor);
            }

            return new Tuple<string, Uri>(ImageManipulation.EncodeImage(image, width, height), selected);
        }

        private static bool EqualBitmaps(Bitmap bmp1, Bitmap bmp2)
        {
            //Test to see if we have the same size of image
            if (bmp1.Size != bmp2.Size)
            {
                return false;
            }
            else
            {
                //Convert each image to a byte array
                System.Drawing.ImageConverter ic =
                       new System.Drawing.ImageConverter();
                byte[] btImage1 = new byte[1];
                btImage1 = (byte[])ic.ConvertTo(bmp1, btImage1.GetType());
                byte[] btImage2 = new byte[1];
                btImage2 = (byte[])ic.ConvertTo(bmp2, btImage2.GetType());

                //Compute a hash for each image
                SHA256Managed shaM = new SHA256Managed();
                byte[] hash1 = shaM.ComputeHash(btImage1);
                byte[] hash2 = shaM.ComputeHash(btImage2);

                //Compare the hash values
                for (int i = 0; i < hash1.Length && i < hash2.Length; i++)
                {
                    if (hash1[i] != hash2[i])
                        return false;
                }
            }
            return true;
        }

        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.WebContent", out result);

            return result;
        }
    }
}
