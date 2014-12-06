using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using System.IO;
using System.Threading;
using Postworthy.Models.Core;

namespace Postworthy.Models.Repository.Providers
{
    public class DistributedSharedCache<TYPE> : RepositoryStorageProvider<TYPE> where TYPE : RepositoryEntity
    {
        private const string SPLIT_BY = "\u271D\u271D";
        private MemcachedClient SharedCache;
        private RepositoryStorageProvider<TYPE> LongTermStorage;
        private TimeSpan ItemTTL;

        public DistributedSharedCache(string providerKey, RepositoryStorageProvider<TYPE> longTerm, TimeSpan? itemTTL = null)
            : base(providerKey)
        {
            SharedCache = new MemcachedClient();
            LongTermStorage = longTerm;

            ItemTTL = itemTTL ?? new TimeSpan(1, 0, 0);
        }

        private List<string> GetSharedCacheItemKeys(string key)
        {
            key = key.ToLower();
            return Deserialize<List<string>>(SharedCache.Get(key) as string);
        }

        public override IEnumerable<TYPE> Get(string key)
        {
            key = key.ToLower();
            var objKeys = GetSharedCacheItemKeys(key);
            if (objKeys != null)
            {
                var items = objKeys.Reverse<string>().ToList();
                if (items.Count > 0)
                {
                    foreach (var item in items)
                    {
                        yield return Single(key, item);
                    }
                }
            }

            yield break;
        }

        public override TYPE Single(string collectionKey, string itemKey)
        {
            var obj = SharedCache.Get(itemKey + "_0") as string;
            if (obj == null)
            {
                var ltobj = LongTermStorage != null ? LongTermStorage.Single(collectionKey, itemKey) : null;
                if (ltobj != null)
                {
                    StoreSingle(ltobj);
                }
                return ltobj;
            }
            else
            {
                var split = obj.Split(new string[] { SPLIT_BY }, StringSplitOptions.RemoveEmptyEntries);
                int next = int.Parse(split[0]);
                if (next == 0)
                    return Deserialize<TYPE>(split[1]);
                else
                {
                    var bigObj = split[1];
                    while (next > 0)
                    {
                        var temp = SharedCache.Get(itemKey + "_" + next) as string;
                        split = temp.Split(new string[] { SPLIT_BY }, StringSplitOptions.RemoveEmptyEntries);
                        next = int.Parse(split[0]);
                        bigObj += split[1];
                    }

                    return Deserialize<TYPE>(bigObj);
                }
            }
        }

        private void StoreSingle(TYPE obj)
        {
            int chunkSize = 512 * 1000;
            var serializedData = Serialize(obj);
            List<string> chunks = new List<string>();
            int chunkCount = (int)Math.Ceiling(serializedData.Length / ((chunkSize + ((SPLIT_BY.Length + "000".Length) * 2)) * 1.0));

            if (chunkCount > 1)
            {
                var serializedDataCharacters = serializedData.ToList();
                for (int i = 0; i < chunkCount; i++)
                {
                    var next = i + 1 < chunkCount ? i + 1 : 0;
                    SharedCache.Store(StoreMode.Set,
                        obj.UniqueKey.ToString() + "_" + i,
                        next + SPLIT_BY + string.Join("", serializedDataCharacters.Skip(i * chunkSize).Take(chunkSize)),
                        ItemTTL);
                }
            }
            else
                SharedCache.Store(StoreMode.Set, obj.UniqueKey.ToString() + "_0", "0" + SPLIT_BY + serializedData, ItemTTL);
        }

        public override void Store(string key, TYPE obj)
        {
            key = key.ToLower();
            var objects = GetSharedCacheItemKeys(key);

            if (objects != null)
                objects = objects.Union(new List<string> { obj.UniqueKey }).ToList();
            else
                objects = new List<string> { obj.UniqueKey };

            try
            {
                //SharedCache.Store(StoreMode.Set, obj.UniqueKey.ToString(), Serialize(obj), ItemTTL);
                StoreSingle(obj);
            }
            catch (MemcachedException ex)
            {
                if (ex.Message != "object too large for cache") throw;

                objects.Remove(obj.UniqueKey);
            }

            if (objects.Count > 0)
                SharedCache.Store(StoreMode.Set, key, Serialize(objects), ItemTTL);
        }

        public override void Store(string key, IEnumerable<TYPE> obj)
        {
            key = key.ToLower();
            var objects = GetSharedCacheItemKeys(key);

            if (objects != null)
                objects = objects.Union(obj.Select(o => o.UniqueKey)).ToList();
            else
                objects = obj.Select(x => x.UniqueKey).ToList();

            foreach (var o in obj)
            {
                try
                {
                    //SharedCache.Store(StoreMode.Set, o.UniqueKey.ToString(), Serialize(o), ItemTTL);
                    StoreSingle(o);
                }
                catch (MemcachedException ex)
                {
                    if (ex.Message != "object too large for cache") throw;

                    objects.Remove(o.UniqueKey);
                }
            }
            if (objects.Count > 0)
                SharedCache.Store(StoreMode.Set, key, Serialize(objects), ItemTTL);
        }

        public override void Remove(string key, TYPE obj)
        {
            key = key.ToLower();
            var objects = GetSharedCacheItemKeys(key);

            if (objects != null)
            {
                objects.Remove(obj.UniqueKey);
                SharedCache.Remove(obj.UniqueKey.ToString());

                if (objects.Count > 0)
                    SharedCache.Store(StoreMode.Set, key, Serialize(objects), ItemTTL);
                else
                    SharedCache.Remove(key);
            }
        }

        public override void Remove(string key, IEnumerable<TYPE> obj)
        {
            key = key.ToLower();
            var objects = GetSharedCacheItemKeys(key);

            if (objects != null)
            {
                foreach (var o in obj)
                {
                    objects.Remove(o.UniqueKey);
                    SharedCache.Remove(o.UniqueKey.ToString());
                }

                if (objects.Count > 0)
                    SharedCache.Store(StoreMode.Set, key, Serialize(objects), ItemTTL);
                else
                    SharedCache.Remove(key);
            }
        }
    }
}
