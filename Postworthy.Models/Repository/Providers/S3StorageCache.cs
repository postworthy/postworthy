using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Postworthy.Models.Repository.Providers
{
    public class S3StorageCache<TYPE> : RepositoryStorageProvider<TYPE> where TYPE : RepositoryEntity
    {
        public override IEnumerable<TYPE> Get(string key)
        {
            throw new NotImplementedException();
        }

        public override void Store(string key, TYPE obj)
        {
            throw new NotImplementedException();
        }

        public override void Store(string key, List<TYPE> obj)
        {
            throw new NotImplementedException();
        }

        public override void Remove(string key, TYPE obj)
        {
            throw new NotImplementedException();
        }

        public override void Remove(string key, List<TYPE> obj)
        {
            throw new NotImplementedException();
        }
    }
}
