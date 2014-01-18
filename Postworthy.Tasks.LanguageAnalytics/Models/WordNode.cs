using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Tasks.LanguageAnalytics.Models
{
    public class WordNode : IEquatable<WordNode>
    {
        private string word = "";
        public int Hash { get; set; }
        public string Word
        {
            get { return word; }
            set
            {
                word = value;
                Hash = value.GetHashCode();
            }
        }
        public int Count { get; set; }
        public List<CountableItem<WordNode>> Pre { get; set; }
        public List<CountableItem<WordNode>> Post { get; set; }
        public WordNode()
        {
            Pre = new List<CountableItem<WordNode>>();
            Post = new List<CountableItem<WordNode>>();
        }
        public List<List<WordNode>> Phrases(int depth)
        {
            var phrases = new List<List<WordNode>>();

            if (depth > 0)
            {
                foreach (var post in Post)
                {
                    var childPhrases = post.Key.Phrases(depth - 1);
                    foreach(var childPhrase in childPhrases)
                    {
                        childPhrase.Insert(0, this);
                    }
                    phrases.AddRange(childPhrases);
                }
            }
            else
            {
                phrases.Add(new List<WordNode>() { this });
            }

            return phrases;
        }
        bool IEquatable<WordNode>.Equals(WordNode other)
        {
            return this.Hash == other.Hash;
        }
    }
}
