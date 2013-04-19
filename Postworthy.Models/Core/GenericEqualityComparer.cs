using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Postworthy.Models.Core
{
    public class GenericEqualityComparer<T> : IEqualityComparer<T>
    {
        public Func<T, T, bool> EqualsFunc { get; set; }
        public Func<T, int> GetHashCodeFunc { get; set; }

        #region IEqualityComparer<T> Members

        public bool Equals(T x, T y)
        {
            return EqualsFunc(x, y);
        }

        public int GetHashCode(T obj)
        {
            return GetHashCodeFunc(obj);
        }

        #endregion
    }

    public static class GenericEqualityComparerFactory<T>
    {
        public static IEqualityComparer<T> Build(Func<T, T, bool> Equals, Func<T, int> GetHashCode)
        {
            return new GenericEqualityComparer<T>() { EqualsFunc = Equals, GetHashCodeFunc = GetHashCode };
        }
    }
}
