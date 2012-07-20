using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Postworthy.Models.Core
{
    public static class FileUtility
    {
        private static string StoragePath = null;
        public static string GetPath(string fileName)
        {
            if (string.IsNullOrEmpty(StoragePath))
            {
                StoragePath = Path.GetTempPath() + "longtermstorage/";
                if (!Directory.Exists(StoragePath))
                    Directory.CreateDirectory(StoragePath);
            }
            return StoragePath + fileName;
        }
    }
}
