using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postworthy.Models.Core
{
    public static class ImageManipulation
    {
        public static string EncodeImage(Bitmap bmp, int width = 0, int height = 0)
        {
            width = width == 0 ? bmp.Width : width;
            height = height == 0 ? bmp.Height : height;
            Bitmap result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage((Image)result))
            {
                g.DrawImage(bmp, 0, 0, width, height);
            }
            bmp.Dispose();

            using (var stream = new MemoryStream())
            {
                result.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        public static Bitmap DecodeImage(string ImageBase64Encoded)
        {
            using(var stream = new MemoryStream(Convert.FromBase64String(ImageBase64Encoded)))
            {
                return (Bitmap)Bitmap.FromStream(stream);
            }
        }
    }
}
