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
        private bool OnlyWithMentions = false;

        public void Init(TextWriter log)
        {
            NoTweetList.Add(UsersCollection.PrimaryUser().TwitterScreenName.ToLower());
            Messages = Enumerable.Range(0, TweetBotSettings.Settings.Messages.Count - 1)
                .Select(i => TweetBotSettings.Settings.Messages[i].Value).ToArray();
            OnlyWithMentions = TweetBotSettings.Settings.Filters["OnlyWithMentions"] != null ? 
                TweetBotSettings.Settings.Filters["OnlyWithMentions"].Value : 
                false;
            if (Messages == null)
                throw new ArgumentNullException("'TweetBotSettings' section must be defined in the app.config file!");
            else
            {
                log.WriteLine("{0}: TweetBot will respond with: {1}", 
                    DateTime.Now, 
                    Environment.NewLine + string.Join(Environment.NewLine, Messages));
            }
        }

        public Task<IEnumerable<Tweet>> ProcessItems(IEnumerable<Tweet> tweets)
        {
            return Task<IEnumerable<Tweet>>.Factory.StartNew(new Func<IEnumerable<Tweet>>(() =>
                {
                    var repliedTo = new List<Tweet>();
                    foreach (var t in tweets)
                    {
                        string tweetedBy = t.User.Identifier.ScreenName.ToLower();
                        if (!NoTweetList.Any(x => x == tweetedBy) && //Don't bug people with constant retweets
                            !t.TweetText.ToLower().Contains(NoTweetList[0]) && //Don't bug them even if they are mentioned in the tweet
                            (!OnlyWithMentions || t.Status.Entities.UserMentions.Count > 0) //OPTIONAL: Only respond to tweets that mention someone
                            ) 
                        {
                            //Dont want to keep hitting the same person over and over so add them to the ignore list
                            NoTweetList.Add(tweetedBy);
                            //If they were mentioned in a tweet they get ignored in the future just in case they reply
                            NoTweetList.AddRange(t.Status.Entities.UserMentions.Where(um => !string.IsNullOrEmpty(um.ScreenName)).Select(um => um.ScreenName)); 
                            
                            string message = "";
                            if(t.User.FollowersCount > 9999)
                                //TODO: It would be very cool to have the code branch here for custom tweets to popular twitter accounts
                                //IDEA: Maybe have it text you for a response
                                message = Messages.OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                            else
                                //Randomly select response from list of possible responses
                                message = Messages.OrderBy(x => Guid.NewGuid()).FirstOrDefault();

                            //Tweet it
                            TwitterModel.Instance.UpdateStatus(message + " RT @" + t.User.Identifier.ScreenName + " " + t.TweetText, processStatus: false);

                            repliedTo.Add(t);

                            //Wait at least 1 minute between tweets so it doesnt look bot-ish with fast retweets
                            //Add some extra random timing somewhere between 0-2 minutes
                            //The shortest wait will be 1 minute the longest will be 3
                            int randomTime = 60000 + (1000 * Enumerable.Range(0, 120).OrderBy(x => Guid.NewGuid()).FirstOrDefault());
                            System.Threading.Thread.Sleep(randomTime); 
                        }
                    }
                    return repliedTo;
                }));
        }
    }

    #region TweetBotSettings
    public class TweetBotSettings : ConfigurationSection
    {
        private static TweetBotSettings settings = ConfigurationManager.GetSection("TweetBotSettings") as TweetBotSettings;

        public static TweetBotSettings Settings { get { return settings; } }

        [ConfigurationProperty("Messages", IsKey = false, IsRequired = true)]
        public MessageCollection Messages { get { return (MessageCollection)base["Messages"]; } }

        [ConfigurationProperty("Filters", IsKey = false, IsRequired = false)]
        public FilterCollection Filters { get { return (FilterCollection)base["Filters"]; } }
    }

    public class FilterCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new Filter();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((Filter)element).Key;
        }

        public Filter this[int idx]
        {
            get
            {
                return (Filter)BaseGet(idx);
            }
        }
        
        public Filter this[string key]
        {
            get
            {
                return (Filter)BaseGet(key);
            }
        }
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

    public class Filter : ConfigurationElement
    {
        [ConfigurationProperty("key", IsKey = false, IsRequired = true)]
        public string Key { get { return (string)base["key"]; } set { base["key"] = value; } }
        [ConfigurationProperty("value", IsKey = false, IsRequired = true)]
        public bool Value { get { return (bool)base["value"]; } set { base["value"] = value; } }
    }

    public class Message : ConfigurationElement
    {
        [ConfigurationProperty("key", IsKey = true, IsRequired = true)]
        public string Key { get { return (string)base["key"]; } set { base["key"] = value; } }
        [ConfigurationProperty("value", IsKey = false, IsRequired = true)]
        public string Value { get { return (string)base["value"]; } set { base["value"] = value; } }
    }
    #endregion
}

