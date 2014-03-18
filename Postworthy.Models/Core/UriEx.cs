using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Threading;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Postworthy.Models.Core
{
    public class UriEx : IEquatable<UriEx>
    {
        private string _Title;
     
        public Uri Uri { get; set; }
        public string Title { get { return !string.IsNullOrEmpty(_Title) ? _Title : Uri.ToString(); } set { _Title = value; } }
        public string Description { get; set; }
        public Uri Image { get; set; }
        public Uri Video { get; set; }
        public bool IsHtmlContentUrl { get; set; }
        public bool IsImageContentUrl { get; set; }
        public int UrlTweetCount { get; set; }
        public int UrlFacebookShareCount { get; set; }
        public int ShareCount { get { return UrlTweetCount + UrlFacebookShareCount; } }
        public UriEx() { }

        public UriEx(string uri)
        {
            Uri = new Uri(uri);
            Title = Uri.ToString();
        }

        public void Init()
        {
            var htmlUri = Uri.HtmlContentUri();
            if (htmlUri != null)
            {
                Uri = htmlUri;
                IsHtmlContentUrl = true;
            }
            else
            {
                var imageUri = Uri.ImageContentUri();
                if (imageUri != null)
                {
                    Uri = imageUri;
                    IsImageContentUrl = true;
                }
            }
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Title) ? Uri.ToString() : Title;
        }

        public bool Equals(UriEx other)
        {
            return Uri.Equals(other.Uri);
        }

        public override int GetHashCode()
        {
            return Uri.GetHashCode();
        }

    }
}
