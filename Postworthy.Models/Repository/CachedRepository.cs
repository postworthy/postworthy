using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Repository.Providers;

namespace Postworthy.Models.Repository
{
    public class CachedRepository<TYPE> : IRepository<TYPE>
        where TYPE : RepositoryEntity
    {
        [ThreadStatic]
        private static volatile CachedRepository<TYPE> instance;
        private static object instance_lock = new object();
        private SimpleRepository<TYPE> Storage;
        private SimpleRepository<TYPE> Cache;

        protected CachedRepository(string providerKey)
        {
            Storage = new SimpleRepository<TYPE>(providerKey);
            Cache = new SimpleRepository<TYPE>(providerKey)
                .SetProvider(new DistributedSharedCache<TYPE>(providerKey, Storage.GetProvider(), new TimeSpan(0, 20, 0)));
        }

        public static CachedRepository<TYPE> Instance(string providerKey)
        {
            if (instance == null)
            {
                lock (instance_lock)
                {
                    if (instance == null)
                        instance = new CachedRepository<TYPE>(providerKey);
                }
            }
            else if (instance.Storage.GetProvider().ProviderKey.ToLower() != providerKey.ToLower())
            {
                instance = null;
                return Instance(providerKey);
            }
                
            return instance;
        }

        public void SetCacheTTL(TimeSpan itemTTL)
        {
            Cache.SetProvider(new DistributedSharedCache<TYPE>(Storage.GetProvider().ProviderKey, Storage.GetProvider(), itemTTL));
        }

        public bool ContainsKey(string key)
        {
            return Storage.ContainsKey(key);
        }

        public IEnumerable<TYPE> Query(string key, int pageIndex = 0, int pageSize = 100, Func<TYPE, bool> where = null)
        {
            string cacheKey = key + "_" + pageIndex + "_" + pageSize;
            var result = Cache.Query(cacheKey, 0, 0, where);
            if (result == null || result.FirstOrDefault() == null)
            {
                var storedResult = Storage.Query(key, pageIndex, pageSize, where);
                if (storedResult != null && storedResult.FirstOrDefault() != null)
                {
                    Cache.Save(cacheKey, storedResult);
                    result = storedResult;
                }
            }

            return result;
        }

        public void Save(string key, TYPE obj)
        {
            Storage.Save(key, obj);
            Cache.Delete(key + "_0_100"); //Remove the first page
            Cache.Delete(key + "_0_1000"); //Remove the first page
        }

        public void Save(string key, IEnumerable<TYPE> objects)
        {
            Storage.Save(key, objects);
            Cache.Delete(key + "_0_100"); //Remove the first page
            Cache.Delete(key + "_0_1000"); //Remove the first page
        }

        public void Delete(string key)
        {
            Storage.Delete(key);
            Cache.Delete(key + "_0_100"); //Remove the first page
            Cache.Delete(key + "_0_1000"); //Remove the first page
        }

        public void Delete(string key, TYPE obj)
        {
            Storage.Delete(key, obj);
            Cache.Delete(key + "_0_100"); //Remove the first page
            Cache.Delete(key + "_0_1000"); //Remove the first page
        }

        public void Delete(string key, IEnumerable<TYPE> objects)
        {
            Storage.Delete(key, objects);
            Cache.Delete(key + "_0_100"); //Remove the first page
            Cache.Delete(key + "_0_1000"); //Remove the first page
        }
    }
}
