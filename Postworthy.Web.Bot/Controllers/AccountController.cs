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
            var user = UsersCollection.PrimaryUser();
            if (user != null)
                ViewBag.Brand = user.SiteName;
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
                    return RedirectToAction("Index", new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional });
                }
            }
            else
                return RedirectToAction("Index", new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional });
        }

        //
        // GET: /Account/LogOff

        public ActionResult LogOff()
        {
            FormsAuthentication.SignOut();

            return RedirectToAction("Index", new { controller = "Home", action = "Index", id = UrlParameter.Optional });
        }

        //
        // GET: /Account/Register

        public ActionResult Register()
        {
            FormsAuthentication.SignOut();

            return RedirectToAction("LogOn");
        }
    }
}
