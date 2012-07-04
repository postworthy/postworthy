using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;

namespace Postworthy.Models.Core
{
    public static class Extensions
    {
        public static IEnumerable<TResult> SelectWithPrevious<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TSource, int, TResult> projection)
        {
            using (var iterator = source.GetEnumerator())
            {
                int index = 0;
                if (!iterator.MoveNext())
                {
                    yield break;
                }
                TSource previous = iterator.Current;
                while (iterator.MoveNext())
                {
                    yield return projection(previous, iterator.Current, index++);
                    previous = iterator.Current;
                }
            }
        }

        public static bool IsWithinAverageDifference(this IEnumerable<DateTime> dates, DateTime date = default(DateTime))
        {
            if (dates.Count() > 0)
            {
                if (date == default(DateTime)) 
                    date = DateTime.Now.ToUniversalTime();
                else 
                    date = date.ToUniversalTime();

                var ordered = dates.Select(x=>x.ToUniversalTime()).OrderBy(x => x);
                var current = date - ordered.First();

                if (dates.Count() > 2)
                {
                    var average = TimeSpan.FromSeconds(ordered.SelectWithPrevious((prev, cur, index) => { return index > 0 ? (cur - prev).Seconds : int.MinValue; }).Where(x => x != int.MinValue).Average());
                    return current < average;
                }
                else if (dates.Count() == 2)
                    return current < TimeSpan.FromSeconds(Math.Abs((dates.First() - dates.Skip(1).First()).TotalSeconds));
            }
            return true;
        }
    }

    public static class UriExtensions
    {
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
    }
}