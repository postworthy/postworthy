using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Postworthy.Models.Twitter;
using Postworthy.Models.Repository;
using System.IO;
using System.Configuration;
using Postworthy.Models.Account;
using Microsoft.AspNet.SignalR.Client;

namespace Postworthy.Models.Streaming
{
    public class StandardProcessingStep : IProcessingStep
    {
        protected static object queue_push_lock = new object();
        protected static List<Tweet> queue_push = new List<Tweet>();
        protected TextWriter log;
        protected string secret;
        protected static int streamingHubConnectAttempts = 0;
        protected HubConnection hubConnection = null;
        protected IHubProxy streamingHub = null;

        public virtual void Init(TextWriter log)
        {
            this.log = log;
            secret = ConfigurationManager.AppSettings["TwitterCustomerSecret"];

            while (streamingHubConnectAttempts++ < 3)
            {
                if (streamingHubConnectAttempts > 1) System.Threading.Thread.Sleep(5000);

                log.WriteLine("{0}: Attempting To Connect To PushURL '{1}' (Attempt: {2})", DateTime.Now, ConfigurationManager.AppSettings["PushURL"], streamingHubConnectAttempts);
                hubConnection = (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["PushURL"])) ? new HubConnection(ConfigurationManager.AppSettings["PushURL"]) : null;

                if (hubConnection != null)
                {
                    try
                    {
                        streamingHub = hubConnection.CreateHubProxy("streamingHub");
                        hubConnection.StateChanged += new Action<Microsoft.AspNet.SignalR.Client.StateChange>(sc =>
                        {
                            if (sc.NewState == Microsoft.AspNet.SignalR.Client.ConnectionState.Connected)
                            {
                                log.WriteLine("{0}: Push Connection Established", DateTime.Now);
                                lock (queue_push_lock)
                                {
                                    if (queue_push.Count > 0)
                                    {
                                        log.WriteLine("{0}: Pushing {1} Tweets to Web Application", DateTime.Now, queue_push.Count());
                                        streamingHub.Invoke("Send", new StreamItem() { Secret = secret, Data = queue_push }).Wait();
                                        queue_push.Clear();
                                    }
                                }
                            }
                            else if (sc.NewState == Microsoft.AspNet.SignalR.Client.ConnectionState.Disconnected)
                                log.WriteLine("{0}: Push Connection Lost", DateTime.Now);
                            else if (sc.NewState == Microsoft.AspNet.SignalR.Client.ConnectionState.Reconnecting)
                                log.WriteLine("{0}: Reestablishing Push Connection", DateTime.Now);
                            else if (sc.NewState == Microsoft.AspNet.SignalR.Client.ConnectionState.Connecting)
                                log.WriteLine("{0}: Establishing Push Connection", DateTime.Now);

                        });
                        var startHubTask = hubConnection.Start();
                        startHubTask.Wait();
                        if (!startHubTask.IsFaulted) break;
                    }
                    catch (Exception ex)
                    {
                        hubConnection = null;
                        log.WriteLine("{0}: Error: {1}", DateTime.Now, ex.ToString());
                    }
                }
            }
        }

        public virtual Task<IEnumerable<Tweet>> ProcessItems(IEnumerable<Tweet> tweets)
        {
            return Task<IEnumerable<Tweet>>.Factory.StartNew(new Func<IEnumerable<Tweet>>(() =>
            {
                var tp = new TweetProcessor(tweets, true);
                tp.Start();

                StoreInRepository(tweets);

                if (hubConnection != null && streamingHub != null)
                {

                    int retweetThreshold = UsersCollection.PrimaryUser().RetweetThreshold;
                    tweets = tweets.Where(t => t.RetweetCount >= retweetThreshold).ToArray();
                    if (hubConnection.State == Microsoft.AspNet.SignalR.Client.ConnectionState.Connected)
                    {
                        if (tweets.Count() > 0)
                        {
                            log.WriteLine("{0}: Pushing {1} Tweets to Web Application", DateTime.Now, tweets.Count());
                            streamingHub.Invoke("Send", new StreamItem() { Secret = secret, Data = tweets }).Wait();
                        }
                    }
                    else
                    {
                        lock (queue_push_lock)
                        {
                            queue_push.AddRange(tweets);
                        }
                    }
                }

                return tweets;
            }));
        }

        protected virtual void StoreInRepository(IEnumerable<Tweet> tweets)
        {
            tweets
                .GroupBy(t => t.User.Identifier.ScreenName)
                .ToList()
                .ForEach(g =>
                {
                    CachedRepository<Tweet>.Instance.Save(g.Key + TwitterModel.TWEETS, g.OrderBy(t => t.CreatedAt).Select(t => t).ToList());
                    log.WriteLine("{0}: {1} Tweets Saved for {2}", DateTime.Now, g.Count(), g.Key);
                });

            //CachedRepository<Tweet>.Instance.FlushChanges();
        }

        public void Shutdown()
        {

        }
    }
}
