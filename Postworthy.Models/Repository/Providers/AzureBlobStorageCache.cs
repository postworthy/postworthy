using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Configuration;
using Postworthy.Models.Account;
using Newtonsoft.Json;
using System.IO;

namespace Postworthy.Models.Repository.Providers
{
    public class AzureBlobStorageCache<TYPE> : RepositoryStorageProvider<TYPE> where TYPE : RepositoryEntity
    {

        private CloudStorageAccount storageAccount = null;
        private CloudBlobClient blobClient = null;
        private CloudBlobContainer container = null;

        public AzureBlobStorageCache()
            : base()
        {
            var connectionString = ConfigurationManager.AppSettings["AzureStorageConnectionString"];
            if (string.IsNullOrEmpty(connectionString))
                throw new Exception("Config Section 'appSettings' missing AzureStorageConnectionString value!");

            storageAccount = CloudStorageAccount.Parse(connectionString);
            blobClient = storageAccount.CreateCloudBlobClient();
            container = blobClient.GetContainerReference(UsersCollection.PrimaryUser().TwitterScreenName.ToLower());

            if (!container.Exists()) 
                container.Create();
        }

        private RET DownloadBlob<RET>(CloudBlockBlob blob) where RET : RepositoryEntity
        {
            using (var stream = new MemoryStream())
            {
                using (var reader = new StreamReader(stream))
                {
                    try
                    {
                        blob.DownloadToStream(stream);
                        stream.Seek(0, 0);
                    }
                    catch (StorageException se)
                    {
                        return default(RET);
                    }
                    return Deserialize<RET>(reader.ReadToEnd());
                }
            }
        }

        private void UploadBlob(CloudBlockBlob blob, RepositoryEntity obj)
        {
            using(var stream = new MemoryStream())
            {
                using(var writer = new StreamWriter(stream))
                {
                    writer.Write(Serialize(obj));
                    writer.Flush();
                    stream.Seek(0, 0);
                    blob.UploadFromStream(stream);
                }
            }   
        }

        private StorageEntityIndex GetStorageEntityIndex(string key)
        {
            key = key.ToLower();
            
            return DownloadBlob<StorageEntityIndex>(container.GetDirectoryReference(StorageEntityIndex.DIRECTORY_KEY).GetBlockBlobReference(key))
                ?? new StorageEntityIndex(key);
        }

        public override List<TYPE> Get(string key, int limit)
        {
            return container.GetDirectoryReference(key)
                .ListBlobs().Cast<CloudBlockBlob>()
                .Reverse<CloudBlockBlob>()
                .Take(limit)
                .Select(x=>DownloadBlob<TYPE>(x))
                .ToList();
        }

        public override void Store(string key, TYPE obj)
        {
            key = key.ToLower();
            var index = GetStorageEntityIndex(key);
            
            if (index != null && index.EntityKeys != null)
                index.EntityKeys = index.EntityKeys.Union(new List<string> { obj.UniqueKey }).ToList();
            else
                index.EntityKeys = new List<string> { obj.UniqueKey };

            UploadBlob(container.GetDirectoryReference(key).GetBlockBlobReference(obj.UniqueKey), obj);
            UploadBlob(container.GetDirectoryReference(StorageEntityIndex.DIRECTORY_KEY).GetBlockBlobReference(key), index);
        }

        public override void Store(string key, List<TYPE> obj)
        {
            key = key.ToLower();
            var index = GetStorageEntityIndex(key);

            if (index != null && index.EntityKeys != null)
                index.EntityKeys = index.EntityKeys.Union(obj.Select(o => o.UniqueKey)).ToList();
            else
                index.EntityKeys = obj.Select(x => x.UniqueKey).ToList();

            obj.ForEach(o =>
            {
                UploadBlob(container.GetDirectoryReference(key).GetBlockBlobReference(o.UniqueKey), o);
            });

            UploadBlob(container.GetDirectoryReference(StorageEntityIndex.DIRECTORY_KEY).GetBlockBlobReference(key), index);
        }

        public override void Remove(string key, TYPE obj)
        {
            container.GetDirectoryReference(key).GetBlockBlobReference(obj.UniqueKey).Delete();
        }

        public override void Remove(string key, List<TYPE> obj)
        {
            obj.ForEach(o =>
            {
                Remove(key, o);
            });
        }

        #region Internal Blob Azure Classes
        private class StorageEntityIndex : RepositoryEntity
        {
            public string Key { get; set; }
            public const string DIRECTORY_KEY = "Index";
            public StorageEntityIndex() 
            {
                EntityKeys = new List<string>();
            }

            public StorageEntityIndex(string key)
            {
                this.Key = key;
                EntityKeys = new List<string>();
            }

            public StorageEntityIndex(string key, List<string> EntityKeys)
            {
                this.Key = key;
                this.EntityKeys = EntityKeys;
            }

            public List<string> EntityKeys { get; set; }

            public override string UniqueKey
            {
                get { return Key; }
            }

            public override bool IsEqual(RepositoryEntity other)
            {
                return this.UniqueKey == other.UniqueKey;
            }
        }
        #endregion
    }
}
