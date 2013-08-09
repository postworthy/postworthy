using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Postworthy.Tasks.CloudServiceManager.Azure;
using Postworthy.Tasks.CloudServiceManager.Communication;

namespace Postworthy.Tasks.CloudServiceManager
{
    class Program
    {
        static void Main(string[] args)
        {
            var managerClient = new ManagerClient();
            managerClient.Start();

            var vmManager = new VirtualMachineManager();
            vmManager.AddVirtualMachine("test");

            managerClient.QueueCommand(new ManagerClientCommand()
            {
                IPAddress = IPAddress.Parse("127.0.0.1"),
                Command = new KeyValuePair<string, string>("PrimaryUser", "bestincode")
            });
        }
    }
}
