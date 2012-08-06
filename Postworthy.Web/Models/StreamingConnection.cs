using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using SignalR;
using Newtonsoft.Json;
using Postworthy.Models.Streaming;
using System.Configuration;
using Postworthy.Models.Twitter;
using Postworthy.Models.Account;
using System.Web.Mvc;
using System.IO;
using Postworthy.Web.Controllers;
using Postworthy.Web.Models;
using System.Web.Routing;

public class StreamingConnection : PersistentConnection
{
    public ControllerContext HomeContext { get; set; }
    public StreamingConnection()
        : base()
    {
      HttpContextBase httpContextBase = new HttpContextWrapper(HttpContext.Current);
      RouteData routeData = new RouteData();
      routeData.Values.Add("controller", "Home");
      HomeContext = new ControllerContext(new RequestContext(httpContextBase, routeData), new HomeController());
    }
    protected override Task OnReceivedAsync(IRequest request, string connectionId, string data)
    {
        var item = JsonConvert.DeserializeObject<StreamItem>(data);
        if (item.Secret == ConfigurationManager.AppSettings["TwitterCustomerSecret"])
        {
            var topTweets = TwitterModel.Instance.Tweets(UsersCollection.PrimaryUser().TwitterScreenName).Take(50);
            if (topTweets != null)
            {
                var tweetsToSend = item.Data.Where(t => t.TweetRank >= topTweets.Min(x => x.TweetRank) && !topTweets.Contains(t)).OrderBy(t=>t.CreatedAt);
                if (tweetsToSend.Count() > 0)
                {
                    int index = tweetsToSend.Count();
                    List<string> returnValues = new List<string>();
                    ViewEngineResult viewResult = ViewEngines.Engines.FindPartialView(HomeContext, "_Item");

                    foreach(var tweet in tweetsToSend)
                    {
                        using (StringWriter sw = new StringWriter())
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
                            
                            ViewContext viewContext = new ViewContext(HomeContext, viewResult.View, ViewData, new TempDataDictionary(), sw);
                            viewResult.View.Render(viewContext, sw);

                            returnValues.Add(sw.GetStringBuilder().ToString());
                        }
                    }
                    return Connection.Broadcast(returnValues);
                }
            }
        }
        
        return new Task(() => { /* Do Nothing */ });
    }
}