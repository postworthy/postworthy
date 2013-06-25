using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Postworthy.Models.Repository.Providers
{
    public class PassThroughCache<TYPE> : RepositoryStorageProvider<TYPE> where TYPE : RepositoryEntity
    {
        public override List<TYPE> Get(string key, int limit)
        {
            return null;
        }

        public override void Store(string key, TYPE obj)
        {
            
        }

        public override void Store(string key, List<TYPE> obj)
        {
            
        }

        public override void Remove(string key, TYPE obj)
        {
            
        }

        public override void Remove(string key, List<TYPE> obj)
        {
            
        }
    }
}
