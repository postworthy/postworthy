using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Postworthy.Models.Repository
{
    public abstract class RepositoryStorageProvider<TYPE> where TYPE : RepositoryEntity
    {
        public abstract IEnumerable<TYPE> Get(string key);
        public abstract void Store(string key, TYPE obj);
        public abstract void Store(string key, List<TYPE> obj);
        public abstract void Remove(string key, TYPE obj);
        public abstract void Remove(string key, List<TYPE> obj);

        protected string Serialize(object obj)
        {
            try
            {
                return JsonConvert.SerializeObject(obj);
            }
            catch(System.ArgumentException ex) 
            {
                if (ex.Message == "dateTime is invalid and Kind is Local")
                    return "";
                else throw;
            }
        }
        protected RET Deserialize<RET>(string json)
        {
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    return JsonConvert.DeserializeObject<RET>(json);
                }
                catch { }
            }
            
            return default(RET);
        }
    }
}
