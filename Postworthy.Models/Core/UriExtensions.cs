using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.Web.Script.Serialization;
using Postworthy.Models.Twitter;
using System.IO;
using System.Text;

namespace Postworthy.Models.Core
{
    public static class UriExtensions
    {
        private const string URL_TWEET_COUNT_ENDPOINT = "http://urls.api.twitter.com/1/urls/count.json?url=";

        public static HttpWebRequest GetWebRequest(this Uri uri)
        {
            var webReq = (HttpWebRequest)WebRequest.Create(uri);
            //webReq.Timeout = 300000;
            webReq.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/536.6 (KHTML, like Gecko) Chrome/20.0.1092.0 Safari/536.6";
            webReq.KeepAlive = true;
            webReq.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            return webReq;
        }

        public static int ContentLength(this Uri uri)
        {
            var req = uri.GetWebRequest();
            req.Method = "HEAD";
            using (WebResponse resp = req.GetResponse())
            {
                int ContentLength;
                if (int.TryParse(resp.Headers.Get("Content-Length"), out ContentLength))
                    return ContentLength;
                else
                    return 0;
            }
        }

        public static Uri HtmlContentUri(this Uri uri)
        {
            var req = uri.GetWebRequest();
            req.Method = "HEAD";
            try
            {
                using (WebResponse resp = req.GetResponse())
                {
                    if (resp.Headers.Get("Content-Type").Contains("text/html"))
                        return resp.ResponseUri;
                }

            }
            catch (WebException wex) 
            { 
                WebResponse resp = wex.Response;
                if (resp != null && resp.Headers.Get("Content-Type").Contains("text/html"))
                        return resp.ResponseUri;
            }
            catch { }

            return null;
        }

        public static Uri ImageContentUri(this Uri uri)
        {
            var req = uri.GetWebRequest();
            req.Method = "HEAD";
            try
            {
                using (WebResponse resp = req.GetResponse())
                {
                    if (resp.Headers.Get("Content-Type").Contains("image/"))
                        return resp.ResponseUri;
                }
            }
            catch (WebException wex)
            {
                WebResponse resp = wex.Response;
                if (resp != null && resp.Headers.Get("Content-Type").Contains("image/"))
                    return resp.ResponseUri;
            }
            catch { }

            return null;
        }

        public static int GetTweetCount(this Uri uri)
        {
            var jss = new JavaScriptSerializer();
            var twtcnt = new Uri(URL_TWEET_COUNT_ENDPOINT + HttpUtility.UrlEncode(uri.ToString()));
            var req = twtcnt.GetWebRequest();
            using (var resp = req.GetResponse())
            {
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.Default))
                {
                    try
                    {
                        var utc = jss.Deserialize<UrlTweetCount>(reader.ReadToEnd());
                        return utc.count;
                    }
                    catch { return 0; }
                }
            }
        }
    }
}