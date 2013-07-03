using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Postworthy.Models.Repository;
using Postworthy.Tasks.Bot.Models;

namespace Postworthy.Web.Bot.Controllers
{
    [Authorize]
    public class BotCommandController : Controller
    {
        private const string COMMAND_REPO_KEY = "BotCommands";

        public string RepoKey
        {
            get
            {
                return User.Identity.Name + "_" + COMMAND_REPO_KEY;
            }
        }
        private Repository<BotCommand> commandRepo = Repository<BotCommand>.Instance;
        public JsonResult Refresh()
        {
            var command = new BotCommand() { Value = DateTime.Now.ToString(), Command = BotCommand.CommandType.Refresh };
            commandRepo.Save(RepoKey, command);
            return Json(command, JsonRequestBehavior.AllowGet);
        }
        public JsonResult RemovePotentialTweet(string id)
        {
            var command = new BotCommand() { Value = id, Command = BotCommand.CommandType.RemovePotentialTweet };
            commandRepo.Save(RepoKey, command);
            return Json(command, JsonRequestBehavior.AllowGet);
        }
        public JsonResult RemovePotentialRetweet(string id)
        {
            var command = new BotCommand() { Value = id, Command = BotCommand.CommandType.RemovePotentialRetweet };
            commandRepo.Save(RepoKey, command);
            return Json(command, JsonRequestBehavior.AllowGet);
        }
        public JsonResult AddKeyword(string id)
        {
            var command = new BotCommand() { Value = id, Command = BotCommand.CommandType.AddKeyword };
            commandRepo.Save(RepoKey, command);
            return Json(command, JsonRequestBehavior.AllowGet);
        }
        public JsonResult IgnoreKeyword(string id)
        {
            var command = new BotCommand() { Value = id, Command = BotCommand.CommandType.IgnoreKeyword };
            commandRepo.Save(RepoKey, command);
            return Json(command, JsonRequestBehavior.AllowGet);
        }
        public JsonResult TargetTweep(string id)
        {
            var command = new BotCommand() { Value = id, Command = BotCommand.CommandType.TargetTweep };
            commandRepo.Save(RepoKey, command);
            return Json(command, JsonRequestBehavior.AllowGet);
        }
        public JsonResult IgnoreTweep(string id)
        {
            var command = new BotCommand() { Value = id, Command = BotCommand.CommandType.IgnoreTweep };
            commandRepo.Save(RepoKey, command);
            return Json(command, JsonRequestBehavior.AllowGet);
        }
    }
}
