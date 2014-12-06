using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Postworthy.Models;
using Postworthy.Models.Account;
using Postworthy.Models.Twitter;
using Postworthy.Web.Models;
using Postworthy.Models.Repository;
using Postworthy.Models.Web;
using Postworthy.Models.Core;
using System.Web.Caching;
namespace Postworthy.Web.Controllers
{
    public class HomeController : Controller
    {
        private const string FRONTPAGE_SLUG = "frontpage";
        private const string PHOTOS_SLUG = "photos";
        private const string VIDEOS_SLUG = "videos";
        protected PostworthyUser PrimaryUser { get; set; }
        protected override void Initialize(System.Web.Routing.RequestContext requestContext)
        {
            base.Initialize(requestContext);
            PrimaryUser = UsersCollection.PrimaryUsers().Where(u => u.IsPrimaryUser).FirstOrDefault();
            if (PrimaryUser != null)
                ViewBag.Brand = PrimaryUser.SiteName;
        }
        [OutputCache(Duration = 300, VaryByCustom = "User")]
        public ActionResult Index(DateTime? id = null, string slug = null)
        {
            //if (MobileHelper.IsMobileDevice(Request.UserAgent)) return RedirectToAction("index", "mobile");
            if (!String.IsNullOrEmpty(Request.QueryString["p"]))
                return RedirectPermanent("~/");

            string dayTag = "";
            DateTime date = DateTime.Now;
            if (id.HasValue)
            {
                date = id.Value;
                if (!string.IsNullOrEmpty(slug))
                {
                    switch (slug)
                    {
                        case PHOTOS_SLUG:
                            return Photos(DateTime.Now.ToShortDateString() != date.ToShortDateString() ? (DateTime?)date : null);
                        case VIDEOS_SLUG:
                            return Videos(DateTime.Now.ToShortDateString() != date.ToShortDateString() ? (DateTime?)date : null);
                        default:
                            return Video(DateTime.Now.ToShortDateString() != date.ToShortDateString() ? (DateTime?)date : null, slug);
                    }
                }
                else
                    dayTag = "_" + date.ToShortDateString();
            }

            ViewBag.Date = date;
            ViewBag.ArticleStubIndex = CachedRepository<ArticleStubIndex>.Instance(PrimaryUser.TwitterScreenName)
                .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).CONTENT_INDEX)
                .FirstOrDefault();
            
