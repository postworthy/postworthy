﻿using AForge.Imaging.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Postworthy.Models.Web
{
    public class ArticleStub : IEquatable<ArticleStub>
    {
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public Uri Link { get; set; }
        public string Image { get; set; }
        public Uri OriginalImageUri { get; set; }
        public string Summary { get; set; }
        public Uri Video { get; set; }

        public string GetSlug(int maxLength = 100)
        {
            string str = WebUtility.HtmlDecode(TaglessTitle().ToLower());

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

        private string _lazyBluredImage = null;
        public string ImageWithLazyBlur()
        {
            if (_lazyBluredImage == null && !string.IsNullOrEmpty(Image))
            {
                var blur = new GaussianBlur(1, 8);
                using (var ms = new MemoryStream(Convert.FromBase64String(Image)))
                using (var outStrm = new MemoryStream())
                using (var png = new Bitmap(ms))
                {
                    blur.ApplyInPlace(png);
                    png.Save(outStrm, System.Drawing.Imaging.ImageFormat.Png);
                    _lazyBluredImage = Convert.ToBase64String(outStrm.ToArray());
                }
            }

            return _lazyBluredImage;
        }

        public string TaglessTitle()
        {
            string str = WebUtility.HtmlDecode(Title);

            var tags = new System.Text.RegularExpressions.Regex(@"</?\w+((\s+\w+(\s*=\s*(?:"".*?""|'.*?'|[^'"">\s]+))?)+\s*|\s*)/?>", System.Text.RegularExpressions.RegexOptions.Singleline);

            return tags.Replace(str, "");
        }

        public string TaglessSubTitle()
        {
            string str = WebUtility.HtmlDecode(SubTitle);

            var tags = new System.Text.RegularExpressions.Regex(@"</?\w+((\s+\w+(\s*=\s*(?:"".*?""|'.*?'|[^'"">\s]+))?)+\s*|\s*)/?>", System.Text.RegularExpressions.RegexOptions.Singleline);

            return tags.Replace(str, "");
        }

        public string GetSummary(int length = 1100)
        {
            var s = Summary.Trim();
            if (s.Length > length)
            {
                var end = s.IndexOf('.', length - 1);
                if (end > -1)
                    return s.Substring(0, end + 1);
                else
                    return s.Substring(0, length - 1);
            }
            else
                return s;
        }

        public bool Equals(ArticleStub other)
        {
            return Link.ToString() == other.Link.ToString() || Title == other.Title && Summary == other.Summary;
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() == typeof(ArticleStub))
                return Equals((ArticleStub)obj);
            else
                return false;
        }
    }
}
