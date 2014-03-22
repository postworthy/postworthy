using Postworthy.Models.Repository;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Postworthy.Models.Web
{
    public class Article : RepositoryEntity
    {
        [Required]
        public Guid ArticleID { get; set; }
        [Required]
        public string Title { get; set; }
        [Required]
        [Display(Name = "Sub Title")]
        public string SubTitle { get; set; }
        public DateTime PublishedDate { get; set; }
        public string PublishedBy { get; set; }
        public List<string> Images { get; set; }
        public Uri Video { get; set; }

        [Required]
        [DataType(DataType.MultilineText)]
        public string Content { get; set; }
        public List<string> Tags { get; set; }

        public Article()
        {
            ArticleID = Guid.NewGuid();
            Images = new List<string>();
            Tags = new List<string>();
            PublishedDate = DateTime.Now;
        }

        public string GetSlug(int maxLength = 100)
        {
            string str = WebUtility.HtmlDecode(Title.ToLower());

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
        public override string UniqueKey
        {
            get { return "Article_" + ArticleID.ToString(); }
        }

        public override bool IsEqual(RepositoryEntity other)
        {
            return UniqueKey == other.UniqueKey;
        }
    }
}