            var page = CachedRepository<ArticleStubPage>
                .Instance(PrimaryUser.TwitterScreenName)
                .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).CONTENT + dayTag)
                .FirstOrDefault() ?? new ArticleStubPage();

            var articleIndex = CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX)
                .FirstOrDefault() ?? new ArticleIndex();

            ViewBag.ArticlesIndex = articleIndex;

            return View(page);
        }
        public ActionResult FrontPage()
        {
            ViewBag.ArticleStubIndex = CachedRepository<ArticleStubIndex>.Instance(PrimaryUser.TwitterScreenName)
                .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).CONTENT_INDEX)
                .FirstOrDefault();

            var page = CachedRepository<ArticleStubPage>
                .Instance(PrimaryUser.TwitterScreenName)
                .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).CONTENT)
                .FirstOrDefault() ?? new ArticleStubPage();

            var index = CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX).FirstOrDefault() ?? new ArticleIndex();

            var items = index.Articles.OrderByDescending(i => i.Published).Take(25);

            var fullArticles = new List<Article>();

            foreach (var item in items)
            {
                var articles = CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                    .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + item.DayTag).ToList();

                var article = articles.Where(x => x.UniqueKey == item.Key).FirstOrDefault();
                if (article != null)
                    fullArticles.Add(article);
            }

            return View("FrontPage",new FrontPageModel(fullArticles, page.ArticleStubs));
        }

        [HttpPost]
        [AuthorizePrimaryUser]
        public ActionResult Index(string slug, DateTime? id = null)
        {
            string dayTag = "";
            DateTime date = DateTime.Now;
            if (id.HasValue)
            {
                date = id.Value;
                dayTag = "_" + date.ToShortDateString();
            }

            ViewBag.Date = date;

            var page = CachedRepository<ArticleStubPage>.Instance(PrimaryUser.TwitterScreenName).Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).CONTENT + dayTag).FirstOrDefault();

            var article = page.ArticleStubs.Where(s => s.GetSlug() == slug).FirstOrDefault();
            page.ExcludedArticleStubs.Add(article);

            page.ExcludedArticleStubs = page.ExcludedArticleStubs.Distinct().ToList();

            CachedRepository<ArticleStubPage>.Instance(PrimaryUser.TwitterScreenName).Save(TwitterModel.Instance(PrimaryUser.TwitterScreenName).CONTENT + dayTag, page);

            HttpResponse.RemoveOutputCacheItem(Url.RouteUrl(Request.RequestContext.RouteData.Values));

            return RedirectToAction("Index", new { id = !id.HasValue ? null : (id.Value.ToShortDateString().Replace('/', '-')) });
        }

        public ActionResult Videos(DateTime? id)
        {
            DateTime date = DateTime.Now;
            if (id.HasValue)
                date = id.Value;

            ViewBag.Date = date;

            var dayTag = id.HasValue ? "_" + id.Value.ToShortDateString() : "";

            var page = CachedRepository<ArticleStubPage>.Instance(PrimaryUser.TwitterScreenName).Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).CONTENT + dayTag).FirstOrDefault();

            var videos = page.ArticleStubs.Where(s => s.Video != null).ToList();

            ViewBag.Videos = videos;

            if (videos != null && videos.Count > 0)
                return View("Video", null);
            else
                return RedirectPermanent("~/");
        }

        public ActionResult Video(DateTime? id, string slug)
        {
            DateTime date = DateTime.Now;
            if (id.HasValue)
                date = id.Value;

            ViewBag.Date = date;

            var dayTag = id.HasValue ? "_" + id.Value.ToShortDateString() : "";

            var page = CachedRepository<ArticleStubPage>.Instance(PrimaryUser.TwitterScreenName).Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).CONTENT + dayTag).FirstOrDefault();

            var videos = page.ArticleStubs.Where(s => s.Video != null).ToList();
            var stub = videos.Where(s => s.GetSlug() == slug).FirstOrDefault();

            ViewBag.Videos = videos;

            if (stub != null)
                return View("Video", stub);
            else
                return RedirectPermanent("~/");
        }

        public ActionResult Photos(DateTime? id)
        {
            DateTime date = DateTime.Now;
            if (id.HasValue)
                date = id.Value;

            ViewBag.Date = date;

            var dayTag = id.HasValue ? "_" + id.Value.ToShortDateString() : "";
            var page = CachedRepository<ArticleStubPage>.Instance(PrimaryUser.TwitterScreenName).Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).CONTENT + dayTag).FirstOrDefault();
            var photoStubs = page.ArticleStubs.Where(s => s.Link.Authority.ToLower() == "instagram.com").ToList();

            if (photoStubs != null && photoStubs.Count > 0)
                return View("Photos", photoStubs);
            else
                return RedirectPermanent("~/");
        }

        [AuthorizePrimaryUser]
        [HttpPost]
        public ActionResult Tweet(string Tweet)
        {
            //if (MobileHelper.IsMobileDevice(Request.UserAgent)) return RedirectToAction("tweet", "mobile");

            if (!string.IsNullOrEmpty(Tweet))
                TwitterModel.Instance(PrimaryUser.TwitterScreenName).UpdateStatus(Tweet, User.Identity.Name);

            return RedirectToAction("Index");
        }

        [Authorize]
        public ActionResult Retweet(ulong id)
        {
            //if (MobileHelper.IsMobileDevice(Request.UserAgent)) return RedirectToAction("retweet", "mobile");

            if (id > 0)
                TwitterModel.Instance(PrimaryUser.TwitterScreenName).Retweet(id, User.Identity.Name);

            return RedirectToAction("Index");
        }

        /*public ActionResult Directory()
        {
            return View(UsersCollection.All());
        }*/

        public ActionResult About()
        {
            //if (MobileHelper.IsMobileDevice(Request.UserAgent)) return RedirectToAction("about", "mobile");

            return View(PrimaryUser);
        }

        public ActionResult Image(string id)
        {
            var base64 = (string)System.Web.HttpContext.Current.Cache[id];
            if (!string.IsNullOrEmpty(base64))
                return File(Convert.FromBase64String(base64), "image/png");
            else
                throw new HttpException(404, id + " not found");
        }
    }
}
