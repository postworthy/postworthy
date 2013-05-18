using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Postworthy.Models.Streaming
{
    public interface IKeywordSuggestionStep
    {
        void SetIgnoreKeywords(List<string> keywords);
        void ResetHasNewKeywordSuggestions();
        bool HasNewKeywordSuggestions();
        List<string> GetKeywordSuggestions();
    }
}
