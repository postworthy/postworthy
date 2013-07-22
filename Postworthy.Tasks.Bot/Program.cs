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

namespace Postworthy.Tasks.Bot
{
    class Program
    {
        private const int TCP_PORT = 49152;
        static void Main(string[] args)
        {
            if (!EnsureSingleLoad())
            {
                Console.WriteLine("{0}: Another Instance Currently Runing", DateTime.Now);
                return;
            }

            if (UsersCollection.PrimaryUser() == null)
            {
                Console.WriteLine("{0}: No Primary User Found", DateTime.Now);
                ListenForPrimaryUser();
            }

            var streamMonitor = new DualStreamMonitor(Console.Out);
            streamMonitor.Start();

            while (Console.ReadLine() != "exit") ;
            streamMonitor.Stop();
        }

        private static void ListenForPrimaryUser()
        {
            Console.WriteLine("{0}: Waiting For BotManager", DateTime.Now);
            var listener = new TcpListener(IPAddress.Any, TCP_PORT);
            listener.Start();
            while (UsersCollection.PrimaryUser() == null)
            {
                var client = listener.AcceptTcpClient();
                var stream = new StreamReader(client.GetStream());
                var data = new byte[4096];
                try
                {
                    var message = stream.ReadLine();

                    if (!string.IsNullOrEmpty(message) && message.StartsWith("PrimaryUser:"))
                    {
                        Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                        config.AppSettings.Settings.Add(new KeyValueConfigurationElement("PrimaryUser", message.Split(':')[1]));
                        config.AppSettings.SectionInformation.ForceSave = true;
                        config.Save(ConfigurationSaveMode.Modified);
                        ConfigurationManager.RefreshSection("appSettings");
                    }

                    client.Close();
                }
                catch { }
            }
            listener.Stop();

            Console.WriteLine("{0}: BotManager Set PrimaryUser as {1}", DateTime.Now, UsersCollection.PrimaryUser().TwitterScreenName);
        }

        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.Bot", out result);

            return result;
        }
    }
}
