using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Postworthy.Models.Account;
using Postworthy.Tasks.StreamMonitor;

namespace Postworthy.Tasks.Bot
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

            var streamMonitor = new DualStreamMonitor(Console.Out);
            streamMonitor.Start();

            while (Console.ReadLine() != "exit") ;
            streamMonitor.Stop();
        }



        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.Bot." + UsersCollection.PrimaryUser().TwitterScreenName, out result);

            return result;
        }
    }
}
