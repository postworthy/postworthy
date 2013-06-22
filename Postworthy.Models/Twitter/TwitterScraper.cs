using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using HtmlAgilityPack;

namespace Postworthy.Models.Twitter
{
    public static class TwitterScraper
    {
        private static string USER_AGENT = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/28.0.1468.0 Safari/537.36";
        public static string GetTwitterUrl(string url)
        {
            if (string.IsNullOrEmpty(ConfigurationManager.AppSettings["PrimaryUser"]) || string.IsNullOrEmpty(ConfigurationManager.AppSettings["Password"]))
                throw new Exception("PrimaryUser and Password settings must be present in the config file to use GetTwitterUrl");

            string result = string.Empty;
            CookieContainer cookieContainer = new CookieContainer();
            HttpWebRequest initialFetch = (HttpWebRequest)WebRequest.Create("http://twitter.com");
            initialFetch.UserAgent = USER_AGENT;
            initialFetch.CookieContainer = cookieContainer;

            using (var initialResponse = initialFetch.GetResponse())
            {
                var doc = new HtmlDocument();
                HtmlNode token = null;
                using (var reader = new StreamReader(initialResponse.GetResponseStream()))
                {
                    doc.Load(reader);
                    token = doc.DocumentNode.SelectNodes("//input[@name='authenticity_token']").FirstOrDefault();
                }
                
                var authenticityToken = token.GetAttributeValue("value", "");
                if (!string.IsNullOrEmpty(authenticityToken))
                {
                    HttpWebRequest loginRequest = (HttpWebRequest)WebRequest.Create("https://twitter.com/sessions");
                    loginRequest.UserAgent = USER_AGENT;
                    loginRequest.CookieContainer = cookieContainer;

                    loginRequest.Method = "POST";
                    loginRequest.ContentType = "application/x-www-form-urlencoded";

                    string postData = "session%5Busername_or_email%5D=" + ConfigurationManager.AppSettings["PrimaryUser"];
                    postData += "&session%5Bpassword%5D=" + ConfigurationManager.AppSettings["Password"];
                    postData += "&authenticity_token=" + authenticityToken;
                    postData += "&redirect_after_login=" + HttpUtility.UrlEncode(url);
                    byte[] data = new ASCIIEncoding().GetBytes(postData);

                    loginRequest.ContentLength = data.Length;

                    using (Stream stream = loginRequest.GetRequestStream())
                    {
                        stream.Write(data, 0, data.Length);
                    }

                    using (var loginResponse = (HttpWebResponse)loginRequest.GetResponse())
                    {
                        using (var reader = new StreamReader(loginResponse.GetResponseStream()))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }

            return result;
        }
    }
}
