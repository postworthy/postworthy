using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Postworthy.Models.Account;
using Postworthy.Models.Twitter;
using LinqToTwitter;
using System.Configuration;
using System.Timers;
using Postworthy.Models.Repository;
using Postworthy.Models.Streaming;
using SignalR.Client.Hubs;
using System.Net;
using System.Threading.Tasks;
using System.Runtime.ConstrainedExecution;

namespace Postworthy.Tasks.StreamMonitor
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!EnsureSingleLoad())
            {
                Console.WriteLine("{0}: Another Instance Currently Runing", DateTime.Now);
                return;
            }

            var streamMonitor = new StreamMonitor(Console.Out);
            streamMonitor.Start();

            while (Console.ReadLine() != "exit") ;
            streamMonitor.Stop();
        }

        
        
        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.StreamMonitor." + UsersCollection.PrimaryUser().TwitterScreenName, out result);

            return result;
        }
    }
}
