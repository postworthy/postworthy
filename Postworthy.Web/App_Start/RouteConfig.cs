﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace Postworthy.Web
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("{*favicon}", new { favicon = @"(.*/)?favicon.ico(/.*)?" });

            routes.MapRoute(
                name: "Shortest",
                url: "{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "Shorter",
                url: "{id}/{slug}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional, slug = UrlParameter.Optional }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
            routes.MapRoute(
                name: "Article",
                url: "{controller}/{action}/{id}/{slug}",
                defaults: new { controller = "Article", action = "Details", id = UrlParameter.Optional, slug = UrlParameter.Optional }
            );
        }
    }
}
