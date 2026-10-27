// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace EftPosition
{
    public sealed class MapBackdrop : IDisposable
    {
        Bitmap image,cache;
        string path="",mode="plain";
        int dim;
        long revision=-1;
        public string Error {get;private set;}
        public long CacheBytes {get{return cache==null?0:(long)cache.Width*cache.Height*4;}}
        public void Apply(AppPreferences preferences,string assets)
        {
            string next=preferences.BackgroundMode=="custom"?preferences.BackgroundFile:preferences.BackgroundMode=="tarkov"?Path.Combine(assets,"tarkov-background.jpg"):preferences.BackgroundMode=="cyberpunk"?Path.Combine(assets,"tarkov-cyberpunk-background.jpg"):"";
            next=next??"";mode=preferences.BackgroundMode;dim=preferences.BackgroundDim;
            if(next!=path || revision!=preferences.BackgroundRevision)
            {
                if(image!=null){image.Dispose();image=null;}path=next;revision=preferences.BackgroundRevision;Error=null;
                if(next.Length>0)try{image=ReadImage(next);}
                catch(Exception ex){if(!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException) && !(ex is OutOfMemoryException))throw;Error=UiText.T("Nie można otworzyć tła. Wybierz obraz ponownie.");}
            }
            ClearCache();
        }
        public static Bitmap ReadImage(string file)
        {
            var info=new FileInfo(file);if(!info.Exists || info.Length>12*1024*1024)throw new InvalidDataException(UiText.T("Wybierz obraz JPG lub PNG do 12 MB."));
            using(FileStream stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(Image source=Image.FromStream(stream,false,false))
            {
                if(source.Width<1 || source.Height<1 || source.Width>8192 || source.Height>8192 || (long)source.Width*source.Height>20000000)throw new InvalidDataException(UiText.T("Wybierz obraz do 20 megapikseli."));
                double factor=Math.Min(1,Math.Min(1920.0/source.Width,1080.0/source.Height));
                Bitmap result=new Bitmap(Math.Max(1,(int)(source.Width*factor)),Math.Max(1,(int)(source.Height*factor)),PixelFormat.Format32bppPArgb);
                try{using(Graphics g=Graphics.FromImage(result)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(source,new Rectangle(Point.Empty,result.Size));}return result;}
                catch{result.Dispose();throw;}
            }
        }
        public void ClearCache(){if(cache!=null){cache.Dispose();cache=null;}}
        public void Draw(Graphics graphics,Size size)
        {
            if(size.Width<1 || size.Height<1)return;
            if(cache==null || cache.Size!=size)
            {
                ClearCache();cache=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb);
                using(Graphics g=Graphics.FromImage(cache))
                {
                    using(var gradient=new LinearGradientBrush(new Rectangle(Point.Empty,size),UiTheme.Surface,UiTheme.Background,80))g.FillRectangle(gradient,new Rectangle(Point.Empty,size));
                    if(image!=null && mode!="plain")
                    {
                        float scale=Math.Max((float)size.Width/image.Width,(float)size.Height/image.Height);
                        float width=image.Width*scale,height=image.Height*scale;
                        g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(image,new RectangleF((size.Width-width)/2,(size.Height-height)/2,width,height));
                        using(var shade=new SolidBrush(Color.FromArgb(dim*255/100,9,13,16)))g.FillRectangle(shade,new Rectangle(Point.Empty,size));
                    }
                }
            }
            graphics.DrawImageUnscaled(cache,0,0);
        }
        public void Dispose(){ClearCache();if(image!=null){image.Dispose();image=null;}}
    }
}
