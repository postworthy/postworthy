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

        public override List<TYPE> Get(string key, int limit)
        {
            key = key.ToLower();
            var objKeys = GetSharedCacheItemKeys(key);
            if (objKeys != null)
            {
                var objectsWithKey = objKeys
                    .Reverse<string>()
                    .Take(limit > 0 ? limit : objKeys.Count)
                    .Select(k => new { key = k, obj = Deserialize<TYPE>(SharedCache.Get(k) as string) })
                    .ToList();

                var ltObjects = objectsWithKey.Where(o => o.obj == null).ToList();
                ltObjects.Clear();

                foreach (var o in objectsWithKey)
                {
                    if (o.obj == null)
                    {
                        var ltObj = Deserialize<TYPE>(GetLocal(o.key) as string);
                        if (ltObj != null)
                        {
                            SharedCache.Store(StoreMode.Set, ltObj.UniqueKey.ToString(), Serialize(ltObj));
                            ltObjects.Add(new { key = o.key, obj = ltObj });
                        }
                    }
                }

                objectsWithKey.AddRange(ltObjects);

                var objects = objectsWithKey
                    .Where(o => o.obj != null)
                    .Select(o => o.obj);

                if (objects != null)
                    return objects.ToList();
            }
            return null;
        }

        public override void Store(string key, TYPE obj)
        {
            key = key.ToLower();
            var objects = GetSharedCacheItemKeys(key);

            if (objects != null)
                objects = objects.Union(new List<string> { obj.UniqueKey }).ToList();
            else
                objects = new List<string> { obj.UniqueKey };

            SharedCache.Store(StoreMode.Set, obj.UniqueKey.ToString(), Serialize(obj));
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
                SharedCache.Store(StoreMode.Set, o.UniqueKey.ToString(), Serialize(o));
            });
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
