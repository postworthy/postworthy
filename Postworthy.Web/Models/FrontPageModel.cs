using Postworthy.Models.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Postworthy.Web.Models
{
    public class FrontPageModel
    {
        public List<ArticleStub> ArticleStubs { get; set; }
        public List<ArticleStub> ExcludedArticleStubs { get; set; }
        public List<Article> FullArticles { get; set; }
        public FrontPageModel()
        {
            FullArticles = new List<Article>();
            ArticleStubs = new List<ArticleStub>();
            ExcludedArticleStubs = new List<ArticleStub>();
        }
        public FrontPageModel(IEnumerable<Article> fullArticles, IEnumerable<ArticleStub> articleStubs = null)
            : this()
        {
            if (fullArticles != null)
                FullArticles.AddRange(fullArticles);
            if (articleStubs != null)
                ArticleStubs.AddRange(articleStubs);
        }
    }
}