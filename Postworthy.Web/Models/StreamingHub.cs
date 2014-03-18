using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;
using System.Configuration;
using Postworthy.Models.Streaming;
using System.Web.Mvc;
using Postworthy.Models.Account;
using Postworthy.Models.Twitter;
using System.IO;
using System.Web.Routing;
using Postworthy.Web.Controllers;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;

namespace Postworthy.Web.Models
{
    public class StreamingHub : Hub
    {
        public ControllerContext HomeContext { get; set; }

        public StreamingHub()
            : base()
        {
            if (HttpContext.Current != null)
            {
                HttpContextBase httpContextBase = new HttpContextWrapper(HttpContext.Current);
                RouteData routeData = new RouteData();
                routeData.Values.Add("controller", "Home");
                HomeContext = new ControllerContext(new RequestContext(httpContextBase, routeData), new HomeController());
            }
        }

        public Task Disconnect()
        {
            return Groups.Remove(Context.ConnectionId, "web")
                .ContinueWith(new Action<Task>(x => { Groups.Remove(Context.ConnectionId, "other"); }));
        }

        public Task Connect()
        {
            if (HttpContext.Current != null && HttpContext.Current.Request.UrlReferrer != null)
                return Groups.Add(Context.ConnectionId, "web");
            else
                return Groups.Add(Context.ConnectionId, "other");
        }

        public Task Reconnect(IEnumerable<string> groups)
        {
            if (HttpContext.Current != null && HttpContext.Current.Request.UrlReferrer != null)
                return Groups.Add(Context.ConnectionId, "web");
            else
                return Groups.Add(Context.ConnectionId, "other");
        }

        private void Update(List<string> data)
        {
            Clients.Client("web").update(data);
        }

        public void Send(StreamItem item)
        {
            if (item.Secret == ConfigurationManager.AppSettings["TwitterCustomerSecret"])
            {
                var screenname = UsersCollection.PrimaryUser().TwitterScreenName;
                var topTweets = TwitterModel.Instance.Tweets(screenname).Take(50);
                if (topTweets != null)
                {
                    var tweetsToSend = item.Data.Where(t => t.TweetRank >= topTweets.Min(x => x.TweetRank) && !topTweets.Contains(t)).OrderBy(t => t.CreatedAt);
                    if (tweetsToSend.Count() > 0)
                    {
                        int index = tweetsToSend.Count();
                        List<string> returnValues = new List<string>();
                        List<string> returnValuesMobile = new List<string>();
                        ViewEngineResult viewResult = ViewEngines.Engines.FindPartialView(HomeContext, "_Item");

                        var tweetCache = TwitterModel.Instance.PrimaryUserTweetCache;
                        if (tweetCache != null) tweetCache.AddRange(tweetsToSend);

                        foreach (var tweet in tweetsToSend)
                        {

                            var ViewData = new ViewDataDictionary<ItemData>(new ItemData()
                            {
                                Model = tweet,
                                index = index--,
                                isTop10 = false,
                                isTop20 = false,
                                isTop30 = false,
                                randomImage = tweet.Links.Where(l => l.Image != null).OrderBy(x => Guid.NewGuid()).FirstOrDefault(),
                                hasVideo = tweet.Links.Where(l => l.Video != null).Count() > 0,
                                topN = ""
                            });
                            using (StringWriter sw = new StringWriter())
                            {
                                ViewContext viewContext = new ViewContext(HomeContext, viewResult.View, ViewData, new TempDataDictionary(), sw);
                                viewResult.View.Render(viewContext, sw);

                                returnValues.Add(sw.GetStringBuilder().ToString());
                            }
                        }
                        Update(returnValues);
                    }
                }
            }
        }
    }
}