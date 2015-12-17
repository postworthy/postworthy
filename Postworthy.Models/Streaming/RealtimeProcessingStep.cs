using Postworthy.Models.Account;
using Postworthy.Models.Repository;
using Postworthy.Models.Twitter;
using Postworthy.Models.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Postworthy.Models.Core;
using System.IO;
using System.Drawing;
using System.Text.RegularExpressions;

namespace Postworthy.Models.Streaming
{
    class RealtimeProcessingStep : StandardProcessingStep
    {
        private const int MAX_CONTENT = 1000;
        private PostworthyUser User;
        private List<Tweet> Tweets = new List<Tweet>();
        private ArticleStubPage Page = new ArticleStubPage();
        private SimpleRepository<ArticleStubPage> repoPage;
        private SimpleRepository<ArticleStubIndex> repoIndex;

        public override void Init(string screenname, System.IO.TextWriter log)
        {
            base.Init(screenname, log);
            User = UsersCollection.PrimaryUsers().Where(x => x.TwitterScreenName == screenname).First();
            repoPage = new SimpleRepository<ArticleStubPage>(screenname);
            repoIndex = new SimpleRepository<ArticleStubIndex>(screenname);
        }

        protected override void StoreInRepository(IEnumerable<Twitter.Tweet> tweets)
        {
            var start = DateTime.Now.AddHours(-48);
            var dayTag = "_" + DateTime.Now.ToShortDateString();

            Func<Tweet, bool> where = t =>
                t != null &&
                    //Should everything be displayed or do you only want content
                (User.OnlyTweetsWithLinks == false || (t.Links != null && t.Links.Count > 0)) &&
                    //Minumum threshold applied so we get results worth seeing (if it is your own tweet it gets a pass on this step)
                ((t.RetweetCount > User.RetweetThreshold || t.User.ScreenName.ToLower() == User.TwitterScreenName.ToLower()) &&
                    //Apply Date Range
                (t.CreatedAt >= start));

            Tweets = Tweets.Union(tweets.Where(where)).OrderByDescending(x=>x.TweetRank).Take(MAX_CONTENT).ToList();
            
            var groups = Tweets
                //Group similar tweets 
                .GroupSimilar2()
                //Convert groups into something we can display
                .Select(g => new TweetGroup(g) { RepositoryKey = TwitterModel.Instance(User.TwitterScreenName).CONTENT })
                //Order by TweetRank
                .OrderByDescending(g => g.TweetRank)
                //Only the top content
                .Take(MAX_CONTENT);

            Task<List<ArticleStub>> contentTask = null;
            Task continueTask = null;

            if (groups != null && groups.Count() > 0)
            {
                //Get Standard Deviation
                double stdev = 0;
                var values = groups.Select(x => x.TweetRank);
                double avg = values.Average();
                stdev = Math.Sqrt(values.Sum(d => (d - avg) * (d - avg)) / values.Count());

                //Filter groups that are way high...
                //groups = groups.Where(x => x.TweetRank < (avg + stdev));

                var results = groups.OrderByDescending(x=>x.TweetRank).ToList();
                contentTask = CreateContent(results, Page);
                continueTask = contentTask.ContinueWith(task => {
                    if (task.Result.Count >= 25)
                    {
                        var key = TwitterModel.Instance(screenName).CONTENT.ToLower();
                        Page = new ArticleStubPage(1, task.Result.Take(100));

                        repoPage.Delete(key);
                        repoPage.Save(key, Page);

                        repoPage.Delete(key + dayTag);
                        repoPage.Save(key + dayTag, Page);

                        var articleStubIndex = repoIndex.Query(TwitterModel.Instance(screenName).CONTENT_INDEX).FirstOrDefault() ?? new ArticleStubIndex();
                        var day = DateTime.Now.StartOfDay();
                        if (articleStubIndex.ArticleStubPages.Where(x => x.Key == day.ToFileTimeUtc()).Count() == 0)
                        {
                            articleStubIndex.ArticleStubPages.Add(new KeyValuePair<long, string>(day.ToFileTimeUtc(), day.ToShortDateString()));
                            repoIndex.Save(TwitterModel.Instance(screenName).CONTENT_INDEX, articleStubIndex);
                        }
                    }
                });
            }

            base.StoreInRepository(tweets);

            if (contentTask != null && contentTask != null)
                Task.WaitAll(contentTask, continueTask);
        }

        private async Task<List<ArticleStub>> CreateContent(List<TweetGroup> groupingResults, ArticleStubPage existing)
        {
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

            return results;
        }

        private List<Uri> ExtractImageUris(UriEx uriex, HtmlAgilityPack.HtmlDocument doc)
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

        private string ExtractSummary(string title, HtmlAgilityPack.HtmlDocument doc)
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

        private async Task<Tuple<string, Uri>> GetBestImage(IEnumerable<Uri> images)
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

    }
}
