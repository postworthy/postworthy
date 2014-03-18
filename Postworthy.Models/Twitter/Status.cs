using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Models.Twitter
{
    public class Status
    {
        public class MediaEntity
        {
            public string Url { get; set; }
            public string ExpandedUrl { get; set; }
        }
        public class UserEntity
        {
            public ulong Id { get; set; }
            public string ScreenName { get; set; }
            public string Name { get; set; }
        }
        public class EntityCollection
        {
            public List<MediaEntity> UrlEntities { get; set; }
            public List<MediaEntity> MediaEntities { get; set; }
            public List<UserEntity> UserMentionEntities { get; set; }

            public EntityCollection()
            {
                UrlEntities = new List<MediaEntity>();
                MediaEntities = new List<MediaEntity>();
                UserMentionEntities = new List<UserEntity>();
            }
        }
        public ulong StatusID { get; set; }
        public string Text { get; set; }
        public int RetweetCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public User User { get; set; }
        public EntityCollection Entities { get; set; }
        public bool Retweeted { get; set; }
        public Status RetweetedStatus { get; set; }

        public Status() { }
        public Status(LinqToTwitter.Status status)
        {
            StatusID = status.StatusID;
            Text = status.Text;
            RetweetCount = status.RetweetCount;
            Retweeted = status.Retweeted;

            if (Retweeted) 
                RetweetedStatus = new Status(status.RetweetedStatus);

            CreatedAt = status.CreatedAt;
            User = new User(status.User);

            Entities = new EntityCollection();

            if (status.Entities != null)
            {
                if (status.Entities.UrlEntities != null)
                {
                    foreach (var ent in status.Entities.UrlEntities)
                    {
                        Entities.UrlEntities.Add(new MediaEntity()
                        {
                            ExpandedUrl = ent.ExpandedUrl,
                            Url = ent.Url
                        });
                    }
                }
                if (status.Entities.MediaEntities != null)
                {
                    foreach (var ent in status.Entities.MediaEntities)
                    {
                        Entities.MediaEntities.Add(new MediaEntity()
                        {
                            ExpandedUrl = ent.ExpandedUrl,
                            Url = ent.Url
                        });
                    }
                }
                if (status.Entities.UserMentionEntities != null)
                {
                    foreach (var ent in status.Entities.UserMentionEntities)
                    {
                        Entities.UserMentionEntities.Add(new UserEntity()
                        {
                            Id = ent.Id,
                            ScreenName = ent.ScreenName,
                            Name = ent.Name
                        });
                    }
                }
            }
        }
    }
}
