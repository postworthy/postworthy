using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Postworthy.Web.Models
{
    public class MobileHelper
    {
        private static string[] mobileDevices = new string[] { "iphone", "ipod", "android", "ppc", "windows ce", "blackberry", "opera mini", "mobile", "palm", "portable", "opera mobi" };
        private static string[] ignore = new string[] { "ipad" };

        public static bool IsMobileDevice(string userAgent)
        {
            if (!string.IsNullOrEmpty(userAgent))
            {
                userAgent = userAgent.ToLower();
                if (ignore.Any(x => userAgent.Contains(x)))
                    return false;
                else
                    return mobileDevices.Any(x => userAgent.Contains(x));
            }
            else 
                return false;
        }
    }
}