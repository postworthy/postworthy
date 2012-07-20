using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Postworthy.Models.Repository
{
    public class RepositorySingleton<TYPE> : RepositoryEntity
    {
        public string Key { get; set; }
        public TYPE Data { get; set; }
        public DateTime CreatedOn { get; set; }
        public RepositorySingleton() 
        {
            CreatedOn = DateTime.Now;
        }
        public RepositorySingleton(string key)
        {
            this.Key = key;
            CreatedOn = DateTime.Now;
        }
        public override string UniqueKey
        {
            get { return Key; }
        }

        public override bool IsEqual(RepositoryEntity other)
        {
            return UniqueKey == UniqueKey;
        }
    }
}
