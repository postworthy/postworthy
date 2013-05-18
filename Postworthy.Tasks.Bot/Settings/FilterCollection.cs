using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Tasks.Bot.Settings
{
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
}
