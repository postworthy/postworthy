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
using LinqToTwitter;
using System.Threading.Tasks;

namespace Postworthy.Web.Controllers
{
    public class AccountController : AsyncController
    {
        protected override void Initialize(System.Web.Routing.RequestContext requestContext)
        {
            base.Initialize(requestContext);
            var user = UsersCollection.PrimaryUsers().Where(u => u.IsPrimaryUser).FirstOrDefault();
            if (user != null)
                ViewBag.Brand = user.SiteName;
        }

        //
        // GET: /Account/LogOn

        public async Task<ActionResult> LogOn(bool complete = false)
        {
            //return View();
            if (!Request.IsAuthenticated)
            {
                if (complete)
                {
                    var auth = new MvcAuthorizer
                    {
                        CredentialStore = new SessionStateCredentialStore()
                    };

                    await auth.CompleteAuthorizeAsync(Request.Url);

                    FormsAuthentication.SetAuthCookie(auth.CredentialStore.ScreenName, true);
                    PostworthyUser pm = UsersCollection.Single(auth.CredentialStore.ScreenName, force: true, addIfNotFound: true);
                    if (string.IsNullOrEmpty(pm.AccessToken) && string.IsNullOrEmpty(pm.OAuthToken))
                    {
                        pm.AccessToken = auth.CredentialStore.OAuthTokenSecret;
                        pm.OAuthToken = auth.CredentialStore.OAuthToken;
                        UsersCollection.Save();
                    }

                    return RedirectToAction("Index", new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional });
                }
                else
                {
                    //var auth = new MvcSignInAuthorizer
                    var auth = new MvcAuthorizer
                    {
                        CredentialStore = new SessionStateCredentialStore
                        {
                            ConsumerKey = ConfigurationManager.AppSettings["TwitterCustomerKey"],
                            ConsumerSecret = ConfigurationManager.AppSettings["TwitterCustomerSecret"]
                        }
                    };
                    string twitterCallbackUrl = Request.Url.ToString() + "?complete=true";
                    return await auth.BeginAuthorizationAsync(new Uri(twitterCallbackUrl));
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

            return RedirectToAction("BeginAsync");
        }
    }
}
