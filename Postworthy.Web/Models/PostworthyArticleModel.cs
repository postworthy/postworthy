using Postworthy.Models.Account;
using Postworthy.Models.Repository;
using Postworthy.Models.Twitter;
using Postworthy.Models.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Postworthy.Web.Models
{
    public class PostworthyArticleModel
    {
        protected PostworthyUser PrimaryUser { get; set; }

        #region Article Methods
        public PostworthyArticleModel(PostworthyUser primaryUser)
        {
            PrimaryUser = primaryUser;
        }
        public ArticleIndex GetArticleIndex()
        {
            return CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                    .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX).FirstOrDefault() ?? new ArticleIndex();
        }
        public bool HasArticles()
        {
            return GetArticleIndex().Articles.Count > 0;
        }
        public Article GetArticle(uint id)
        {
            var articleIndex = GetArticleIndex().Articles.Where(x => x.ID() == id).FirstOrDefault();

            if (articleIndex != null)
            {
                var articles = CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                    .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + articleIndex.DayTag).ToList();

                return articles.Where(x => x.UniqueKey == articleIndex.Key).FirstOrDefault();
            }

            return null;
        }
        public IEnumerable<Article> Articles(Func<Article, bool> where = null)
        {
            var articleIndexGroups = GetArticleIndex().Articles.GroupBy(x => x.DayTag).OrderByDescending(x => x.First().Published);
            foreach (var group in articleIndexGroups)
            {
                var articles = CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                   .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + group.Key).ToList();

                var filtered = where != null ? articles
                    .Where(where) : articles;

                if (articles != null)
                {
                    foreach (var article in filtered)
                    {
                        yield return article;
                    }
                }
            }
            
            yield return null;
        }
        public List<Article> PagedArticles(int skip, int take, string filter, out int pageCount)
        {
            var filtered = GetArticleIndex().Articles.Where(x => string.IsNullOrEmpty(filter) || x.Tags.Where(t => t.Replace("&", "").Replace(" ", "-").Replace(".", "-").ToLower() == filter).Count() > 0).ToList();

            pageCount = (int)Math.Ceiling(filtered.Count / (take * 1.0));

            var items = filtered
                .OrderByDescending(i => i.Published)
                .Skip(skip).Take(take);

            var results = new List<Article>();

            foreach (var item in items)
            {
                var article = GetArticle(item.ID());
                if (article != null)
                    results.Add(article);
            }
            return results;
        }
        public void SaveArticle(Article article)
        {
            string dayTag = "_" + article.PublishedDate.ToShortDateString();

            var index = GetArticleIndex();

            CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                .Save(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + dayTag, article);

            index.Articles.Add(new ArticleIndex.Index(article.PublishedDate.ToFileTimeUtc(), article.Title, article.UniqueKey, article.Tags));

            CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                .Save(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX, index);
        }
        public void EditArticle(Article article)
        {
            string dayTag = "_" + article.PublishedDate.ToShortDateString();

            CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                .Save(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + dayTag, article);

            var index = GetArticleIndex();

            var articleIndex = index.Articles.Where(x => x.Key == article.UniqueKey).FirstOrDefault();
            articleIndex.Title = article.Title;
            articleIndex.Tags = article.Tags;

            CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                .Save(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX, index);
        }
        public void DeleteArticle(uint id)
        {
            var index = GetArticleIndex();

            var articleIndex = index.Articles.Where(x => x.ID() == id).FirstOrDefault();

            if (articleIndex != null)
            {
                var article = CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                    .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + articleIndex.DayTag, where: x => x.UniqueKey == articleIndex.Key).FirstOrDefault();

                if (article != null)
                {
                    CachedRepository<Article>.Instance(PrimaryUser.TwitterScreenName)
                        .Delete(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE + articleIndex.DayTag, article);

                    index.Articles.Remove(articleIndex);

                    CachedRepository<ArticleIndex>.Instance(PrimaryUser.TwitterScreenName)
                        .Save(TwitterModel.Instance(PrimaryUser.TwitterScreenName).ARTICLE_INDEX, index);
                }
            }
        }
        #endregion
        #region ArticleStub Methods
        public ArticleStubIndex GetArticleStubIndex()
        {
            return CachedRepository<ArticleStubIndex>.Instance(PrimaryUser.TwitterScreenName)
                .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).CONTENT_INDEX).FirstOrDefault() ?? new ArticleStubIndex();
        }
        public ArticleStubPage GetArticleStubPage(DateTime date)
        {
            var dayTag = date.ToShortDateString() == DateTime.Now.ToShortDateString() ? "" : "_" + date.ToShortDateString();
            return CachedRepository<ArticleStubPage>.Instance(PrimaryUser.TwitterScreenName)
                .Query(TwitterModel.Instance(PrimaryUser.TwitterScreenName).CONTENT + dayTag).FirstOrDefault() ?? new ArticleStubPage();
        }
        public IEnumerable<ArticleStubPage> ArticleStubPages(Func<Article, bool> where = null)
        {
            var dates = GetArticleStubIndex().ArticleStubPages.Select(x => DateTime.Parse(x.Value)).OrderByDescending(x => x);
            foreach(var date in dates)
            {
                yield return GetArticleStubPage(date);
            }
            yield return null;
        }
        public void ExcludeArticleStub(DateTime date, string slug)
        {
            var dayTag = "_" + date.ToShortDateString();
            var model = new PostworthyArticleModel(PrimaryUser);
            var page = model.GetArticleStubPage(date);

            var article = page.ArticleStubs.Where(s => s.GetSlug() == slug).FirstOrDefault();
            page.ExcludedArticleStubs.Add(article);

            page.ExcludedArticleStubs = page.ExcludedArticleStubs.Distinct().ToList();

            CachedRepository<ArticleStubPage>.Instance(PrimaryUser.TwitterScreenName).Save(TwitterModel.Instance(PrimaryUser.TwitterScreenName).CONTENT + dayTag, page);
        }
        #endregion
    }
}