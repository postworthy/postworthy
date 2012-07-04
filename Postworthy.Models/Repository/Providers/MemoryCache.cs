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

        private Action<string, TYPE> ChangeHandler;

        public IEnumerable<string> Keys 
        { 
            get 
            {
                return LocalCache.OfType<System.Collections.DictionaryEntry>()
                            .Where(d => d.Value is List<TYPE>)
                            .Select(d => d.Key.ToString());
            } 
        }

        public MemoryCache(Action<string, TYPE> ChangeHandler)
        {
            LocalCache = HttpRuntime.Cache;
            this.ChangeHandler = ChangeHandler;
            if (this.ChangeHandler == null) throw new ArgumentNullException("ChangeHandler");
        }

        public override List<TYPE> Get(string key, int limit)
        {
            key = key.ToLower();
            var objects = LocalCache[key] as List<TYPE>;
            if (objects != null)
                return objects.Reverse<TYPE>().Take(limit).ToList();
            else
                return null;
        }

        public override void Store(string key, TYPE obj)
        {
            key = key.ToLower();
            obj.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler((x, y) =>
            {
                var entity = x as RepositoryEntity;
                ChangeHandler(entity.RepositoryKey, (TYPE)entity);
            });

            var objects = LocalCache[key] as List<TYPE>;

            if (objects == null)
                LocalCache[key] = new List<TYPE> { obj };
            else
            {
                objects.Update(obj);
            }
        }

        public override void Store(string key, List<TYPE> obj)
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

        public override void Remove(string key, List<TYPE> obj)
        {
            throw new NotImplementedException();
        }
    }
}
