using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Postworthy.Models.Repository.Providers;

namespace Postworthy.Models.Repository
{
    public interface IRepository<TYPE> where TYPE : RepositoryEntity
    {
        bool ContainsKey(string key);
        IEnumerable<TYPE> Query(string key, int pageIndex = 0, int pageSize = 100, Func<TYPE, bool> where = null);
        void Save(string key, TYPE obj);
        void Save(string key, IEnumerable<TYPE> objects);
        void Delete(string key);
        void Delete(string key, TYPE obj);
        void Delete(string key, IEnumerable<TYPE> objects);
    }
}
