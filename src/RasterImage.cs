// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace EftPosition
{
    sealed class RasterImage : IDisposable
    {
        readonly List<Bitmap> levels=new List<Bitmap>();
        readonly ImageAttributes edges=new ImageAttributes();
        public int Width {get;private set;}
        public int Height {get;private set;}
        public long Bytes {get {long size=0;foreach(Bitmap image in levels)size+=(long)image.Width*image.Height*4;return size;}}
        public RasterImage(string path,int width,int height)
        {
            Width=width;Height=height;edges.SetWrapMode(WrapMode.TileFlipXY);
            try
            {
                using(Image source=Image.FromFile(path))
                {
                    if(source.Width!=width || source.Height!=height)throw new InvalidDataException("Nieprawidłowy obraz mapy.");
                    var image=new Bitmap(width,height,PixelFormat.Format32bppPArgb);levels.Add(image);
                    using(Graphics g=Graphics.FromImage(image)){g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(source,0,0);}
                }
                // A small, bounded mip pyramid prevents repeatedly filtering
                // millions of source pixels when the whole map is zoomed out.
                while(Math.Max(levels[levels.Count-1].Width,levels[levels.Count-1].Height)>512)
                {
                    Bitmap source=levels[levels.Count-1];var image=new Bitmap(Math.Max(1,source.Width/2),Math.Max(1,source.Height/2),PixelFormat.Format32bppPArgb);levels.Add(image);
                    using(Graphics g=Graphics.FromImage(image))
                    {g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(source,new Rectangle(Point.Empty,image.Size),0,0,source.Width,source.Height,GraphicsUnit.Pixel,edges);}
                }
            }
            catch{Dispose();throw;}
        }
        public void Draw(Graphics graphics,Size viewport,float scale,float x,float y,bool preview=false)
        {
            int level=0;
            while(level+1<levels.Count && scale<(float)levels[level].Width/Width*.70710678f)level++;
            Bitmap source=levels[level];float sx=(float)Width/source.Width*scale,sy=(float)Height/source.Height*scale;
            if(x>=viewport.Width || y>=viewport.Height || x+Width*scale<=0 || y+Height*scale<=0)return;
            // Use a quick sample during direct manipulation, then redraw the
            // settled view with antialiasing. Mips prefilter zoomed-out detail.
            graphics.InterpolationMode=preview?InterpolationMode.NearestNeighbor:InterpolationMode.HighQualityBilinear;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
            GraphicsState state=graphics.Save();
            try
            {
                // Keep a canonical texel grid across full redraws and exposed
                // strips. GDI+ source-rectangle cropping can shift its filter.
                graphics.TranslateTransform(x,y);graphics.ScaleTransform(sx,sy);
                var rectangle=new Rectangle(0,0,source.Width,source.Height);
                graphics.DrawImage(source,rectangle,rectangle,GraphicsUnit.Pixel);
            }
            finally{graphics.Restore(state);}
        }
        public void Dispose(){foreach(Bitmap image in levels)image.Dispose();levels.Clear();edges.Dispose();}
    }
}
