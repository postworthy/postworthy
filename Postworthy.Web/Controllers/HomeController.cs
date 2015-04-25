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
        public static string[] IMAGE_DOMAINS = { "instagram.com", "ow.ly" };
        protected PostworthyUser PrimaryUser { get; set; }
        protected override void Initialize(System.Web.Routing.RequestContext requestContext)
        {
            base.Initialize(requestContext);
            ViewBag.Home = true;
            PrimaryUser = UsersCollection.PrimaryUsers().Where(u => u.IsPrimaryUser).FirstOrDefault();
            if (PrimaryUser != null)
                ViewBag.Brand = PrimaryUser.SiteName;
        }
        [OutputCache(Duration = 300, VaryByCustom = "User")]
        public ActionResult Index(DateTime? id = null, string slug = null)
        {
            var p = Request.QueryString["p"];
            if (!string.IsNullOrEmpty(p))
            {
                Session[p] = Request.Url.ToString();
                return RedirectToAction("Details", "Article", new { id = p, slug = "p" });
            }

            if (Request.Url.ToString().ToLower().Contains("/home/article?id="))
            {
                Session[Request.QueryString["id"]] = Request.Url.ToString();
                return RedirectToAction("Details", "Article", new { id = Request.QueryString["id"], slug = "p" });
            }

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
            }
            
            var model = new PostworthyArticleModel(PrimaryUser);
            var articles = model.GetArticleIndex();

            if (articles.Articles.Count >= 5 && !id.HasValue && PrimaryUser.EnableFrontPage)
                return FrontPage();

            ViewBag.Date = date;
            ViewBag.ArticleStubIndex = model.GetArticleStubIndex();
            ViewBag.ArticlesIndex = articles;

            return View(model.GetArticleStubPage(date));

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
            DateTime date = DateTime.Now;
            if (id.HasValue)
                date = id.Value;

            ViewBag.Date = date;

            var model = new PostworthyArticleModel(PrimaryUser);
            model.ExcludeArticleStub(date, slug);

            HttpResponse.RemoveOutputCacheItem(Url.RouteUrl(Request.RequestContext.RouteData.Values));

            return RedirectToAction("Index", new { id = !id.HasValue ? null : (id.Value.ToShortDateString().Replace('/', '-')) });
        }

        public ActionResult Videos(DateTime? id)
        {
            DateTime date = DateTime.Now;
            if (id.HasValue)
                date = id.Value;

            ViewBag.Date = date;

            var model = new PostworthyArticleModel(PrimaryUser);
            var page = model.GetArticleStubPage(date);

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

            var model = new PostworthyArticleModel(PrimaryUser);
            var page = model.GetArticleStubPage(date);

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

            var model = new PostworthyArticleModel(PrimaryUser);
            var page = model.GetArticleStubPage(date);
            var photoStubs = page.ArticleStubs.Where(s => IMAGE_DOMAINS.Contains(s.Link.Authority.ToLower())).ToList();

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
        public ActionResult Archive()
        {
            //if (MobileHelper.IsMobileDevice(Request.UserAgent)) return RedirectToAction("about", "mobile");
            ViewBag.Archive = true;
            ViewBag.Home = false;
            var model = new PostworthyArticleModel(PrimaryUser);
            return View(model.GetArticleStubIndex());
        }

        public ActionResult About()
        {
            //if (MobileHelper.IsMobileDevice(Request.UserAgent)) return RedirectToAction("about", "mobile");
            ViewBag.About = true;
            ViewBag.Home = false;
            return View(PrimaryUser);
        }

        public ActionResult Image(string id)
        {
            var base64 = (string)System.Web.HttpContext.Current.Cache[id];
            if (!string.IsNullOrEmpty(base64))
                return File(Convert.FromBase64String(base64), "image/jpg");
            else
                throw new HttpException(404, id + " not found");
        }

        public ActionResult Out(DateTime id, string slug)
        {
            var model = new PostworthyArticleModel(PrimaryUser);
            var articles = model.GetArticleStubPage(id).ArticleStubs;
            var article = articles
                .Where(x => x.GetSlug().ToLower() == slug.ToLower())
                .FirstOrDefault();
            ViewBag.OriginalPage = "~/" + id.ToShortDateString().Replace("/", "-");
            if (article != null)
            {
                if (Request.UrlReferrer != null && Request.UrlReferrer.Authority != Request.Url.Authority)
                    return View(article);
                else
                {
                    ViewBag.Outbound = article.Link.ToString();
                    return View();
                }
            }
            else
            {
                ViewBag.OriginalPage = "~/";
                ViewBag.Outbound = Url.Content("~/");
                Response.StatusCode = 404;
                return View();
            }
        }
    }
}
