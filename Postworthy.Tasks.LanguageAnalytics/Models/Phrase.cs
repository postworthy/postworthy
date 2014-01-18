using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Tasks.LanguageAnalytics.Models
{
    public class Phrase
    {
        public int Count { get; set; }
        public List<WordNode> Words { get; set; }

        public Phrase()
        {
            Words = new List<WordNode>();
        }
    }
}
