using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;

namespace TestServer.Support
{
    public static class HelpClass
    {
        public static byte[] ConcatPhoto(byte[] commonPhoto, byte[] newPhoto)
        {
            MemoryStream stream1 = new MemoryStream(commonPhoto);
            Bitmap bmp1 = new Bitmap(stream1);

            MemoryStream stream2 = new MemoryStream(newPhoto);
            Bitmap bmp2 = new Bitmap(stream2);

            int outputImageWidth = bmp1.Width + bmp2.Width;

            int outputImageHeight = bmp1.Height > bmp2.Height ? bmp1.Height : bmp2.Height;

            Bitmap outputImage = new Bitmap(outputImageWidth, outputImageHeight,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (Graphics graphics = Graphics.FromImage(outputImage))
            {
                graphics.DrawImage(bmp1, new Rectangle(new Point(), bmp1.Size),
                    new Rectangle(new Point(), bmp1.Size), GraphicsUnit.Pixel);
                graphics.DrawImage(bmp2, new Rectangle(new Point(bmp1.Width, 0), bmp2.Size),
                    new Rectangle(new Point(), bmp2.Size), GraphicsUnit.Pixel);
            }
            MemoryStream m = new MemoryStream();
            outputImage.Save(m, ImageFormat.Jpeg);
                
            return m.ToArray();
            
        }
    }
}