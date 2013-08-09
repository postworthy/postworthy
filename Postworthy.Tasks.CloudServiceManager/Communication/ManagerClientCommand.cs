using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace Postworthy.Tasks.CloudServiceManager.Communication
{
    public class ManagerClientCommand
    {
        public IPAddress IPAddress { get; set; }
        public KeyValuePair<string,string> Command { get; set; }
    }
}
