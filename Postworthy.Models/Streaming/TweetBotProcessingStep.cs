using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using Postworthy.Models.Twitter;
using Postworthy.Models.Account;
using System.Threading.Tasks;
using System.IO;

namespace Postworthy.Models.Streaming
{
    public class TweetBotProcessingStep : IProcessingStep
    {
        protected string Message = "";
        public void Init(TextWriter log)
        {
            Message = ConfigurationManager.AppSettings["TweetBotMessage"];
            if (string.IsNullOrEmpty(Message))
                throw new ArgumentNullException("'TweetBotMessage' must be defined in the appSettings section of the configuration file!");
            else
                log.WriteLine("{0}: TweetBot will respond with: {1}", DateTime.Now, Message);
        }

        public Task<IEnumerable<Tweet>> ProcessItems(IEnumerable<Tweet> tweets)
        {
            return Task<IEnumerable<Tweet>>.Factory.StartNew(new Func<IEnumerable<Tweet>>(() =>
                {
                    var repliedTo = new List<Tweet>();
                    var user = UsersCollection.PrimaryUser();
                    foreach (var t in tweets)
                    {
                        if (t.User.Identifier.ScreenName.ToLower() != user.TwitterScreenName.ToLower())
                        {
                            TwitterModel.Instance.UpdateStatus("@" + t.User.Identifier.ScreenName + " " + Message, processStatus: false);
                            repliedTo.Add(t);
                        }
                    }
                    return repliedTo;
                }));
        }
    }
}
