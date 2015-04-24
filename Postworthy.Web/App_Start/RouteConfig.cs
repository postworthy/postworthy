using System;
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
                name: "Front Page (SEO)",
                url: "frontpage",
                defaults: new { controller = "Home", action = "FrontPage" }
            );
            routes.MapRoute(
                name: "Archive (SEO)",
                url: "archive",
                defaults: new { controller = "Home", action = "Archive" }
            );
            routes.MapRoute(
                name: "About (SEO)",
                url: "about",
                defaults: new { controller = "Home", action = "About" }
            );
            routes.MapRoute(
                name: "Article Index (SEO)",
                url: "articles/{id}/",
                defaults: new { controller = "Article", action = "Index", id = 0, slug = UrlParameter.Optional }
            );
            routes.MapRoute(
                name: "Article Index w/ Slug (SEO)",
                url: "articles/tag/{slug}/{id}/",
                defaults: new { controller = "Article", action = "Index", id = 0, slug = UrlParameter.Optional }
            );
            routes.MapRoute(
                name: "Article Details (SEO)",
                url: "article/{slug}_{id}",
                defaults: new { controller = "Article", action = "Details", id = UrlParameter.Optional, slug = UrlParameter.Optional, seo = true }
            );
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
                defaults: new { controller = "Article", action = "Details", id = UrlParameter.Optional, slug = UrlParameter.Optional, seo = false }
            );
        }
    }
}
