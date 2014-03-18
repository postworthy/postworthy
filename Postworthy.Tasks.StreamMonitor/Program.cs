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

            var streamMonitors = new List<DualStreamMonitor>();

            UsersCollection.PrimaryUsers().AsParallel().ForAll(u =>
            {
                var streamMonitor = new DualStreamMonitor(u, Console.Out);
                streamMonitor.Start();

                lock (streamMonitors)
                {
                    streamMonitors.Add(streamMonitor);
                }
            });

            while (Console.ReadLine() != "exit") ;

            streamMonitors.ForEach(s => s.Stop());
        }



        private static bool EnsureSingleLoad()
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "Postworthy.Tasks.StreamMonitor", out result);

            return result;
        }
    }
}
