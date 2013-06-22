using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Postworthy.Models.Repository;

namespace Postworthy.Tasks.Bot.Models
{
    public class BotCommand : RepositoryEntity
    {
        public enum CommandType
        {
            RemovePotentialTweet,
            RemovePotentialRetweet,
            AddKeyword,
            IgnoreKeyword,
            TargetTweep,
            IgnoreTweep,
        }
        public Guid ID { get; set; }
        public CommandType Command { get; set; }
        public string Value { get; set; }
        public bool HasBeenExecuted { get; set; }

        public BotCommand()
        {
            ID = Guid.NewGuid();
        }

        public override string UniqueKey
        {
            get { return "BotCommand_" + ID.ToString(); }
        }

        public override bool IsEqual(RepositoryEntity other)
        {
            return this.UniqueKey == other.UniqueKey;
        }
    }
}
