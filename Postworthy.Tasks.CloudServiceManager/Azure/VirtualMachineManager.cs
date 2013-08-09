using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Postworthy.Tasks.CloudServiceManager.Azure
{
    public class VirtualMachineManager
    {
        private X509Certificate2 GetCertificate()
        {
            string certThumb = ConfigurationManager.AppSettings.Get("CertificateThumbprint");
            X509Store certificateStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            certificateStore.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection certs = certificateStore.Certificates.Find(
                X509FindType.FindByThumbprint,
                certThumb,
                false);

            if (certs.Count > 0) return certs[0];
            else throw new Exception(string.Format("Certificate matching {0} not found!", certThumb));
        }

        public bool AddVirtualMachine(string nameVM)
        {
            string subscriptionID = ConfigurationManager.AppSettings.Get("SubscriptionID");
            string serviceName = ConfigurationManager.AppSettings.Get("ServiceName");
            string adminPass = ConfigurationManager.AppSettings.Get("AdminPassword");
            string mediaLink = ConfigurationManager.AppSettings.Get("MediaLink");
            string sourceImageName = ConfigurationManager.AppSettings.Get("SourceImageName");

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(new Uri("https://management.core.windows.net/" + subscriptionID
            + "/services/hostedservices/" + serviceName + "/deployments"));

            request.Method = "POST";
            request.ClientCertificates.Add(GetCertificate());
            request.ContentType = "application/xml";
            request.Headers.Add("x-ms-version", "2012-03-01");

            // Add body to the request
            var xdoc = XDocument.Load("..\\..\\AddVirtualMachine.xml");
            var root = xdoc.Elements().First();

            root.Elements().Single(x => x.Name.LocalName == "Name").Value = serviceName;
            root.Elements().Single(x => x.Name.LocalName == "Label").Value = serviceName;
            var role = root.Elements().Single(x => x.Name.LocalName == "RoleList").Elements().First(x => x.Name.LocalName == "Role");
            role.Elements().Single(x => x.Name.LocalName == "RoleName").Value = nameVM;
            var configSet = role.Elements().Single(x => x.Name.LocalName == "ConfigurationSets").Elements().First(x => x.Name.LocalName == "ConfigurationSet");
            configSet.Elements().Single(x => x.Name.LocalName == "ComputerName").Value = nameVM;
            configSet.Elements().Single(x => x.Name.LocalName == "AdminPassword").Value = adminPass;
            var hardDisk = role.Elements().Single(x => x.Name.LocalName == "OSVirtualHardDisk");
            //hardDisk.Elements().Single(x => x.Name.LocalName == "MediaLink").Value = mediaLink;
            hardDisk.Elements().Single(x => x.Name.LocalName == "SourceImageName").Value = sourceImageName;

            Stream requestStream = request.GetRequestStream();
            StreamWriter streamWriter = new StreamWriter(requestStream, System.Text.UTF8Encoding.UTF8);
            xdoc.Save(streamWriter);

            streamWriter.Close();
            requestStream.Close();

            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                return response.StatusCode == HttpStatusCode.Created;
            }
            catch (Exception ex) { return false; }
            
        }
    }
}
