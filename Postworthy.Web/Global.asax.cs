using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using Postworthy.Models.Core;
using System.Web.Caching;

namespace Postworthy.Web
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
        public override string GetVaryByCustomString(HttpContext context, string arg)
        {
            DateTime id = DateTime.MinValue;
            string time = "0";
            if(DateTime.TryParse(context.Request.Url.ToString().Split('?').First().Split('/').Last(), out id))
                time = id.StartOfDay().ToFileTimeUtc().ToString();
            if (arg == "User")
                return "User=" + ((context.Request.IsAuthenticated) ? context.User.Identity.Name + "_" + time : "guest" + "_" + time);

            return base.GetVaryByCustomString(context, arg);
        }
    }
}
