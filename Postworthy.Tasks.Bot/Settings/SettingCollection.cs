using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Tasks.Bot.Settings
{
    public class SettingCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new Setting();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((Setting)element).Key;
        }

        public Setting this[int idx]
        {
            get
            {
                return (Setting)BaseGet(idx);
            }
        }

        public Setting this[string key]
        {
            get
            {
                return (Setting)BaseGet(key);
            }
        }
    }
}
