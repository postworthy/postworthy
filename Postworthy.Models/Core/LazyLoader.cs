using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Postworthy.Models.Core
{
    public class LazyLoader<T> : Lazy<T> where T:class, new()
    {
        public string ID { get; set; }

        public LazyLoader(string id, Func<T> factory) : base(factory, true)
        {
            this.ID = id;
        }
    }
}
