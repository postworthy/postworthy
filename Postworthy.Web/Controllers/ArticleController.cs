using Postworthy.Models.Account;
using Postworthy.Models.Repository;
using Postworthy.Models.Twitter;
using Postworthy.Models.Web;
using Postworthy.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Postworthy.Models.Core;

namespace Postworthy.Web.Controllers
{

    public class ArticleController : Controller
    {
        private const int PAGE_SIZE = 10;
        protected PostworthyUser PrimaryUser { get; set; }
        protected override void Initialize(System.Web.Routing.RequestContext requestContext)
        {
            base.Initialize(requestContext);
            PrimaryUser = UsersCollection.PrimaryUsers().Where(u => u.IsPrimaryUser).FirstOrDefault();
            if (PrimaryUser != null)
                ViewBag.Brand = PrimaryUser.SiteName;
        }

        [OutputCache(VaryByParam = "id", Duration = 300)]
        public ActionResult Index(int id = 0)
        {
            ViewBag.Page = id;
            ViewBag.PageSize = PAGE_SIZE;

            var index = CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX).FirstOrDefault() ?? new ArticleIndex();

            ViewBag.PageCount = (int)Math.Ceiling(index.Articles.Count / (PAGE_SIZE * 1.0));

            var items = index.Articles.OrderByDescending(i => i.Published).Skip(PAGE_SIZE * id).Take(PAGE_SIZE);

            var viewModel = new List<Article>();

            foreach (var item in items)
            {
                var articles = CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                    .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + item.DayTag).ToList();

                var article = articles.Where(x => x.UniqueKey == item.Key).FirstOrDefault();
                if (article != null)
                    viewModel.Add(article);
            }

            return View(viewModel);
        }

        [OutputCache(VaryByParam = "id", Duration = 86400)]
        public ActionResult Details(int id, string slug)
        {
            var index = CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX).FirstOrDefault() ?? new ArticleIndex();

            var articleIndex = index.Articles.Where(x => x.Key.GetHashCode() == id).FirstOrDefault();

            if (articleIndex != null)
            {
                var articles = CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                    .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + articleIndex.DayTag).ToList();

                var article = articles.Where(x => x.UniqueKey == articleIndex.Key).FirstOrDefault();

                return View(article);
            }
            else
                return RedirectToAction("Index", new { id = 0 });
        }

        public ActionResult Admin()
        {
            var index = CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX).FirstOrDefault() ?? new ArticleIndex();
            return View(index);
        }


        [AuthorizePrimaryUser]
        public ActionResult Create()
        {
            return View(new Article());
        }

        [AuthorizePrimaryUser]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult Create(Article article)
        {
            try
            {
                SplitTags(article);
                GetImages(article);

                article.PublishedBy = PrimaryUser.TwitterScreenName;
                article.PublishedDate = DateTime.Now;

                string dayTag = "_" + article.PublishedDate.ToShortDateString();

                var index = CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                    .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX).FirstOrDefault() ?? new ArticleIndex();

                CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                    .Save(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + dayTag, article);

                index.Articles.Add(new ArticleIndex.Index(article.PublishedDate.ToFileTimeUtc(), article.Title, article.UniqueKey, article.Tags));

                CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                    .Save(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX, index);

                return Json(new { result = "success" }, JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new { result = "failure" }, JsonRequestBehavior.AllowGet);
            }
        }

        [AuthorizePrimaryUser]
        public ActionResult Edit(string id)
        {
            var index = CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                    .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX).FirstOrDefault();

            var articleIndex = index.Articles.Where(x => x.Key == id).FirstOrDefault();

            if (articleIndex != null)
            {
                var articles = CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                    .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + articleIndex.DayTag).ToList();

                var article = articles.Where(x => x.UniqueKey == articleIndex.Key).FirstOrDefault();

                return View("Create", article);
            }
            else
                return RedirectToAction("Admin", new { id = PrimaryUser.TwitterScreenName });
        }

        [AuthorizePrimaryUser]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult Edit(string id, Article article)
        {
            try
            {
                //article.PublishedBy = PrimaryUser.TwitterScreenName;
                //article.PublishedDate = DateTime.Now;
                SplitTags(article);
                GetImages(article);

                string dayTag = "_" + article.PublishedDate.ToShortDateString();

                CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                    .Save(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + dayTag, article);

                var index = CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                    .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX).FirstOrDefault();

                var articleIndex = index.Articles.Where(x => x.Key == article.UniqueKey).FirstOrDefault();
                articleIndex.Title = article.Title;
                articleIndex.Tags = article.Tags;

                CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                    .Save(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX, index);

                var route = @Url.RouteUrl("Article", new { controller = "article", action = "details", id = article.UniqueKey.GetHashCode().ToString(), slug = article.GetSlug() });

                Response.RemoveOutputCacheItem(route);

                return Json(new { result = "success" }, JsonRequestBehavior.AllowGet);
            }
            catch
            {
                return Json(new { result = "failure" }, JsonRequestBehavior.AllowGet);
            }
        }

        [AuthorizePrimaryUser]
        public ActionResult Delete(string id)
        {
            var index = CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                    .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX).FirstOrDefault();

            var articleIndex = index.Articles.Where(x => x.Key == id).FirstOrDefault();

            if (articleIndex != null)
            {
                var article = CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                    .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + articleIndex.DayTag, where: x => x.UniqueKey == articleIndex.Key).FirstOrDefault();

                if (article != null)
                {
                    CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                        .Delete(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + articleIndex.DayTag, article);

                    index.Articles.Remove(articleIndex);

                    CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                        .Save(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX, index);
                }
            }

            return RedirectToAction("Admin", new { id = PrimaryUser.TwitterScreenName });
        }

        private void SplitTags(Article article)
        {
            article.Tags = article.Tags.SelectMany(t => t.Split(',')).Select(t => t.Trim()).ToList();
        }

        private void GetImages(Article article)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(article.Content);
            var imgNodes = doc.DocumentNode.SelectNodes("//img");
            if (imgNodes != null && imgNodes.Count > 0)
                article.Images.Add(imgNodes.First().Attributes["src"].Value);
        }
    }
}
