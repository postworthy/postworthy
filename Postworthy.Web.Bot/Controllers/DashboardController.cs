using Postworthy.Web.Bot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Postworthy.Models.Account;

namespace Postworthy.Web.Bot.Controllers
{
    public class DashboardController : Controller
    {
        //
        // GET: /Dashboard/

        [AuthorizePrimaryUser]
        public ActionResult Index()
        {
            var model = new DashboardModel(UsersCollection.Single(User.Identity.Name));
            return View(model);
        }

    }
}
