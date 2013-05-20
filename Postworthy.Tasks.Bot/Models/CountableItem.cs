using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Tasks.Bot.Models
{
    public class CountableItem : CountableItem<string>
    {
        public CountableItem(string key, int count) : base(key, count) { }
    }

    public class CountableItem<T>
    {
        public T Key { get; set; }
        public int Count { get; set; }

        public CountableItem(T key, int count)
        {
            Key = key;
            Count = count;
        }
    }
}
