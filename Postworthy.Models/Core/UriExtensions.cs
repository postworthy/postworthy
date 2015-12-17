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
        private const string URL_FACEBOOK_SHARE_COUNT_ENDPOINT = "https://api.facebook.com/method/fql.query?format=json&query=select%20%20like_count%20from%20link_stat%20where%20url=%22{0}%22";

        public static HttpWebRequest GetWebRequest(this Uri uri, int connectionTimeout = 100000, int readwriteTimeout = 100000)
        {
            var webReq = (HttpWebRequest)WebRequest.Create(uri);
            webReq.Timeout = connectionTimeout;
            webReq.ReadWriteTimeout = readwriteTimeout;
            webReq.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/536.6 (KHTML, like Gecko) Chrome/20.0.1092.0 Safari/536.6";
            webReq.KeepAlive = true;
            webReq.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            webReq.Headers.Add("Accept-Charset", "ISO-8859-1,utf-8;q=0.7,*;q=0.3");
            webReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

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
            //var jss = new JavaScriptSerializer();
            //var twtcnt = new Uri(URL_TWEET_COUNT_ENDPOINT + HttpUtility.UrlEncode(uri.ToString()));
            //try
            //{
            //    var req = twtcnt.GetWebRequest();
            //    using (var resp = req.GetResponse())
            //    {
            //        using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.Default))
            //        {
            //            try
            //            {
            //                var utc = jss.Deserialize<UrlTweetCount>(reader.ReadToEnd());
            //                return utc.count;
            //            }
            //            catch { return 0; }
            //        }
            //    }
            //}
            //catch { return 0; }

            return 100;
        }

        public static int GetFacebookShareCount(this Uri uri)
        {
            var jss = new JavaScriptSerializer();
            var fbshare = new Uri(string.Format(URL_FACEBOOK_SHARE_COUNT_ENDPOINT, HttpUtility.UrlEncode(uri.ToString())));
            var req = fbshare.GetWebRequest();
            using (var resp = req.GetResponse())
            {
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.Default))
                {
                    try
                    {
                        var usc = jss.Deserialize<UrlFaceBookShareCount[]>(reader.ReadToEnd());
                        return usc.FirstOrDefault().like_count;
                    }
                    catch { return 0; }
                }
            }
        }
    }
}