using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Postworthy.Models.Account;
using Postworthy.Tasks.StreamMonitor;
using System.Net.Sockets;
using System.Net;
using System.Configuration;
using System.IO;
using Postworthy.Models.Communication;
using Postworthy.Tasks.Bot.Communication;
using System.Threading;

namespace Postworthy.Tasks.Bot
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!EnsureSingleLoad())
            {
                Console.WriteLine("{0}: Another Instance Currently Running", DateTime.Now);
                return;
            }

            var botClient = new BotClient(HandleBotManagerCommunication);
            botClient.Start();

            if (UsersCollection.PrimaryUser() == null)
            {
                Console.WriteLine("{0}: No Primary User Found", DateTime.Now);
                Console.Write("{0}: Waiting For PrimaryUser to be Set by BotManager", DateTime.Now);
                while (UsersCollection.PrimaryUser() == null)
                {
                    Thread.Sleep(1000);
                    Console.Write(".");
                }
                Console.WriteLine("");
                Console.WriteLine("{0}: BotManager Set PrimaryUser to be {1}", DateTime.Now, UsersCollection.PrimaryUser().TwitterScreenName);
            }

            var streamMonitor = new DualStreamMonitor(Console.Out);
            streamMonitor.Start();

            while (Console.ReadLine() != "exit") ;
            botClient.Stop();
            streamMonitor.Stop();
        }

        private static void HandleBotManagerCommunication(KeyValuePair<string,string> command)
        {
            switch (command.Key.ToLower())
            {
                case "primaryuser":
                    Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    config.AppSettings.Settings.Add(new KeyValueConfigurationElement("PrimaryUser", command.Value));
                    config.AppSettings.SectionInformation.ForceSave = true;
                    config.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                    break;
                default:
                    Console.WriteLine("{0}: Unknown BotManager Command: {1}:{2}", DateTime.Now, command.Key, command.Value);
                    break;
            }
        }

        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.Bot", out result);

            return result;
        }
    }
}
