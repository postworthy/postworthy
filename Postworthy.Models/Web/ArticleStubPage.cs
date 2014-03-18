using Postworthy.Models.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Models.Web
{
    public class ArticleStubPage : RepositoryEntity
    {
        public Guid ArticleStubPageID { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get { return ArticleStubs.Count(); } }
        public List<ArticleStub> ArticleStubs { get; set; }
        public List<ArticleStub> ExcludedArticleStubs { get; set; }
        public ArticleStubPage()
        {
            ArticleStubPageID = Guid.NewGuid();
            PageNumber = 0;
            ArticleStubs = new List<ArticleStub>();
            ExcludedArticleStubs = new List<ArticleStub>();
        }
        public ArticleStubPage(int pageNumber = 0, IEnumerable<ArticleStub> articleStubs = null)
            : this()
        {
            PageNumber = pageNumber;
            if (articleStubs != null)
                ArticleStubs.AddRange(articleStubs);
        }

        public override string UniqueKey
        {
            get { return "Page_" + ArticleStubPageID.ToString(); }
        }

        public override bool IsEqual(RepositoryEntity other)
        {
            return UniqueKey == other.UniqueKey;
        }
    }
}
