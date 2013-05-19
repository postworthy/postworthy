using Postworthy.Web.Bot.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Postworthy.Web.Bot.Controllers
{
    public class DashboardController : Controller
    {
        //
        // GET: /Dashboard/

        [AuthorizePrimaryUser]
        public ActionResult Index()
        {
            var model = new DashboardModel();
            return View(model);
        }

    }
}
