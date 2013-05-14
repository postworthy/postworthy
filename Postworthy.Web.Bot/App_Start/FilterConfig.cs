using System.Web;
using System.Web.Mvc;

namespace Postworthy.Web.Bot
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}