using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using Postworthy.Models.Twitter;
using Postworthy.Models.Account;
using System.Threading.Tasks;
using System.IO;
using System.Collections;

namespace Postworthy.Models.Streaming
{
    public class TweetBotProcessingStep : IProcessingStep
    {
        private List<string> NoTweetList = new List<string>();
        private string[] Messages = null;
        public void Init(TextWriter log)
        {
            NoTweetList.Add(UsersCollection.PrimaryUser().TwitterScreenName.ToLower());
            Messages = Enumerable.Range(0, MessageSettings.Settings.Messages.Count - 1).Select(i => MessageSettings.Settings.Messages[i].Value).ToArray();
            if (Messages == null)
                throw new ArgumentNullException("'MessageSettings' must be defined in the app.config file!");
            else
                log.WriteLine("{0}: TweetBot will respond with: {1}", DateTime.Now, string.Join(Environment.NewLine, Messages));
        }

        public Task<IEnumerable<Tweet>> ProcessItems(IEnumerable<Tweet> tweets)
        {
            return Task<IEnumerable<Tweet>>.Factory.StartNew(new Func<IEnumerable<Tweet>>(() =>
                {
                    var repliedTo = new List<Tweet>();
                    foreach (var t in tweets)
                    {
                        string tweetedBy = t.User.Identifier.ScreenName.ToLower();
                        if (!NoTweetList.Any(x => x == tweetedBy) && !t.TweetText.ToLower().Contains(NoTweetList[0]))
                        {
                            //Dont want to keep hitting the same person over and over so add them to the ignore list
                            NoTweetList.Add(tweetedBy);
                            //If they were mentioned in a tweet they get ignored in the future just in case they reply
                            NoTweetList.AddRange(t.Status.Entities.UserMentions.Where(um => !string.IsNullOrEmpty(um.ScreenName)).Select(um => um.ScreenName)); 
                            //Randomly select response from list of possible responses
                            string message = Messages.OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                            //Tweet it
                            TwitterModel.Instance.UpdateStatus(message + " RT @" + t.User.Identifier.ScreenName + " " + t.TweetText, processStatus: false);

                            repliedTo.Add(t);
                        }
                        System.Threading.Thread.Sleep(120000); //Wait at least 2 minutes between tweets so it doesnt look bot-ish with fast retweets.
                    }
                    return repliedTo;
                }));
        }
    }
    public class MessageSettings : ConfigurationSection
    {
        private static MessageSettings messages = ConfigurationManager.GetSection("MessageSettings") as MessageSettings;

        public static MessageSettings Settings { get { return messages; } }

        [ConfigurationProperty("Messages", IsKey = true, IsRequired = true)]
        public MessageCollection Messages { get { return (MessageCollection)base["Messages"]; } }
    }

    public class MessageCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new Message();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((Message)element).Key;
        }

        public Message this[int idx]
        {
            get
            {
                return (Message)BaseGet(idx);
            }
        }
    }

    public class Message : ConfigurationElement
    {
        [ConfigurationProperty("key", IsKey = true, IsRequired = true)]
        public string Key { get { return (string)base["key"]; } set { base["key"] = value; } }
        [ConfigurationProperty("value", IsKey = true, IsRequired = true)]
        public string Value { get { return (string)base["value"]; } set { base["value"] = value; } }
    }
}

