using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Tasks.Bot.Models
{
    public class CountableItem
    {
        public string Key { get; set; }
        public int Count { get; set; }

        public CountableItem(string key, int count)
        {
            Key = key;
            Count = count;
        }
    }
}
