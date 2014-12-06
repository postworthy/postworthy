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
using System.Net;
using System.Drawing;

namespace Postworthy.Web.Controllers
{

    public class ArticleController : Controller
    {
        private const string OLD_URL = "oldurl";
        private const int PAGE_SIZE = 10;
        protected PostworthyUser PrimaryUser { get; set; }
        protected override void Initialize(System.Web.Routing.RequestContext requestContext)
        {
            base.Initialize(requestContext);
            ViewBag.Article = true;
            PrimaryUser = UsersCollection.PrimaryUsers().Where(u => u.IsPrimaryUser).FirstOrDefault();
            if (PrimaryUser != null)
                ViewBag.Brand = PrimaryUser.SiteName;
        }

        [OutputCache(VaryByParam = "id,slug", Duration = 300)]
        public ActionResult Index(int id = 0, string slug = "")
        {
            slug = slug.ToLower();
            ViewBag.Slug = slug;
            ViewBag.Page = id;
            ViewBag.PageSize = PAGE_SIZE;

            var model = new PostworthyArticleModel(PrimaryUser);
            int pageCount = 0;
            var viewModel = model.PagedArticles(PAGE_SIZE * id, PAGE_SIZE, slug, out pageCount);

            ViewBag.Tags = model.GetArticleIndex().Articles.SelectMany(x => x.Tags).Distinct().Select(x => GetTagLink(x));
            ViewBag.PageCount = pageCount;

            return View(viewModel);
        }

        [OutputCache(VaryByParam = "id,slug", Duration = 86400)]
        public ActionResult Details(uint id, string slug)
        {
            Article article = null;

            var model = new PostworthyArticleModel(PrimaryUser);

            if (slug != "p")
            {
                article = model.GetArticle(id);
                if (article != null)
                    ViewBag.RelatedArticles = model.Articles(x => x.ID() != article.ID() && x.Tags.Any(y => article.Tags.Where(z => z.ToLower() == y.ToLower()).Count() > 0)).Where(x => x != null).Take(3).ToList();
                else
                    ViewBag.RelatedArticles = null;
            }
            else
            {
                var oldURL = (Session[id.ToString()] ?? "").ToString().ToLower();

                if (!string.IsNullOrEmpty(oldURL))
                {
                    article = model.Articles(x => x.MetaData.Where(y => y.Key == OLD_URL && y.Value.ToLower() == oldURL).Count() > 0).FirstOrDefault();

                    if (article != null)
                        return RedirectPermanent("~/Article/Details/" + article.ID() + "/" + article.GetSlug());
                }
            }

            if (article != null)
            {
                article.Tags = article.Tags.Select(x => GetTagLink(x)).ToList();
                ViewBag.Tags = model.GetArticleIndex().Articles.SelectMany(x => x.Tags).Distinct().Select(x => GetTagLink(x));
                return View(article);
            }
            else
                return RedirectToAction("Index", new { id = 0 });
        }

        public ActionResult Admin()
        {
            var model = new PostworthyArticleModel(PrimaryUser);
            return View(model.GetArticleIndex());
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

                var model = new PostworthyArticleModel(PrimaryUser);

                model.SaveArticle(article);

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
            var model = new PostworthyArticleModel(PrimaryUser);
            var article = model.GetArticle(id.GetUintHashCode());

            if (article != null)
                return View("Create", article);
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

                var model = new PostworthyArticleModel(PrimaryUser);
                model.EditArticle(article);

                var route = @Url.RouteUrl("Article", new { controller = "article", action = "details", id = article.ID(), slug = article.GetSlug() });

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
            var model = new PostworthyArticleModel(PrimaryUser);
            model.DeleteArticle(id.GetUintHashCode());

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
            {
                var src = imgNodes.First().Attributes["src"].Value;
                if (!src.ToLower().StartsWith("data:"))
                    src = GetImageData(src) ?? src;
                article.Images.Add(src);
            }
        }

        private static string GetImageData(string imgSrc)
        {
            if (!string.IsNullOrEmpty(imgSrc))
            {
                try
                {
                    var request = HttpWebRequest.Create(imgSrc);
                    request.Timeout = 5000;
                    using (var response = request.GetResponse())
                    {
                        var img = (Bitmap)Bitmap.FromStream(response.GetResponseStream());
                        return "data:image/jpg;base64," + ImageManipulation.EncodeImage(img);
                    }
                }
                catch { return null; }
            }
            else
                return null;
        }

        private string GetTagLink(string x)
        {
            return "<a href=\"" + Url.RouteUrl("Article", new { controller = "Article", action = "Index", id = 0, slug = x.Replace("&", "").Replace(" ", "-").Replace(".", "-") }) + "\" title=\"" + x + "\">" + x + "</a>";
        }
    }
}
