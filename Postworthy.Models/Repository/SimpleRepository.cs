using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Postworthy.Models.Repository.Providers;

namespace Postworthy.Models.Repository
{
    public class SimpleRepository<TYPE> : IRepository<TYPE>
        where TYPE : RepositoryEntity
    {
        private RepositoryStorageProvider<TYPE> Storage;
        public SimpleRepository(string providerKey)
        {
            Storage = GetStorageProvider("StorageProvider", providerKey, () => { return new FileSystemCache<TYPE>(providerKey); });
        }
        public RepositoryStorageProvider<TYPE> GetProvider()
        {
            return Storage;
        }
        public SimpleRepository<TYPE> SetProvider(RepositoryStorageProvider<TYPE> provider)
        {
            Storage = provider;
            return this;
        }
        public bool ContainsKey(string key)
        {
            key = key.ToLower();
            return Storage.Get(key) != null;
        }
        public IEnumerable<TYPE> Query(string key, int pageIndex = 0, int pageSize = 100, Func<TYPE, bool> where = null)
        {
            key = key.ToLower();

            var objects = Storage.Get(key);

            if (objects != null)
            {
                if (where != null)
                    objects = objects.Where(where);

                return (pageSize > 0 ? objects.Skip(pageIndex).Take((int)pageSize) : objects).Where(x => x != null).ToList();
            }
            else
                return null;
        }
        public void Save(string key, TYPE obj)
        {
            key = key.ToLower();
            obj.RepositoryKey = key;
            Storage.Store(key, obj);
        }
        public void Save(string key, IEnumerable<TYPE> objects)
        {
            key = key.ToLower();
            foreach (var o in objects)
            {
                o.RepositoryKey = key;
            }

            Storage.Store(key, objects);
        }
        public void Delete(string key)
        {
            key = key.ToLower();
            Delete(key, Storage.Get(key));
        }
        public void Delete(string key, TYPE obj)
        {
            key = key.ToLower();
            Storage.Remove(key, obj);
        }
        public void Delete(string key, IEnumerable<TYPE> objects)
        {
            key = key.ToLower();
            if (objects != null)
            {
                Storage.Remove(key, objects);
            }
        }
        private RepositoryStorageProvider<TYPE> GetStorageProvider(string SettingKey, string providerKey, Func<RepositoryStorageProvider<TYPE>> defaultType)
        {
            string overrideProvider = ConfigurationManager.AppSettings[SettingKey];
            if (!string.IsNullOrEmpty(overrideProvider))
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetType(overrideProvider, false) != null).FirstOrDefault();
                if (assembly != null)
                {
                    var type = assembly.GetType(overrideProvider, false);
                    if (type != null)
                    {
                        var provider = type.MakeGenericType(typeof(TYPE))
                            .GetConstructor(new Type[] { typeof(string) })
                            .Invoke(new object[] { providerKey }) as RepositoryStorageProvider<TYPE>;
                        if (provider != null)
                            return provider;
                    }
                }
            }
            return defaultType();
        }
    }
}
