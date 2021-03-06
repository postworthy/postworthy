﻿using System;
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
                    var end = DateTime.Now.AddMilliseconds(timeout);
                    TcpListener listener = null;
                    try
                    {
                        listener = new TcpListener(ip, port);
                        listener.ExclusiveAddressUse = false;
                        listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
                        listener.Server.ReceiveTimeout = timeout;
                        listener.Start();
                        do
                        {
                            if (listener.Pending())
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
                            }
                            else if (DateTime.Now >= end) 
                                return null;
                        } while (waitForValid);
                    }
                    finally
                    {
                        if (listener != null)
                        {
                            listener.Stop();
                            if (listener.Server != null)
                                listener.Server.Dispose();
                        }
                    }

                    return null;
                });
        }
    }
}
