using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Caching;
using System.Web;

namespace Postworthy.Models.Repository.Providers
{
    public class MemoryCache<TYPE> : RepositoryStorageProvider<TYPE> where TYPE : RepositoryEntity
    {
        private Cache LocalCache;

        public IEnumerable<string> Keys
        {
            get
            {
                return LocalCache.OfType<System.Collections.DictionaryEntry>()
                            .Where(d => d.Value is List<TYPE>)
                            .Select(d => d.Key.ToString());
            }
        }

        public MemoryCache(string providerKey, Action<string, TYPE> ChangeHandler)
            : base(providerKey)
        {
            LocalCache = HttpRuntime.Cache;
        }

        public override IEnumerable<TYPE> Get(string key)
        {
            key = key.ToLower();
            var objects = LocalCache[key] as List<TYPE>;
            if (objects != null)
            {
                var items = objects.Reverse<TYPE>().ToList();
                if (items.Count > 0)
                {
                    foreach (var item in items)
                    {
                        yield return item;
                    }
                }
            }

            yield break;
        }

        public override TYPE Single(string collectionKey, string itemKey)
        {
            var key = collectionKey.ToLower();
            var objects = LocalCache[key] as List<TYPE>;
            if (objects != null)
            {
                return objects.Where(x => x.UniqueKey == itemKey).FirstOrDefault();
            }
            else
                return null;
        }

        public override void Store(string key, TYPE obj)
        {
            key = key.ToLower();

            var objects = LocalCache[key] as List<TYPE>;

            if (objects == null)
                LocalCache[key] = new List<TYPE> { obj };
            else
            {
                objects.Update(obj);
            }
        }

        public override void Store(string key, IEnumerable<TYPE> obj)
        {
            throw new NotImplementedException();
        }

        public override void Remove(string key, TYPE obj)
        {
            key = key.ToLower();
            var objects = LocalCache[key] as List<TYPE>;

            if (objects != null)
                objects.Remove(obj);
        }

        public override void Remove(string key, IEnumerable<TYPE> obj)
        {
            throw new NotImplementedException();
        }
    }
}
