using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Postworthy.Models.Repository.Providers
{
    public class PassThroughCache<TYPE> : RepositoryStorageProvider<TYPE> where TYPE : RepositoryEntity
    {
        public override IEnumerable<TYPE> Get(string key)
        {
            return null;
        }

        public override TYPE Single(string collectionKey, string itemKey)
        {
            return null;
        }

        public override void Store(string key, TYPE obj)
        {
            
        }

        public override void Store(string key, IEnumerable<TYPE> obj)
        {
            
        }

        public override void Remove(string key, TYPE obj)
        {
            
        }

        public override void Remove(string key, IEnumerable<TYPE> obj)
        {
            
        }

        public PassThroughCache(string providerKey) : base(providerKey) { }
    }
}
