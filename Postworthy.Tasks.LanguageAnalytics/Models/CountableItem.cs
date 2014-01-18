using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Tasks.LanguageAnalytics.Models
{
    public class CountableItem : CountableItem<string>
    {
        public CountableItem(string key, int count) : base(key, count) { }
    }

    public class CountableItem<T>
    {
        private T key = default(T);
        public T Key
        {
            get { return key; }
            set { key = value; LastModifiedTime = DateTime.Now; }
        }
        private int count;
        public int Count
        {
            get { return count; }
            set { count = value; LastModifiedTime = DateTime.Now; }
        }
        public DateTime LastModifiedTime { get; set; }

        public CountableItem(T key, int count)
        {
            Key = key;
            Count = count;
            LastModifiedTime = DateTime.Now;
        }
    }
}
