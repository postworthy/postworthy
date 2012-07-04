using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Postworthy.Models;
using Postworthy.Models.Account;
using Postworthy.Models.Twitter;

namespace Postworthy.Web.Controllers
{
    public class HomeController : Controller
    {
        private const string POSTWORTHY = "postworthy";

        protected override void Initialize(System.Web.Routing.RequestContext requestContext)
        {
            base.Initialize(requestContext);
            var user = UsersCollection.Single(requestContext.RouteData.Values.SingleOrDefault(x => x.Key == "site").Value.ToString());
            if (user != null)
            {
                ViewBag.ScreenName = user.TwitterScreenName;
                ViewBag.Brand = user.SiteName;
            }
        }

        public ActionResult Index()
        {
            string screenName = ViewBag.ScreenName;
            if (!string.IsNullOrEmpty(screenName))
            {
                return View(TwitterModel.Instance.Tweets(screenName));
            }
            else
                return RedirectToAction("Register", new
                {
                    site = Request.RequestContext.RouteData.Values.SingleOrDefault(x => x.Key == "site").Value,
                    controller = "Account",
                    action = "Register",
                    id = UrlParameter.Optional
                });
        }

        [Authorize]
        [HttpPost]
        public ActionResult Tweet(string Tweet)
        {
            if (!string.IsNullOrEmpty(Tweet))
                TwitterModel.Instance.UpdateStatus(Tweet, User.Identity.Name);

            return RedirectToAction("Index");
        }

        public ActionResult Directory()
        {
            return View(UsersCollection.All());
        }

        public ActionResult About()
        {
            string screenName = ViewBag.ScreenName;
            if (!string.IsNullOrEmpty(screenName))
                return View(UsersCollection.Single(screenName));
            else
                return RedirectToAction("Register", new
                {
                    site = Request.RequestContext.RouteData.Values.SingleOrDefault(x => x.Key == "site").Value,
                    controller = "Account",
                    action = "Register",
                    id = UrlParameter.Optional
                });
        }
    }
}
