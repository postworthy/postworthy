using Postworthy.Models.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Postworthy.Models.Web
{
    public class ArticleIndex : RepositoryEntity
    {
        public class Index
        {
            public long Published { get; set; }
            public string Title { get; set; }
            public string DayTag { get; set; }
            public string Key { get; set; }
            public List<string> Tags { get; set; }
            public Index()
            {
                Tags = new List<string>();
            }
            public Index(long published, string title, string key, IEnumerable<string> tags)
                : this()
            {
                DayTag = "_" + DateTime.FromFileTimeUtc(published).ToLocalTime().ToShortDateString();
                Published = published;
                Title = title;
                Key = key;
                Tags.AddRange(tags);
            }
            public string GetSlug(int maxLength = 100)
            {
                string str = Title.ToLower();

                // invalid chars, make into spaces
                str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
                // convert multiple spaces/hyphens into one space       
                str = Regex.Replace(str, @"[\s-]+", " ").Trim();
                // cut and trim it
                str = str.Substring(0, str.Length <= maxLength ? str.Length : maxLength).Trim();
                // hyphens
                str = Regex.Replace(str, @"\s", "-");

                return str;

            }
        }
        public Guid ArticleIndexID { get; set; }
        public int PageCount { get { return Articles.Count(); } }
        public List<Index> Articles { get; set; }
        public ArticleIndex()
        {
            ArticleIndexID = Guid.NewGuid();
            Articles = new List<Index>();
        }
        public override string UniqueKey
        {
            get { return "Index_" + ArticleIndexID.ToString(); }
        }

        public override bool IsEqual(RepositoryEntity other)
        {
            return UniqueKey == other.UniqueKey;
        }
    }
}
