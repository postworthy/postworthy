using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using Postworthy.Models.Core;

namespace Postworthy.Models.Repository.Providers
{
    public class FileSystemCache<TYPE> : RepositoryStorageProvider<TYPE> where TYPE : RepositoryEntity
    {
        private string GetPath(string key)
        {
            return FileUtility.GetPath(key + ".json");
        }
        private string GetFileData(string key)
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
        private void Store(string key, string obj)
        {   
            var path = GetPath(key);

            LockFile(path, FileMode.Create, fs =>
            {
                using (var writer = new StreamWriter(fs))
                {
                    writer.Write(obj);
                }
            });
        }
        private void Remove(string key)
        {   
            var path = GetPath(key);

            File.Delete(path);
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

        private List<string> GetLongTermStorageItemKeys(string key)
        {
            key = key.ToLower();
            return Deserialize<List<string>>(this.GetFileData(key) as string);
        }

        public override IEnumerable<TYPE> Get(string key)
        {
            key = key.ToLower();
            var objKeys = GetLongTermStorageItemKeys(key);
            if (objKeys != null)
            {
                var items = objKeys.Reverse<string>();

                foreach(var item in items)
                {
                    yield return Single(key, item);
                }
            }

            yield break;
        }

        public override TYPE Single(string collectionKey, string itemKey)
        {
            return Deserialize<TYPE>(this.GetFileData(itemKey));
        }

        public override void Store(string key, TYPE obj)
        {
            key = key.ToLower();
            var objects = GetLongTermStorageItemKeys(key);

            if (objects != null)
                objects = objects.Union(new List<string> { obj.UniqueKey }).ToList();
            else
                objects = new List<string> { obj.UniqueKey };

            this.Store(obj.UniqueKey.ToString(), Serialize(obj));
            this.Store(key, Serialize(objects));
        }

        public override void Store(string key, IEnumerable<TYPE> obj)
        {
            key = key.ToLower();
            var objects = GetLongTermStorageItemKeys(key);

            if (objects != null)
                objects = objects.Union(obj.Select(o => o.UniqueKey)).ToList();
            else
                objects = obj.Select(x => x.UniqueKey).ToList();

            foreach (var o in obj)
            {
                this.Store(o.UniqueKey.ToString(), Serialize(o));
            }
            this.Store(key, Serialize(objects));
        }

        public override void Remove(string key, TYPE obj)
        {
            key = key.ToLower();
            var objects = GetLongTermStorageItemKeys(key);

            if (objects != null)
            {
                objects.Remove(obj.UniqueKey);
                this.Remove(obj.UniqueKey.ToString());

                if (objects.Count > 0)
                    this.Store(key, Serialize(objects));
                else
                    this.Remove(key);
            }
        }

        public override void Remove(string key, IEnumerable<TYPE> obj)
        {
            key = key.ToLower();
            var objects = GetLongTermStorageItemKeys(key);

            if (objects != null)
            {
                foreach (var o in obj)
                {
                    objects.Remove(o.UniqueKey);
                    this.Remove(o.UniqueKey.ToString());
                }

                if (objects.Count > 0)
                    this.Store(key, Serialize(objects));
                else
                    this.Remove(key);
            }
        }
    }
}
