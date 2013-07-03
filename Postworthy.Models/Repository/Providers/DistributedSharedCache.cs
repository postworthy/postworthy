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
        private MemcachedClient SharedCache;

        private string GetPath(string key)
        {
            return FileUtility.GetPath(key + ".json");
        }

        private string GetLocal(string key)
        {
            var path = GetPath(key);
            string output = null;
            if (File.Exists(path))
            {
                LockFile(path, FileMode.Open, fs =>
                {
                    using (var reader = new StreamReader(fs))
                    {
                        output = reader.ReadToEnd();
                    }
                });

                return output;
            }
            return "";
        }

        private static void LockFile(string path, FileMode mode, Action<FileStream> action)
        {
            var autoResetEvent = new AutoResetEvent(false);

            while (true)
            {
                try
                {
                    using (var file = File.Open(path, mode, FileAccess.ReadWrite, FileShare.Write))
                    {
                        action(file);
                        break;
                    }
                }
                catch (IOException)
                {
                    var fileSystemWatcher =
                        new FileSystemWatcher(Path.GetDirectoryName(path))
                        {
                            EnableRaisingEvents = true
                        };

                    fileSystemWatcher.Changed +=
                        (o, e) =>
                        {
                            if (Path.GetFullPath(e.FullPath) == Path.GetFullPath(path))
                            {
                                autoResetEvent.Set();
                            }
                        };

                    autoResetEvent.WaitOne();
                }
            }
        }

        public DistributedSharedCache()
        {
            SharedCache = new MemcachedClient();
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
                var items = objKeys.Reverse<string>();

                foreach(var item in items)
                {
                    var obj = Deserialize<TYPE>(SharedCache.Get(item) as string);
                    if(obj == null)
                    {
                        obj = Deserialize<TYPE>(GetLocal(item) as string);
                        if (obj != null)
                        {
                            //It is possible an object was to big for cache and you could get an error here.
                            //One possible solution could be to eat the error and always let it pull large objects from Local
                            //For Now I will leave this unhandled
                            SharedCache.Store(StoreMode.Set, obj.UniqueKey.ToString(), Serialize(obj));
                        }
                    }

                    yield return obj;
                }
            }
            yield return null;
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
                SharedCache.Store(StoreMode.Set, obj.UniqueKey.ToString(), Serialize(obj));
            }
            catch (MemcachedException ex)
            {
                if (ex.Message != "object too large for cache") throw;

                objects.Remove(obj.UniqueKey);
            }

            if(objects.Count > 0) 
                SharedCache.Store(StoreMode.Set, key, Serialize(objects));
        }

        public override void Store(string key, List<TYPE> obj)
        {
            key = key.ToLower();
            var objects = GetSharedCacheItemKeys(key);

            if (objects != null)
                objects = objects.Union(obj.Select(o => o.UniqueKey)).ToList();
            else
                objects = obj.Select(x => x.UniqueKey).ToList();

            obj.ForEach(o =>
            {
                try
                {
                    SharedCache.Store(StoreMode.Set, o.UniqueKey.ToString(), Serialize(o));
                }
                catch (MemcachedException ex)
                {
                    if (ex.Message != "object too large for cache") throw;

                    objects.Remove(o.UniqueKey);
                }
            });
            if (objects.Count > 0) 
                SharedCache.Store(StoreMode.Set, key, Serialize(objects));
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
                    SharedCache.Store(StoreMode.Set, key, Serialize(objects));
                else
                    SharedCache.Remove(key);
            }
        }

        public override void Remove(string key, List<TYPE> obj)
        {
            key = key.ToLower();
            var objects = GetSharedCacheItemKeys(key);

            if (objects != null)
            {
                obj.ForEach(o =>
                {
                    objects.Remove(o.UniqueKey);
                    SharedCache.Remove(o.UniqueKey.ToString());
                });

                if (objects.Count > 0)
                    SharedCache.Store(StoreMode.Set, key, Serialize(objects));
                else
                    SharedCache.Remove(key);
            }
        }
    }
}
