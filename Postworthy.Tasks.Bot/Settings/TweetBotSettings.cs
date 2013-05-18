using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Tasks.Bot.Settings
{
    public class TweetBotSettings : ConfigurationSection
    {
        private static TweetBotSettings settings = ConfigurationManager.GetSection("TweetBotSettings") as TweetBotSettings;

        public static TweetBotSettings Get { get { return settings; } }

        [ConfigurationProperty("Messages", IsKey = false, IsRequired = true)]
        public MessageCollection Messages { get { return (MessageCollection)base["Messages"]; } }

        [ConfigurationProperty("Filters", IsKey = false, IsRequired = false)]
        public FilterCollection Filters { get { return (FilterCollection)base["Filters"]; } }

        [ConfigurationProperty("Settings", IsKey = false, IsRequired = false)]
        public SettingCollection Settings { get { return (SettingCollection)base["Settings"]; } }
    }
}
