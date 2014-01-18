using Postworthy.Models.Account;
using Postworthy.Tasks.StreamMonitor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Tasks.LanguageAnalytics
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

            if (UsersCollection.PrimaryUser() == null)
                Console.WriteLine("{0}: No Primary User Found", DateTime.Now);
            else
            {
                var streamMonitor = new DualStreamMonitor(Console.Out);
                streamMonitor.Start();

                while (Console.ReadLine() != "exit") ;

                streamMonitor.Stop();
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
