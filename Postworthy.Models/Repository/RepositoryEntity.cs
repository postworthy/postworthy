using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel;

namespace Postworthy.Models.Repository
{
    public static class RepositoryListExtensions
    {
        public static void Update<T>(this List<T> list, T item) where T : RepositoryEntity
        {
            int index = list.IndexOf(item);
            if (index > -1)
            {
                list.RemoveAt(index);
                list.Insert(index, item);
            }
            else
                list.Add(item);
        }
    }

    [Serializable]
    public abstract class RepositoryEntity : INotifyPropertyChanged, IEquatable<RepositoryEntity>
    {
        public abstract string UniqueKey { get; }
        public string RepositoryKey { get; set; }

        public RepositoryEntity()
        {
        }

        public abstract bool IsEqual(RepositoryEntity other);

        public override int GetHashCode()
        {
            return UniqueKey.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RepositoryEntity);
        }

        public bool Equals(RepositoryEntity other)
        {
            if (other != null)
                return IsEqual(other);
            else
                return false;
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