using Postworthy.Models.Repository;
using Postworthy.Models.Twitter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Tasks.LanguageAnalytics.Models
{
    public class LanguageAnalyticsResults : RepositoryEntity
    {
        public Guid RepoGuid { get; set; }
        public DateTime InitialStart { get; set; }
        public long TotalTweetsProcessed { get; set; }
        public List<CountableItem> Keywords { get; set; }
        public List<CountableItem> KeywordSuggestions { get; set; }
        public List<string> KeywordsToIgnore { get; set; }

        public LanguageAnalyticsResults(){
            InitialStart = DateTime.Now;
            RepoGuid = Guid.NewGuid();
            Keywords = new List<CountableItem>();
            KeywordSuggestions = new List<CountableItem>();
            KeywordsToIgnore = new List<string>();
        }

        public override string UniqueKey
        {
            get { return RepoGuid.ToString(); }
        }

        public override bool IsEqual(RepositoryEntity other)
        {
            return other.UniqueKey == this.UniqueKey;
        }
    }
}
