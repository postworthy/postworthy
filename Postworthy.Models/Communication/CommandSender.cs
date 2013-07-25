using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Models.Communication
{
    public class CommandSender
    {
        public Task<bool> Send(IPAddress ip, int port, KeyValuePair<string, string> command, bool waitForSend = false)
        {
            return Task<bool>.Factory.StartNew(() =>
            {
                bool isSent = false;
                do
                {
                    try
                    {
                        using (var client = new TcpClient())
                        {
                            client.Connect(new IPEndPoint(ip, port));
                            using (var stream = new StreamWriter(client.GetStream()))
                            {
                                stream.WriteLine(command.Key + ":" + command.Value);
                                isSent = true;
                            }
                        }
                    }
                    catch { }
                } while (waitForSend && !isSent);

                return isSent;
            });
        }
    }
}
