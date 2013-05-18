using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Tasks.Bot.Settings
{
    public class Setting : ConfigurationElement
    {
        [ConfigurationProperty("key", IsKey = false, IsRequired = true)]
        public string Key { get { return (string)base["key"]; } set { base["key"] = value; } }
        [ConfigurationProperty("value", IsKey = false, IsRequired = true)]
        public bool Value { get { return (bool)base["value"]; } set { base["value"] = value; } }
    }
}
