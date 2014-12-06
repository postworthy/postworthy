using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Models.Core
{
    public static class ObjectExtensions
    {
        public static uint GetUintHashCode(this object obj)
        {
            int hc = obj.GetHashCode();
            if (hc > 0)
                return Convert.ToUInt32(hc);
            else
                return Convert.ToUInt32(Math.Abs(hc)) + (Convert.ToUInt32(int.MaxValue) + 1);
        }
    }
}
