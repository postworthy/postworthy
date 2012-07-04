using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using Postworthy.Models;
using System.Configuration;
using System.Xml.Serialization;
using System.IO;
using LinqToTwitter;
using Postworthy.Models.Account;
using Postworthy.Models.Twitter;

namespace Postworthy.Web.Controllers
{
    public class AccountController : Controller
    {
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

        //
        // GET: /Account/LogOn

        public ActionResult LogOn()
        {
            //return View();
            if (!Request.IsAuthenticated)
            {
                var credentials = new InMemoryCredentials();
                credentials.ConsumerKey = ConfigurationManager.AppSettings["TwitterCustomerKey"];
                credentials.ConsumerSecret = ConfigurationManager.AppSettings["TwitterCustomerSecret"];
                var auth = new MvcAuthorizer { Credentials = credentials };
                auth.CompleteAuthorization(Request.Url);
                if (!auth.IsAuthorized)
                    return auth.BeginAuthorization(Request.Url);
                else
                {
                    FormsAuthentication.SetAuthCookie(auth.ScreenName, true);
                    PostworthyUser pm = UsersCollection.Single(auth.ScreenName, addIfNotFound: true);
                    if (string.IsNullOrEmpty(pm.AccessToken) && string.IsNullOrEmpty(pm.OAuthToken))
                    {
                        pm.AccessToken = auth.Credentials.AccessToken;
                        pm.OAuthToken = auth.Credentials.OAuthToken;
                        UsersCollection.Save();
                    }
                    return RedirectToAction("Index", new { site = auth.ScreenName, controller = "Home", action = "Index", id = UrlParameter.Optional });
                }
            }
            else
                return RedirectToAction("Index", new { site = User.Identity.Name, controller = "Home", action = "Index", id = UrlParameter.Optional });
        }

        //
        // GET: /Account/LogOff

        public ActionResult LogOff()
        {
            var site = User.Identity.Name;

            FormsAuthentication.SignOut();

            return RedirectToAction("Index", new { site = site, controller = "Home", action = "Index", id = UrlParameter.Optional });
        }

        //
        // GET: /Account/Register

        public ActionResult Register()
        {
            FormsAuthentication.SignOut();

            return RedirectToAction("LogOn");
        }

        //
        // POST: /Account/Register

        [Authorize]
        public ActionResult Personalization()
        {
            PostworthyUser model = UsersCollection.Single(User.Identity.Name);
            return View(model);
        }

        [Authorize]
        [HttpPost]
        public ActionResult Personalization(PostworthyUser model)
        {

            var prevModel = UsersCollection.Single(User.Identity.Name);

            prevModel.SiteName = model.SiteName;
            prevModel.About = model.About;
            prevModel.IncludeFriends = model.IncludeFriends;
            prevModel.OnlyTweetsWithLinks = model.OnlyTweetsWithLinks;

            UsersCollection.Save();

            return View(model);
        }

        [Authorize]
        public ActionResult Friends()
        {
            return View(TwitterModel.Instance.Friends(User.Identity.Name));
        }
    }
}
