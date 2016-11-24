//|---------------------------------------------------------------------------------------------|
//|  Big image clsss is a image with longitude latitude range and pixel range , google level    |
//|  it can get a sub image                                                                     |
//|---------------------------------------------------------------------------------------------|
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace TileDataTransformTool
{
    /// <summary>
    /// big image combinate by google tiles
    /// </summary>
     public class BigImage
    {
        private Image bigImg = null;
        /// <summary>
        /// the combination image
        /// </summary>
         public Image BigImg
        {
            get { return this.bigImg; }
            set { this.bigImg = value; }
        }

        Envelope imgRange = null;
         /// <summary>
         /// longitude latitude range box
         /// </summary>
        public Envelope ImgRange
        {
            get { return imgRange; }
            set { imgRange = value; }
        }

         /// <summary>
         /// pixel range box
         /// </summary>
        public PixelBound PixelBox;
        
         public int googleLevel = 0;

        public void Dispose()
        {
            if (bigImg != null)
            {
                bigImg.Dispose();
                bigImg = null;
            }
        }
         /// <summary>
         /// get sub image by longitude latitude range
         /// </summary>
         /// <param name="lonlatbox"></param>
         /// <param name="width"></param>
         /// <param name="height"></param>
         /// <returns></returns>
        public Image GetSubImage(Envelope lonlatbox, int width, int height)
        {
            if (this.bigImg == null || this.imgRange == null  || !this.imgRange.Contains(lonlatbox))
            {
                MessageBox.Show("GetSubImage wrong,");
                return null;
            }

            PixelBound box = new PixelBound();
            if (DBTranslateFactory.LonLatBound2PixelBound(this.googleLevel, lonlatbox, ref box))
            {
                //double boxwidth = Math.Abs(this.PixelBox.maxPX - this.PixelBox.minPX);
                //double imgwidth = this.bigImg.Width;
                //Rectangle _SourceRect = new Rectangle((int)((double)(box.minPX - this.PixelBox.minPX) / boxwidth * imgwidth), (int)((double)(box.minPY - this.PixelBox.minPY) / boxwidth * imgwidth), (int)((double)(box.maxPX - box.minPX) / boxwidth * imgwidth), (int)((double)(box.maxPY - box.minPY) / boxwidth * imgwidth));
                Rectangle _SourceRect = new Rectangle(box.minPX - this.PixelBox.minPX, box.minPY - this.PixelBox.minPY, box.maxPX - box.minPX, box.maxPY - box.minPY);
                Rectangle _TargetRect = new Rectangle(0, 0, width, height);
                Bitmap _CanvasBitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                System.Drawing.Graphics _CanvasGraphics = System.Drawing.Graphics.FromImage(_CanvasBitmap);
                _CanvasGraphics.Clear(Color.Yellow);
                _CanvasGraphics.CompositingQuality = CompositingQuality.HighQuality;
                _CanvasGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                _CanvasGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                _CanvasGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                _CanvasGraphics.DrawImage(this.bigImg, _TargetRect, _SourceRect, GraphicsUnit.Pixel);

                _CanvasGraphics.Dispose();
                _CanvasGraphics = null;

                return _CanvasBitmap;
            }

            return null;
        }
        
    }

     /// <summary>
     /// pixcel range box , start from left top , to right is x plus , to botoom is y minus
     /// </summary>
     public class PixelBound
     {
         public int minPX;
         public int minPY;
         public int maxPX;
         public int maxPY;

         public bool IsValid()
         {
             if (minPX >= maxPX || minPY >= maxPY)
             {
                 return false;
             }
             return true;
         }
     }
}
