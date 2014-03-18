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
using Postworthy.Web.Models;
using System.Threading.Tasks;
using LinqToTwitter;

namespace Postworthy.Web.Controllers
{
    public class AccountController : AsyncController
    {
        protected PostworthyUser PrimaryUser { get; set; }
        protected override void Initialize(System.Web.Routing.RequestContext requestContext)
        {
            base.Initialize(requestContext);
            PrimaryUser = UsersCollection.PrimaryUsers().Where(u => u.IsPrimaryUser).FirstOrDefault();
            if (PrimaryUser != null)
                ViewBag.Brand = PrimaryUser.SiteName;
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
                    PostworthyUser pm = UsersCollection.Single(auth.CredentialStore.ScreenName, addIfNotFound: true);
                    if (string.IsNullOrEmpty(pm.AccessToken) && string.IsNullOrEmpty(pm.OAuthToken))
                    {
                        pm.AccessToken = auth.CredentialStore.OAuthTokenSecret;
                        pm.OAuthToken = auth.CredentialStore.OAuthToken;
                        UsersCollection.Save();
                    }

                    return RedirectToAction("Index", new { controller = "Home", action = "Index", id = UrlParameter.Optional });
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
                return RedirectToAction("Index", new { controller = "Home", action = "Index", id = UrlParameter.Optional });
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

        //
        // POST: /Account/Register

        [AuthorizePrimaryUser]
        public ActionResult Personalization()
        {  
            PostworthyUser model = UsersCollection.Single(User.Identity.Name);
            return View(model);
        }

        [ValidateInput(false)]
        [AuthorizePrimaryUser]
        [HttpPost]
        public ActionResult Personalization(FormCollection form)
        {

            var prevModel = UsersCollection.Single(User.Identity.Name);

            prevModel.SiteName = form["SiteName"];
            prevModel.About = form["About"];
            prevModel.Track = form["Track"];
            prevModel.IncludeFriends = bool.Parse(form["IncludeFriends"].Split(',').First());
            prevModel.OnlyTweetsWithLinks = bool.Parse(form["OnlyTweetsWithLinks"].Split(',').First());
            prevModel.AnalyticsScript = form["AnalyticsScript"];
            prevModel.AdScript = form["AdScript"];
            prevModel.MobileAdScript = form["MobileAdScript"];
            prevModel.RetweetThreshold = int.Parse(!string.IsNullOrEmpty(form["RetweetThreshold"]) ? form["RetweetThreshold"] : "5");

            UsersCollection.Save();

            return View(prevModel);
        }

        [AuthorizePrimaryUser]
        public ActionResult Friends()
        {
            return View(TwitterModel.Instance(PrimaryUser.TwitterScreenName).Friends(User.Identity.Name));
        }
    }
}
