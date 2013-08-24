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
using System.IO.Compression;

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
                StreamReader reader;
                try
                {
                    blob.DownloadToStream(stream);
                }
                catch (StorageException se)
                {
                    return default(RET);
                }
                try
                {
                    stream.Seek(0, 0);
                    reader = new StreamReader(new GZipStream(stream, CompressionMode.Decompress));
                    var json = reader.ReadToEnd();
                    return Deserialize<RET>(json);
                }
                catch 
                {
                    stream.Seek(0, 0);
                    reader = new StreamReader(stream);
                    return Deserialize<RET>(reader.ReadToEnd());
                }
            }
        }

        private void UploadBlob(CloudBlockBlob blob, RepositoryEntity obj)
        {
            using(var streamCompressed = new MemoryStream())
            {
                using (var gzip = new GZipStream(streamCompressed, CompressionMode.Compress))
                {
                    var data = Encoding.UTF8.GetBytes(Serialize(obj));
                    gzip.Write(data, 0, data.Length);
                    gzip.Flush();
                    gzip.Close();

                    using (var streamOut = new MemoryStream(streamCompressed.ToArray()))
                    {
                        blob.UploadFromStream(streamOut);
                    }
                }
            }   
        }

        private StorageEntityIndex GetStorageEntityIndex(string key)
        {
            key = key.ToLower();
            
            return DownloadBlob<StorageEntityIndex>(container.GetDirectoryReference(StorageEntityIndex.DIRECTORY_KEY).GetBlockBlobReference(key))
                ?? new StorageEntityIndex(key);
        }

        public override IEnumerable<TYPE> Get(string key)
        {
            var items = container.GetDirectoryReference(key)
                .ListBlobs().Cast<CloudBlockBlob>()
                .OrderByDescending(b => b.Properties.LastModified)
                .ToList();

            if (items.Count > 0)
            {
                foreach (var item in items)
                {
                    yield return DownloadBlob<TYPE>(item);
                }
            }

            yield break;
        }

        public override TYPE Single(string collectionKey, string itemKey)
        {
            return DownloadBlob<TYPE>(container.GetDirectoryReference(collectionKey).GetBlockBlobReference(itemKey));
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

        public override void Store(string key, IEnumerable<TYPE> obj)
        {
            key = key.ToLower();
            var index = GetStorageEntityIndex(key);

            if (index != null && index.EntityKeys != null)
                index.EntityKeys = index.EntityKeys.Union(obj.Select(o => o.UniqueKey)).ToList();
            else
                index.EntityKeys = obj.Select(x => x.UniqueKey).ToList();

            foreach(var o in obj)
            {
                UploadBlob(container.GetDirectoryReference(key).GetBlockBlobReference(o.UniqueKey), o);
            }

            UploadBlob(container.GetDirectoryReference(StorageEntityIndex.DIRECTORY_KEY).GetBlockBlobReference(key), index);
        }

        public override void Remove(string key, TYPE obj)
        {
            container.GetDirectoryReference(key).GetBlockBlobReference(obj.UniqueKey).Delete();
        }

        public override void Remove(string key, IEnumerable<TYPE> obj)
        {
            foreach(var o in obj)
            {
                Remove(key, o);
            }
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
