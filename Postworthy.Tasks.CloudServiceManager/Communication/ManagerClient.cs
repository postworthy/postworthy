using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Postworthy.Models.Communication;

namespace Postworthy.Tasks.CloudServiceManager.Communication
{
    public class ManagerClient : IDisposable
    {
        private const int TCP_PORT = 59152;
        private Thread botThread = null;
        private volatile bool _stopBot = false;
        private List<ManagerClientCommand> PendingCommands;
        public ManagerClient()
        {
            PendingCommands = new List<ManagerClientCommand>();
            botThread = new Thread(new ThreadStart(Work));
        }

        public void QueueCommand(ManagerClientCommand command)
        {
            lock (PendingCommands)
            {
                PendingCommands.Add(command);
            }
        }

        private void Work()
        {
            var sender = new CommandSender();
            while (!_stopBot)
            {
                List<ManagerClientCommand> localPendingCommands = null;
                lock(PendingCommands)
                {
                    localPendingCommands = PendingCommands;
                    PendingCommands = new List<ManagerClientCommand>();
                }
                var tasks = new List<Task<bool>>();
                foreach (var command in localPendingCommands)
                {
                    var task = sender.Send(command.IPAddress, TCP_PORT, command.Command, true); 
                }
                Task<bool>.WaitAll(tasks.ToArray());
            }
        }

        public void Start()
        {
            _stopBot = false;
            botThread.Start();
        }

        public void Stop()
        {
            _stopBot = true;
            botThread.Join();
        }

        public void Dispose()
        {
            this.Stop();
            botThread = null;
        }
    }
}
