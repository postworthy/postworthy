using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Postworthy.Models;
using Postworthy.Models.Account;
using Postworthy.Models.Twitter;
using Postworthy.Web.Models;

namespace Postworthy.Web.Controllers
{
    public class HomeController : Controller
    {
        protected override void Initialize(System.Web.Routing.RequestContext requestContext)
        {
            base.Initialize(requestContext);
            var user = UsersCollection.PrimaryUser();
            if (user != null)
                ViewBag.Brand = user.SiteName;
        }

        public ActionResult Index()
        {
            return View(TwitterModel.Instance.Tweets(UsersCollection.PrimaryUser().TwitterScreenName));
        }

        [AuthorizePrimaryUser]
        [HttpPost]
        public ActionResult Tweet(string Tweet)
        {
            if (!string.IsNullOrEmpty(Tweet))
                TwitterModel.Instance.UpdateStatus(Tweet, User.Identity.Name);

            return RedirectToAction("Index");
        }

        /*public ActionResult Directory()
        {
            return View(UsersCollection.All());
        }*/

        public ActionResult About()
        {
            return View(UsersCollection.PrimaryUser());
        }
    }
}
