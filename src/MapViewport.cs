// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace EftPosition
{
    // Shared by vector and raster maps. Integer pans repaint exposed strips;
    // animated zoom reuses a bounded preview, then restores exact geometry.
    sealed class MapViewport : IDisposable
    {
        readonly Action<Graphics,Size,float,float,float> paint;
        Bitmap viewportCache,panCache;
        Bitmap zoomCache;
        RectangleF zoomWorld;
        float zoomScale,zoomReferenceScale;
        int zoomOpacity;
        const int ZoomPixelLimit=1280*720;
        float cachedScale,cachedX,cachedY;
        bool cacheValid;
        int cachedOpacity=100;
        int[] opacityRow;
        public int RenderCount {get;private set;}
        public int ReusedPanCount {get;private set;}
        public int ZoomRenderCount {get;private set;}
        public long ZoomCacheBytes {get{return Bytes(zoomCache);}}
        public long CacheBytes {get{return Bytes(viewportCache)+Bytes(panCache)+Bytes(zoomCache);}}
        static long Bytes(Bitmap image){return image==null?0:(long)image.Width*image.Height*4;}
        public MapViewport(Action<Graphics,Size,float,float,float> draw){paint=draw;}
        public void Invalidate(){cacheValid=false;ClearZoom();}
        static RectangleF WorldView(Size viewport,float scale,float x,float y)
        {return new RectangleF(-x/scale,-y/scale,viewport.Width/scale,viewport.Height/scale);}
        public void DrawZoom(Graphics graphics,Size viewport,float scale,float x,float y,int opacity,float targetScale,float targetX,float targetY)
        {
            if(viewport.Width<=0 || viewport.Height<=0)return;
            if(scale<=0 || targetScale<=0 || Single.IsNaN(scale) || Single.IsNaN(targetScale) || Single.IsInfinity(scale) || Single.IsInfinity(targetScale))throw new ArgumentOutOfRangeException("scale");
            if(opacity<0 || opacity>100)throw new ArgumentOutOfRangeException("opacity");
            RectangleF wanted=RectangleF.Union(WorldView(viewport,scale,x,y),WorldView(viewport,targetScale,targetX,targetY));
            if(zoomCache==null || zoomOpacity!=opacity || !zoomWorld.Contains(wanted) || scale>zoomReferenceScale*1.8f)
            {
                ClearZoom();
                // Cover both ends of the camera motion, including newly revealed edges on zoom-out.
                wanted.Inflate(wanted.Width*.04f,wanted.Height*.04f);
                zoomScale=(float)Math.Min(Math.Min(viewport.Width/wanted.Width,viewport.Height/wanted.Height),Math.Sqrt(ZoomPixelLimit/(double)(wanted.Width*wanted.Height)));
                int width=Math.Max(1,(int)Math.Floor(wanted.Width*zoomScale)),height=Math.Max(1,(int)Math.Floor(wanted.Height*zoomScale));
                zoomWorld=new RectangleF(wanted.Left,wanted.Top,width/zoomScale,height/zoomScale);
                zoomCache=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
                try
                {
                    using(Graphics target=Graphics.FromImage(zoomCache))
                    {target.Clear(Color.Transparent);paint(target,zoomCache.Size,zoomScale,-zoomWorld.Left*zoomScale,-zoomWorld.Top*zoomScale);}
                    ApplyOpacity(zoomCache,new Rectangle(Point.Empty,zoomCache.Size),opacity);
                    zoomOpacity=opacity;zoomReferenceScale=scale;ZoomRenderCount++;
                }
                catch{ClearZoom();throw;}
            }
            GraphicsState state=graphics.Save();
            try
            {
                float left=x+zoomWorld.Left*scale,top=y+zoomWorld.Top*scale;
                graphics.InterpolationMode=InterpolationMode.NearestNeighbor;graphics.PixelOffsetMode=PixelOffsetMode.Half;
                graphics.TranslateTransform(left,top);graphics.ScaleTransform(scale/zoomScale,scale/zoomScale);
                var bounds=new Rectangle(0,0,zoomCache.Width,zoomCache.Height);graphics.DrawImage(zoomCache,bounds,bounds,GraphicsUnit.Pixel);
            }
            finally{graphics.Restore(state);}
        }
        void ClearZoom(){if(zoomCache!=null){zoomCache.Dispose();zoomCache=null;}}
        public void Draw(Graphics graphics,Size viewport,float scale,float offsetX,float offsetY,int opacity)
        {
            ClearZoom();
            if(viewport.Width<=0 || viewport.Height<=0)return;
            if(Single.IsNaN(scale) || Single.IsInfinity(scale) || scale<=0)throw new ArgumentOutOfRangeException("scale");
            if(opacity<0 || opacity>100)throw new ArgumentOutOfRangeException("opacity");
            if(viewportCache==null || viewportCache.Size!=viewport)
            {Clear();viewportCache=new Bitmap(viewport.Width,viewport.Height,PixelFormat.Format32bppPArgb);}
            if(!cacheValid || scale!=cachedScale || offsetX!=cachedX || offsetY!=cachedY || opacity!=cachedOpacity)
            {
                float moveX=offsetX-cachedX,moveY=offsetY-cachedY;
                int dx=(int)Math.Round(moveX),dy=(int)Math.Round(moveY);
                bool reuse=cacheValid && opacity==cachedOpacity && scale==cachedScale && Math.Abs(moveX-dx)<.001f && Math.Abs(moveY-dy)<.001f && Math.Abs(dx)<viewport.Width && Math.Abs(dy)<viewport.Height;
                if(reuse)
                {
                    if(panCache==null)panCache=new Bitmap(viewport.Width,viewport.Height,PixelFormat.Format32bppPArgb);
                    using(Graphics target=Graphics.FromImage(panCache))target.Clear(Color.Transparent);
                    CopyPan(viewportCache,panCache,dx,dy);
                    using(Graphics target=Graphics.FromImage(panCache))
                    using(Region exposed=new Region(new Rectangle(Point.Empty,viewport)))
                    {
                        exposed.Exclude(new Rectangle(dx,dy,viewport.Width,viewport.Height));target.SetClip(exposed,CombineMode.Replace);
                        paint(target,viewport,scale,offsetX,offsetY);
                    }
                    if(dy!=0)ApplyOpacity(panCache,new Rectangle(0,dy>0?0:viewport.Height+dy,viewport.Width,Math.Abs(dy)),opacity);
                    if(dx!=0)ApplyOpacity(panCache,new Rectangle(dx>0?0:viewport.Width+dx,Math.Max(0,dy),Math.Abs(dx),viewport.Height-Math.Abs(dy)),opacity);
                    Bitmap old=viewportCache;viewportCache=panCache;panCache=old;ReusedPanCount++;
                }
                else
                {
                    using(Graphics target=Graphics.FromImage(viewportCache))
                    {target.Clear(Color.Transparent);paint(target,viewport,scale,offsetX,offsetY);}
                    ApplyOpacity(viewportCache,new Rectangle(Point.Empty,viewport),opacity);
                }
                cachedScale=scale;cachedX=offsetX;cachedY=offsetY;cachedOpacity=opacity;cacheValid=true;RenderCount++;
            }
            graphics.DrawImageUnscaled(viewportCache,0,0);
        }
        void ApplyOpacity(Bitmap image,Rectangle bounds,int opacity)
        {
            if(opacity==100 || bounds.Width<=0 || bounds.Height<=0)return;
            if(opacityRow==null || opacityRow.Length<bounds.Width)opacityRow=new int[image.Width];
            BitmapData pixels=image.LockBits(bounds,ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
            // 16.16 scale avoids four integer divisions for every screen pixel.
            uint factor=(uint)((opacity*65536+50)/100);
            try
            {
                for(int y=0;y<bounds.Height;y++)
                {
                    IntPtr row=IntPtr.Add(pixels.Scan0,y*pixels.Stride);Marshal.Copy(row,opacityRow,0,bounds.Width);
                    for(int x=0;x<bounds.Width;x++)
                    {
                        uint p=(uint)opacityRow[x];
                        uint b=((p&255)*factor+32768)>>16,g=(((p>>8)&255)*factor+32768)>>16;
                        uint r=(((p>>16)&255)*factor+32768)>>16,a=((p>>24)*factor+32768)>>16;
                        opacityRow[x]=(int)(b|(g<<8)|(r<<16)|(a<<24));
                    }
                    Marshal.Copy(opacityRow,0,row,bounds.Width);
                }
            }
            finally{image.UnlockBits(pixels);}
        }
        void CopyPan(Bitmap source,Bitmap target,int dx,int dy)
        {
            int width=source.Width-Math.Abs(dx),height=source.Height-Math.Abs(dy);
            if(opacityRow==null || opacityRow.Length<width)opacityRow=new int[source.Width];
            BitmapData from=source.LockBits(new Rectangle(Math.Max(0,-dx),Math.Max(0,-dy),width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
            try
            {
                BitmapData to=target.LockBits(new Rectangle(Math.Max(0,dx),Math.Max(0,dy),width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
                try
                {
                    // Preserve premultiplied channel values bit-for-bit, without
                    // resampling or compositing the already faded viewport.
                    for(int y=0;y<height;y++)
                    {Marshal.Copy(IntPtr.Add(from.Scan0,y*from.Stride),opacityRow,0,width);Marshal.Copy(opacityRow,0,IntPtr.Add(to.Scan0,y*to.Stride),width);}
                }
                finally{target.UnlockBits(to);}
            }
            finally{source.UnlockBits(from);}
        }
        public void Clear()
        {cacheValid=false;opacityRow=null;ClearZoom();if(viewportCache!=null){viewportCache.Dispose();viewportCache=null;}if(panCache!=null){panCache.Dispose();panCache=null;}}
        public void Dispose(){Clear();}

    }
}
