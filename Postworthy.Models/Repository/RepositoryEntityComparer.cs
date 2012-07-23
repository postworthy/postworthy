using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Postworthy.Models.Repository
{
    class RepositoryEntityComparer : IEqualityComparer<RepositoryEntity>
    {
        public bool Equals(RepositoryEntity re1, RepositoryEntity re2)
        {
            if (re1.UniqueKey == re2.UniqueKey)
                return true;
            else
                return false;
        }


        public int GetHashCode(RepositoryEntity re)
        {
            return re.UniqueKey.GetHashCode();
        }
    }
}
