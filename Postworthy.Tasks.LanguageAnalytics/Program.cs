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

            var users = UsersCollection.PrimaryUsers() ?? new List<PostworthyUser>();

            users.AsParallel().ForAll(u =>
            {
                var streamMonitor = new DualStreamMonitor(u, Console.Out);
                streamMonitor.Start();

                while (Console.ReadLine() != "exit") ;

                streamMonitor.Stop();
            });
        }

        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.LanguageAnalytics", out result);

            return result;
        }
    }
}
