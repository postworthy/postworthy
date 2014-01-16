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
using Postworthy.Models.Account;
using Postworthy.Models.Twitter;

namespace Postworthy.Web.Controllers
{
    public class AccountController : Controller
    {
        #region LinqToTwitter Workaround 1/16/2014 (http://linqtotwitter.codeplex.com/discussions/462676)
        private class MvcAuthorizer : LinqToTwitter.WebAuthorizer
        {
            public ActionResult BeginAuthorization()
            {
                return new MvcOAuthActionResult(this);
            }

            public new ActionResult BeginAuthorization(Uri callback)
            {
                this.Callback = callback;
                return new MvcOAuthActionResult(this);
            }
        }

        private class MvcOAuthActionResult : ActionResult
        {
            private readonly LinqToTwitter.WebAuthorizer webAuth;

            public MvcOAuthActionResult(LinqToTwitter.WebAuthorizer webAuth)
            {
                this.webAuth = webAuth;
            }

            public override void ExecuteResult(ControllerContext context)
            {
                webAuth.PerformRedirect = authUrl =>
                {

                    System.Web.HttpContext.Current.Response.Redirect(authUrl);
                };

                Uri callback =
                    webAuth.Callback == null ?
                        System.Web.HttpContext.Current.Request.Url :
                        webAuth.Callback;

                webAuth.BeginAuthorization(callback);
            }
        }
        #endregion

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
                var credentials = new LinqToTwitter.InMemoryCredentials();
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

            return RedirectToAction("Index", new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional });
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
