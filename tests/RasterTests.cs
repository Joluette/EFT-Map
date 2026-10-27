// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace EftPosition
{
    static class RasterTests
    {
        static int checks;
        static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        static void Same(Bitmap actual,Bitmap expected,string message,bool preview=false)
        {
            var rect=new Rectangle(Point.Empty,actual.Size);BitmapData a=actual.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb),b=expected.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            int different=0,maxError=0;
            try
            {
                var left=new int[actual.Width];var right=new int[actual.Width];
                for(int y=0;y<actual.Height;y++)
                {
                    Marshal.Copy(IntPtr.Add(a.Scan0,y*a.Stride),left,0,left.Length);Marshal.Copy(IntPtr.Add(b.Scan0,y*b.Stride),right,0,right.Length);
                    for(int x=0;x<left.Length;x++)
                    {
                        int error=0;for(int shift=0;shift<32;shift+=8)error=Math.Max(error,Math.Abs((int)(((uint)left[x]>>shift)&255)-(int)(((uint)right[x]>>shift)&255)));
                        maxError=Math.Max(error,maxError);if(error>2)different++;
                    }
                }
            }
            finally{actual.UnlockBits(a);expected.UnlockBits(b);}
            // GDI+'s nearest-neighbor span rounding can select an adjacent texel
            // at a handful of exact boundaries in transient frames. Settled,
            // antialiased views must match within two channel units everywhere.
            Check(different<=(preview?128:0),message+" (different="+different+", max="+maxError+")");
        }
        static void Fixture(string file,int width,int height)
        {
            using(var image=new Bitmap(width,height,PixelFormat.Format32bppPArgb))using(Graphics g=Graphics.FromImage(image))
            {
                g.Clear(Color.FromArgb(27,67,103));g.SmoothingMode=SmoothingMode.AntiAlias;
                using(var brush=new SolidBrush(Color.FromArgb(130,211,77,40)))g.FillEllipse(brush,31,27,width-60,height-50);
                using(var pen=new Pen(Color.White,1))for(int x=0;x<width;x+=17)g.DrawLine(pen,x,0,width-x,height);
                g.DrawString("Labs / Labyrinth 012345",SystemFonts.MessageBoxFont,Brushes.Yellow,55,120);
                g.CompositingMode=CompositingMode.SourceCopy;using(var hole=new SolidBrush(Color.Transparent))g.FillEllipse(hole,width-90,40,50,60);
                image.Save(file,ImageFormat.Png);
            }
        }
        static void Mouse(MapCanvas map,string method,int x,int y)
        {typeof(MapCanvas).GetMethod(method,Private).Invoke(map,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,y,0)});}
        static void Camera(MapCanvas map,float scale,float x,float y)
        {typeof(MapCanvas).GetField("scale",Private).SetValue(map,scale);typeof(MapCanvas).GetField("offsetX",Private).SetValue(map,x);typeof(MapCanvas).GetField("offsetY",Private).SetValue(map,y);}
        static Bitmap Render(MapCanvas map)
        {var image=new Bitmap(map.Width,map.Height);map.DrawToBitmap(image,map.ClientRectangle);return image;}
        static void SyntheticTiles(MapLayer layer,string root)
        {
            for(int z=2;z<=4;z++)for(int y=0;y<(1<<(z-1));y++)for(int x=0;x<(1<<(z-1));x++)
            {
                string file=TileMap.TileFile(layer,root,z,x,y);Directory.CreateDirectory(Path.GetDirectoryName(file));
                using(var image=new Bitmap(256,256))using(Graphics g=Graphics.FromImage(image)){g.Clear(Color.FromArgb(128,20,80,220));image.Save(file,ImageFormat.Png);}
            }
        }
        [STAThread]static int Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string root=Path.GetFullPath(args[0]),reports=Path.GetFullPath(args[1]);Check(!Directory.Exists(root),"fresh raster fixtures");Directory.CreateDirectory(root);Directory.CreateDirectory(reports);
                string path=Path.Combine(root,"odd-size-alpha.png");Fixture(path,1001,577);
                using(var image=new RasterImage(path,1001,577))
                {
                    Check(image.Bytes>1001L*577*4 && image.Bytes<=1001L*577*16/3,"mip memory remains below one third beyond original pixels");
                    using(var file=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(file.Length>0,"source PNG is not held open");
                    foreach(bool preview in new[]{false,true})foreach(int opacity in new[]{0,40,65,96,100})foreach(float scale in new[]{.25f,.64f,1.35f,4.1f,64f})
                    using(var moved=new MapViewport((g,size,s,x,y)=>image.Draw(g,size,s,x,y,preview)))
                    using(var fresh=new MapViewport((g,size,s,x,y)=>image.Draw(g,size,s,x,y,preview)))
                    using(var a=new Bitmap(480,320))using(var b=new Bitmap(480,320))using(Graphics ag=Graphics.FromImage(a))using(Graphics bg=Graphics.FromImage(b))
                    {
                        float x=-110.25f,y=-70.5f;moved.Draw(ag,a.Size,scale,x,y,opacity);
                        foreach(Point step in new[]{new Point(37,19),new Point(-61,0),new Point(0,-43),new Point(32,51),new Point(400,200),new Point(-470,-300)})
                        {
                            x+=step.X;y+=step.Y;ag.Clear(Color.Magenta);bg.Clear(Color.Magenta);fresh.Invalidate();
                            moved.Draw(ag,a.Size,scale,x,y,opacity);fresh.Draw(bg,b.Size,scale,x,y,opacity);Same(a,b,"raster pan equals full redraw: "+preview+" / "+opacity+" / "+scale,preview);
                        }
                        Check(moved.ReusedPanCount==6,"integer raster pans reuse pixels");int count=moved.RenderCount;moved.Draw(ag,a.Size,scale,x,y,opacity);Check(count==moved.RenderCount,"unchanged raster view avoids resampling");
                        ag.Clear(Color.Magenta);bg.Clear(Color.Magenta);int next=opacity==40?96:40;fresh.Invalidate();moved.Draw(ag,a.Size,scale,x,y,next);fresh.Draw(bg,b.Size,scale,x,y,next);Same(a,b,"opacity change does not compound fading",preview);
                        using(var smaller=new Bitmap(180,120))using(Graphics sg=Graphics.FromImage(smaller))moved.Draw(sg,smaller.Size,scale,x,y,next);
                        Check(moved.CacheBytes==180L*120*4,"resize discards both old raster viewports");moved.Dispose();Check(moved.CacheBytes==0,"raster viewports released on disposal");
                    }
                }
                var layer=new MapLayer {Id="base",Width=512,Height=512,Affine=new[]{1d,0d,0d,1d,0d,0d},Remote=true,TileSize=256,TileZoom=2,MaxTileZoom=4,TileTemplate="https://assets.tarkov.dev/maps/test-raster-fixture/{z}/{x}/{y}.png"};
                var definition=new MapDefinition {Id="fixture",Name="Raster fixture",Author="Test",Layers=new[]{layer}};
                string solid=Path.Combine(root,"solid.png"),cache=Path.Combine(root,"cache");
                using(var bitmap=new Bitmap(512,512))using(Graphics g=Graphics.FromImage(bitmap)){g.Clear(Color.FromArgb(200,40,20));bitmap.Save(solid);}
                SyntheticTiles(layer,cache);
                using(var form=new Form {ClientSize=new Size(640,420)})using(var map=new MapCanvas {Dock=DockStyle.Fill,MapOnly=true})
                {
                    form.Controls.Add(map);form.CreateControl();map.CreateControl();map.LoadMap(definition,layer,solid,cache);
                    map.ApplyAppearance(new AppPreferences {BackgroundMode="plain",MapOpacity=40},AppDomain.CurrentDomain.BaseDirectory);Camera(map,2,0,0);
                    var viewport=(MapViewport)typeof(MapCanvas).GetField("rasterViewport",Private).GetValue(map);
                    var tiles=(TileMap)typeof(MapCanvas).GetField("tiles",Private).GetValue(map);
                    Color initial;using(var first=Render(map))initial=first.GetPixel(320,210);int paints=viewport.RenderCount;
                    var wait=Stopwatch.StartNew();while(tiles.CacheCount<6 && wait.ElapsedMilliseconds<5000)Thread.Sleep(5);
                    Check(tiles.CacheCount>=6 && tiles.DownloadCount==0,"detail tiles load from offline fixtures");
                    map.RefreshTiles();using(var detail=Render(map)){Check(detail.GetPixel(320,210).B>initial.B+15,"tile arrival refreshes stationary raster image");detail.Save(Path.Combine(reports,"loaded-tiles.png"));}
                    Check(viewport.RenderCount>paints,"new tile pixels invalidate cached map");
                    map.ApplyAppearance(new AppPreferences {BackgroundMode="plain",MapOpacity=0},AppDomain.CurrentDomain.BaseDirectory);
                    Color background;using(var transparent=Render(map))background=transparent.GetPixel(320,210);
                    map.ApplyAppearance(new AppPreferences {BackgroundMode="plain",MapOpacity=40},AppDomain.CurrentDomain.BaseDirectory);
                    using(var composited=Render(map))
                    {
                        Color color=composited.GetPixel(320,210);
                        Check(Math.Abs(color.R-(110*.4+background.R*.6))<3 && Math.Abs(color.G-(60*.4+background.G*.6))<3 && Math.Abs(color.B-(120*.4+background.B*.6))<3,"opacity applies once to the complete base-plus-translucent-tile layer");
                    }
                    using(var stationary=Render(map)){}
                    paints=viewport.RenderCount;Mouse(map,"OnMouseDown",100,100);Mouse(map,"OnMouseMove",132,112);using(var moving=Render(map)){}
                    Check(viewport.RenderCount>paints,"drag starts quick rendering at unchanged zoom");paints=viewport.RenderCount;
                    Mouse(map,"OnMouseUp",132,112);using(var settled=Render(map)){settled.Save(Path.Combine(reports,"settled.png"));}
                    Check(viewport.RenderCount>paints,"mouse release restores final quality even with unchanged camera");
                    map.SetMarkers("fixture",new[]{new MapMarker {Name="Quest fixture",X=120,Z=100,Kind=MapMarkerKind.Quest}});Camera(map,.3f,20,30);
                    using(var markers=Render(map)){}
                    PointF point=map.MarkerScreenPoint(120,100);Check(Math.Abs(point.X-56)<.001 && Math.Abs(point.Y-60)<.001,"mip dimensions do not change world calibration");
                    Check(map.MarkerTooltipAt(Point.Round(point))=="Quest fixture","quest hit testing follows the same calibrated image");
                    var source=(RasterImage)typeof(MapCanvas).GetField("map",Private).GetValue(map);map.ClearMap();
                    Check(source.Bytes==0 && viewport.CacheBytes==0 && tiles.DecodedBytes==0,"switching maps releases pyramid, viewports and tile cache");
                }
                Console.WriteLine("PASS: "+checks+" raster checks: mip bounds, calibrated coordinates, alpha, pan reuse, full redraw parity, tile arrivals, quality restoration, resize and disposal.");return 0;
            }
            catch(Exception ex){Console.WriteLine(ex);return 1;}
        }
    }
}
