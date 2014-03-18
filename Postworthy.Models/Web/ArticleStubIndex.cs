using Postworthy.Models.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Models.Web
{
    public class ArticleStubIndex : RepositoryEntity
    {
        public Guid ArticleStubIndexID { get; set; }
        public int PageCount { get { return ArticleStubPages.Count(); } }
        public List<KeyValuePair<long, string>> ArticleStubPages { get; set; }
        public ArticleStubIndex()
        {
            ArticleStubIndexID = Guid.NewGuid();
            ArticleStubPages = new List<KeyValuePair<long, string>>();
        }
        public override string UniqueKey
        {
            get { return "Index_" + ArticleStubIndexID.ToString(); }
        }

        public override bool IsEqual(RepositoryEntity other)
        {
            return UniqueKey == other.UniqueKey;
        }
    }
}
