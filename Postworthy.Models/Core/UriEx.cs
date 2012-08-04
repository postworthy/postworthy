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
    public class UriEx : INotifyPropertyChanged
    {
        private Uri _Uri;
        private string _Title;
        private string _Description;
        private Uri _Image;
        private Uri _Video;
        private bool _IsHtmlContentUrl;
        private bool _IsImageContentUrl;
        private int _UrlTweetCount;
        private int _UrlFacebookShareCount;

        public Uri Uri { get { return _Uri; } set { SetNotifyingProperty("Uri", ref _Uri, value); } }
        public string Title { get { return !string.IsNullOrEmpty(_Title) ? _Title : Uri.ToString(); } set { SetNotifyingProperty("Title", ref _Title, value); } }
        public string Description { get { return _Description; } set { SetNotifyingProperty("Description", ref _Description, value); } }
        public Uri Image { get { return _Image; } set { SetNotifyingProperty("Image", ref _Image, value); } }
        public Uri Video { get { return _Video; } set { SetNotifyingProperty("Video", ref _Video, value); } }
        public bool IsHtmlContentUrl { get { return _IsHtmlContentUrl; } set { SetNotifyingProperty("IsHtmlContentUrl", ref _IsHtmlContentUrl, value); } }
        public bool IsImageContentUrl { get { return _IsImageContentUrl; } set { SetNotifyingProperty("IsImageContentUrl", ref _IsImageContentUrl, value); } }
        public int UrlTweetCount { get { return _UrlTweetCount; } set { SetNotifyingProperty("UrlTweetCount", ref _UrlTweetCount, value); } }
        public int UrlFacebookShareCount { get { return _UrlFacebookShareCount; } set { SetNotifyingProperty("UrlFacebookShareCount", ref _UrlFacebookShareCount, value); } }
        public int ShareCount { get { return UrlTweetCount + UrlFacebookShareCount; } }
        public UriEx() { }

        public UriEx(string uri)
        {
            _Uri = new Uri(uri);
            _Title = Uri.ToString();
        }

        public void Init()
        {
            var htmlUri = _Uri.HtmlContentUri();
            if (htmlUri != null)
            {
                _Uri = htmlUri;
                IsHtmlContentUrl = true;
            }
            else
            {
                var imageUri = _Uri.ImageContentUri();
                if (imageUri != null)
                {
                    _Uri = imageUri;
                    IsImageContentUrl = true;
                }
            }
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Title) ? Uri.ToString() : Title;
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        protected void SetNotifyingProperty<T>(string propertyName, ref T field, T value)
        {
            if (field == null && value == null)
                return;
            if (value != null && value.Equals(field))
                return;
            field = value;
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
