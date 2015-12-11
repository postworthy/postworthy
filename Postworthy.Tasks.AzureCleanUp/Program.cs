using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Postworthy.Models.Repository;
using Postworthy.Models.Twitter;
using Postworthy.Models.Web;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Tasks.AzureCleanUp
{
    class Program
    {

        static void Main(string[] args)
        {
            var connectionString = ConfigurationManager.AppSettings["AzureStorageConnectionString"];
            if (string.IsNullOrEmpty(connectionString))
                throw new Exception("Config Section 'appSettings' missing AzureStorageConnectionString value!");

            var storageAccount = CloudStorageAccount.Parse(connectionString);
            var blobClient = storageAccount.CreateCloudBlobClient();

            var deleteTasks = new List<Task>(10000000);

            Action<Task> delete = t =>
            {
                lock (deleteTasks)
                {
                    deleteTasks.Add(t);
                }
                Console.Clear();
                Console.WriteLine("Deleting " + deleteTasks.Count + " items");
            };

            var version = TwitterModel.VERSION;

            bool content = args.Any(a => a.ToLower() == "content");
            bool cleanVersion = args.Any(a => a.ToLower() == "version");
            bool shrink = args.Any(a => a.ToLower().StartsWith("shrink"));
            int newSize = args.Where(a => a.ToLower().StartsWith("shrink")).Select(x => x == "shrink" ? 500 : int.Parse(x.Replace("shrink", ""))).FirstOrDefault();

            blobClient.ListContainers()
                //.Skip(1) //Short circuit for testing
                //.Take(1) //Short circuit for testing
                .ToList().AsParallel().ForAll(c =>
            {
                #region Index Cleanup
                var index = c.GetDirectoryReference("Index");
                foreach (var b in index.ListBlobs().Where(x => x is CloudBlockBlob).Cast<CloudBlockBlob>())
                {
                    if (cleanVersion && !b.Name.Contains(version))
                    {
                        //Delete Index
                        var i = c.GetBlockBlobReference(b.Name);
                        delete(i.DeleteIfExistsAsync());

                        //Delete all Tweets
                        var d = c.GetDirectoryReference(b.Name.Split('/').Last());
                        foreach (var t in d.ListBlobs().Where(x => x is CloudBlockBlob).Cast<CloudBlockBlob>())
                        {
                            delete(t.DeleteIfExistsAsync());
                        }
                    }
                    
                    if (shrink && newSize > 0 && b.Name.Contains(version))
                    {
                        //Get Storage Index
                        var i = c.GetBlockBlobReference(b.Name);
                        var storageIndex =  Newtonsoft.Json.JsonConvert.DeserializeObject<StorageEntityIndex>(DownloadBlob(i));

                        if (storageIndex.EntityKeys.Count > newSize)
                        {
                            //Delete extra Tweets
                            var d = c.GetDirectoryReference(b.Name.Split('/').Last());
                            foreach (var t in d.ListBlobs().Where(x => x is CloudBlockBlob).Cast<CloudBlockBlob>().OrderByDescending(x => x.Properties.LastModified).Skip(newSize))
                            {
                                storageIndex.EntityKeys.Remove(t.Name.Split('/').Last());
                                delete(t.DeleteIfExistsAsync());
                            }

                            //Update Storage Index
                            UploadBlob(i, storageIndex);
                        }
                    }
                }
                #endregion

                #region Content Cleanup
                if (content)
                {
                    try
                    {
                        var repoIndex = new SimpleRepository<ArticleStubIndex>(c.Name);
                        var repoPage = new SimpleRepository<ArticleStubPage>(c.Name);
                        var stubIndex = repoIndex.Query(TwitterModel.Instance(c.Name).CONTENT_INDEX).FirstOrDefault();
                        if (stubIndex != null)
                        {
                            var remove = new List<KeyValuePair<long, string>>();
                            for (var i = stubIndex.ArticleStubPages.Count - 31; i > -1 && i < stubIndex.ArticleStubPages.Count; i++) // Only the last month(ish)
                            {
                                var si = stubIndex.ArticleStubPages[i];
                                var page = repoPage.Query(TwitterModel.Instance(c.Name).CONTENT + "_" + si.Value).FirstOrDefault();
                                if (page.ArticleStubs == null || page.ArticleStubs.Count == 0)
                                {
                                    repoPage.Delete(TwitterModel.Instance(c.Name).CONTENT + "_" + si.Value);
                                    remove.Add(si);
                                }
                            }
                            remove.ForEach(x => stubIndex.ArticleStubPages.Remove(x));
                            repoIndex.Save(TwitterModel.Instance(c.Name).CONTENT_INDEX, stubIndex);
                        }
                    }
                    catch { }
                }
                #endregion
            });

            Console.WriteLine("Waiting on all tasks to complete");
            Task.WaitAll(deleteTasks.ToArray());
        }

        private static string DownloadBlob(CloudBlockBlob blob)
        {
            using (var stream = new MemoryStream())
            {
                StreamReader reader;
                try
                {
                    blob.DownloadToStream(stream, options: new BlobRequestOptions()
                    {
                        RetryPolicy = new LinearRetry(TimeSpan.FromSeconds(5), 3)
                    });
                }
                catch (StorageException se)
                {
                    return "";
                }
                try
                {
                    stream.Seek(0, 0);
                    reader = new StreamReader(new GZipStream(stream, CompressionMode.Decompress));
                    return reader.ReadToEnd();
                }
                catch
                {
                    stream.Seek(0, 0);
                    reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
            }
        }

        private static void UploadBlob(CloudBlockBlob blob, RepositoryEntity obj)
        {
            using (var streamCompressed = new MemoryStream())
            {
                using (var gzip = new GZipStream(streamCompressed, CompressionMode.Compress))
                {
                    var data = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(obj));
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

        private class StorageEntityIndex : RepositoryEntity
        {
            public const string DIRECTORY_KEY = "Index";
            public string Key { get; set; }
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
    }
}
