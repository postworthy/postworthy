using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Caching;
using System.Linq.Expressions;
using System.Net;
using System.IO;
using System.Web.Script.Serialization;
using System.Timers;
using Newtonsoft.Json;
using Enyim.Caching;
using Enyim.Caching.Memcached;
using Enyim.Caching.Configuration;
using Postworthy.Models.Repository.Providers;
using System.Configuration;
using System.Reflection;

namespace Postworthy.Models.Repository
{  
    public sealed class Repository<TYPE> where TYPE : RepositoryEntity
    {
        private static volatile Repository<TYPE> instance;
        private static object instance_lock = new object();
        private static object keynotfound_lock = new object();
        private Timer SaveTimer;
        private Timer RefreshTimer;
        private RepositoryStorageProvider<TYPE> MemoryCache;
        private RepositoryStorageProvider<TYPE> SharedCache;
        private RepositoryStorageProvider<TYPE> LongTermStorageCache;
        private Dictionary<string, List<TYPE>> ChangeQueue;
        public event Func<string, List<TYPE>> KeyNotFound;
        public event Func<string, List<TYPE>> RefreshData;
        private Repository() 
        {
            MemoryCache = GetStorageProvider("OverrideLocalStorageProvider", () => { return new MemoryCache<TYPE>(QueueChange); });
            LongTermStorageCache = GetStorageProvider("OverrideLongTermStorageProvider", () => { return new FileSystemCache<TYPE>(); });
            SharedCache = GetStorageProvider("OverrideSharedStorageProvider", () => { return new DistributedSharedCache<TYPE>(LongTermStorageCache); });

            if (MemoryCache is MemoryCache<TYPE>)
            {
                ChangeQueue = new Dictionary<string, List<TYPE>>();

                SaveTimer = new Timer(30000);
                SaveTimer.Elapsed += new ElapsedEventHandler((x, y) =>
                    {
                        SaveTimer.Enabled = false;
                        try
                        {
                            SaveQueue();
                        }
                        catch { }
                        SaveTimer.Enabled = true;
                    });
                SaveTimer.Start();

                RefreshTimer = new Timer(300000);
                RefreshTimer.Elapsed += new ElapsedEventHandler((x, y) =>
                {
                    RefreshTimer.Enabled = false;
                    try
                    {
                        if (RefreshData != null)
                        {
                            var keys = (MemoryCache as MemoryCache<TYPE>).Keys;
                            foreach (var key in keys)
                            {
                                var newData = RefreshData(key);
                                if (newData != null)
                                    Save(key, newData);
                            }
                        }
                    }
                    catch { }
                    RefreshTimer.Enabled = true;
                });
                RefreshTimer.Start();
            }
        }
        public static Repository<TYPE> Instance
        {
            get 
            {
                if (instance == null)
                {
                    lock (instance_lock)
                    {
                        if (instance == null)
                            instance = new Repository<TYPE>();
                    }
                }
                return instance;
            }
        }
        public bool ContainsKey(string key)
        {
            key = key.ToLower();
            return Find(key, triggerNotFound: false).FirstOrDefault() != null;
        }
        public void RefreshLocalCache(string key)
        {
            UpdateLocalCache(key);
        }
        public List<TYPE> Query(string key, int pageIndex = 0, int pageSize = 100, Func<TYPE, bool> where = null)
        {
            key = key.ToLower();

            var objects = Find(key);

            if (objects != null)
            {
                if (where != null)
                {
                    objects = objects.Where(where).ToList();
                }
                else
                    objects = objects.ToList();

                return objects.Skip(pageIndex).Take((int)pageSize).ToList();
            }
            else
                return null;
        }
        public void Save(string key, TYPE obj)
        {
            key = key.ToLower();
            obj.RepositoryKey = key;
            InsertIntoLocalCache(key, obj);
            InsertIntoSharedCache(key, obj);
            InsertIntoLongTermStorage(key, obj);
        }
        public void Save(string key, IEnumerable<TYPE> objects)
        {
            key = key.ToLower();
            foreach (var o in objects)
            {
                o.RepositoryKey = key;
                InsertIntoLocalCache(key, o);
            }

            InsertIntoSharedCache(key, objects);
            InsertIntoLongTermStorage(key, objects);
        }
        public void Delete(string key)
        {
            key = key.ToLower();
            Delete(key, Find(key, false));
            
        }
        public void Delete(string key, TYPE obj)
        {
            key = key.ToLower();
            DeleteFromLocalCache(key, obj);
            DeleteFromSharedCache(key, obj);
            DeleteFromLongTermStorage(key, obj);
        }
        public void Delete(string key, IEnumerable<TYPE> objects)
        {
            key = key.ToLower();
            if (objects != null)
            {
                foreach (var o in objects)
                {
                    DeleteFromLocalCache(key, o);
                }

                DeleteFromSharedCache(key, objects);
                DeleteFromLongTermStorage(key, objects);
            }
        }
        public void FlushChanges()
        {
            SaveQueue();
        }
        private IEnumerable<TYPE> UpdateLocalCache(string key, bool triggerNotFound = true)
        {
            key = key.ToLower();
            var objects = CheckSharedCache(key);

            if (objects == null)
            {
                objects = CheckLongTermStorage(key);

                if (objects != null)
                {
                    foreach(var o in objects)
                    {
                        InsertIntoLocalCache(key, o);
                    }

                    InsertIntoSharedCache(key, objects);
                }
                else if (KeyNotFound != null && triggerNotFound)
                {
                    lock (keynotfound_lock)
                    {
                        objects = CheckLocalCache(key);
                        if (objects == null)
                        {
                            objects = KeyNotFound(key);
                            if (objects != null)
                                Save(key, objects);
                        }
                    }
                }
            }
            else
            {
                foreach (var o in objects)
                {
                    InsertIntoLocalCache(key, o);
                }
            }

            return objects;
        }
        private IEnumerable<TYPE> Find(string key, bool triggerNotFound = true)
        {
            key = key.ToLower();
            if (!string.IsNullOrEmpty(key))
            {
                var objects = CheckLocalCache(key);

                if (objects == null)
                {
                    objects = UpdateLocalCache(key, triggerNotFound);
                }

                return objects != null ? objects.Distinct() : null;
            }
            else
                return null;
        }
        private IEnumerable<TYPE> CheckLocalCache(string key)
        {
            return MemoryCache.Get(key);
        }
        private IEnumerable<TYPE> CheckSharedCache(string key)
        {
            return SharedCache.Get(key);
        }
        private IEnumerable<TYPE> CheckLongTermStorage(string key)
        {
            return LongTermStorageCache.Get(key);
        }
        private void InsertIntoLocalCache(string key, TYPE obj)
        {
            MemoryCache.Store(key, obj);
        }
        private void InsertIntoSharedCache(string key, TYPE obj)
        {
            SharedCache.Store(key, obj);
        }
        private void InsertIntoSharedCache(string key, IEnumerable<TYPE> obj)
        {
            SharedCache.Store(key, obj);
        }
        private void InsertIntoLongTermStorage(string key, TYPE obj)
        {
            LongTermStorageCache.Store(key, obj);
        }
        private void InsertIntoLongTermStorage(string key, IEnumerable<TYPE> obj)
        {
            LongTermStorageCache.Store(key, obj);
        }
        private void DeleteFromLocalCache(string key, TYPE obj)
        {
            MemoryCache.Remove(key, obj);
        }
        private void DeleteFromSharedCache(string key, TYPE obj)
        {
            SharedCache.Remove(key, obj);
        }
        private void DeleteFromSharedCache(string key, IEnumerable<TYPE> obj)
        {
            SharedCache.Remove(key, obj);
        }
        private void DeleteFromLongTermStorage(string key, TYPE obj)
        {
            LongTermStorageCache.Remove(key, obj);
        }
        private void DeleteFromLongTermStorage(string key, IEnumerable<TYPE> obj)
        {
            LongTermStorageCache.Remove(key, obj);
        }
        private void SaveQueue()
        {
            lock(ChangeQueue)
            {
                foreach(var key in ChangeQueue.Keys)
                {
                    Save(key, ChangeQueue[key].Distinct().ToList());
                }
                ChangeQueue.Clear();
            }
        }
        private void QueueChange(string key, TYPE obj)
        {
            key = key.ToLower();
            lock(ChangeQueue)
            {
                List<TYPE> items = ChangeQueue.ContainsKey(key) ? ChangeQueue[key] : null;
                if(items != null)
                    items.Add(obj);
                else
                    ChangeQueue.Add(key, new List<TYPE>{ obj});
            }
        }
        private RepositoryStorageProvider<TYPE> GetStorageProvider(string SettingKey, Func<RepositoryStorageProvider<TYPE>> defaultType)
        {
            string overrideProvider = ConfigurationManager.AppSettings[SettingKey];
            if (!string.IsNullOrEmpty(overrideProvider))
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().Where(x=>x.GetType(overrideProvider, false) != null).FirstOrDefault();
                if (assembly != null)
                {
                    var type = assembly.GetType(overrideProvider, false);
                    if (type != null)
                    {
                        var provider = type.MakeGenericType(typeof(TYPE)).GetConstructor(System.Type.EmptyTypes).Invoke(null) as RepositoryStorageProvider<TYPE>;
                        if (provider != null)
                            return provider;
                    }
                }
            }
            return defaultType();
        }
    }
}