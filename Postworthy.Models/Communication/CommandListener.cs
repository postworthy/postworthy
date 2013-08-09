using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Postworthy.Models.Communication
{
    public class CommandListener
    {
        public Task<KeyValuePair<string, string>?> Listen(IPAddress ip, int port, int timeout = 0, bool waitForValid = false)
        {
            return Task<KeyValuePair<string, string>?>.Factory.StartNew(() =>
                {
                    TcpListener listener = null;
                    try
                    {
                        listener = new TcpListener(ip, port);
                        listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
                        listener.Server.ReceiveTimeout = timeout;
                        listener.Start();
                        do
                        {
                            using (var client = listener.AcceptTcpClient())
                            {
                                using (var stream = new StreamReader(client.GetStream()))
                                {
                                    var data = new byte[4096];
                                    try
                                    {
                                        var message = stream.ReadLine();
                                        var split = message.Split(':');
                                        if (split.Length == 2)
                                        {
                                            return new KeyValuePair<string, string>(split[0], split[1]);
                                        }
                                    }
                                    catch { }
                                }
                             
                            }
                        } while (waitForValid);
                    }
                    finally
                    {
                        if (listener != null)
                            listener.Stop();
                    }

                    return null;
                });
        }
    }
}
