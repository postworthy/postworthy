using Postworthy.Models.Communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Postworthy.Tasks.Bot.Communication
{
    public class BotClient : IDisposable
    {
        private const int TCP_PORT = 59152;
        private Thread botThread = null;
        private Action<KeyValuePair<string, string>> handleCommands;
        private volatile bool _stopBot = false;
        public BotClient(Action<KeyValuePair<string,string>> handleCommands)
        {
            botThread = new Thread(new ThreadStart(Work));
            this.handleCommands = handleCommands;
        }

        private void Work()
        {
            var listener = new CommandListener();
            while (!_stopBot)
            {
                var task = listener.Listen(IPAddress.Any, TCP_PORT, 0, true);
                task.Wait();
                if (task.Result.HasValue)
                    handleCommands(task.Result.Value);
                else if (task.IsFaulted)
                    throw task.Exception;
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
            handleCommands = null;
        }
    }
}
