// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
namespace EftPosition
{
    static class ZoomPreviewTests
    {
        static int checks;
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
        static void Scene(Graphics g,Size size,float scale,float x,float y)
        {
            g.TranslateTransform(x,y);g.ScaleTransform(scale,scale);g.SmoothingMode=SmoothingMode.None;
            using(var b=new SolidBrush(Color.FromArgb(180,36,76,116)))g.FillRectangle(b,-10000,-10000,20000,20000);
            using(var b=new SolidBrush(Color.FromArgb(160,210,40,30)))g.FillRectangle(b,200,-10000,80,20000);
        }
        static void Samples(Bitmap actual,Bitmap expected,string label)
        {
            bool same=true;string detail="";foreach(Point p in new[]{new Point(4,4),new Point(635,4),new Point(4,395),new Point(635,395),new Point(111,83),new Point(530,311)})
            {Color a=actual.GetPixel(p.X,p.Y),b=expected.GetPixel(p.X,p.Y);bool equal=Math.Abs(a.R-b.R)<=2 && Math.Abs(a.G-b.G)<=2 && Math.Abs(a.B-b.B)<=2 && Math.Abs(a.A-b.A)<=2;same&=equal;if(!equal)detail=" at "+p+": "+a+" / "+b;}
            Check(same,label+detail);
        }
        static void Camera(MapCanvas map,float scale,float x,float y)
        {typeof(MapCanvas).GetField("scale",Private).SetValue(map,scale);typeof(MapCanvas).GetField("offsetX",Private).SetValue(map,x);typeof(MapCanvas).GetField("offsetY",Private).SetValue(map,y);}
        static float Field(MapCanvas map,string name){return (float)typeof(MapCanvas).GetField(name,Private).GetValue(map);}
        static Bitmap Render(MapCanvas map){var b=new Bitmap(map.Width,map.Height);map.DrawToBitmap(b,map.ClientRectangle);return b;}
        [STAThread]static int Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);UiText.Language="pl";
            try
            {
                string assets=Path.GetFullPath(args[0]),root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
                foreach(int opacity in new[]{0,40,100})foreach(float factor in new[]{.55f,1.2f})
                using(var preview=new MapViewport(Scene))using(var exact=new MapViewport(Scene))
                using(var a=new Bitmap(640,400,PixelFormat.Format32bppPArgb))using(var b=new Bitmap(640,400,PixelFormat.Format32bppPArgb))
                using(Graphics ag=Graphics.FromImage(a))using(Graphics bg=Graphics.FromImage(b))
                {
                    const float anchorX=230,anchorY=170,startX=-90,startY=-60;
                    float endX=anchorX-(anchorX-startX)*factor,endY=anchorY-(anchorY-startY)*factor;
                    for(int i=0;i<=20;i++)
                    {
                        float scale=1+(factor-1)*i/20f,x=anchorX-(anchorX-startX)*scale,y=anchorY-(anchorY-startY)*scale;
                        ag.Clear(Color.Magenta);bg.Clear(Color.Magenta);
                        preview.DrawZoom(ag,a.Size,scale,x,y,opacity,factor,endX,endY);exact.Draw(bg,b.Size,scale,x,y,opacity);
                        Samples(a,b,"zoom covers newly revealed edges, color and opacity: "+factor+" / "+opacity+" / "+i);
                    }
                    Check(preview.ZoomRenderCount==1,"one scene render serves every intermediate zoom frame");
                    Check(preview.ZoomCacheBytes<=1280L*720*4,"zoom texture respects its pixel budget");
                    ag.Clear(Color.Magenta);preview.Draw(ag,a.Size,factor,endX,endY,opacity);Samples(a,b,"settling restores exact rendering");
                    Check(preview.ZoomCacheBytes==0,"settling releases the temporary zoom texture");
                    preview.DrawZoom(ag,new Size(3840,2160),1,0,0,opacity,.8f,100,100);Check(preview.ZoomCacheBytes<=1280L*720*4,"4K previews keep the same bounded texture memory");
                    int renders=preview.ZoomRenderCount;preview.Invalidate();preview.DrawZoom(ag,a.Size,1,0,0,opacity,.8f,100,100);Check(preview.ZoomRenderCount==renders+1,"content invalidation refreshes an active preview");
                    renders=preview.ZoomRenderCount;preview.DrawZoom(ag,a.Size,1,0,0,opacity==40?100:40,.8f,100,100);Check(preview.ZoomRenderCount==renders+1,"changing opacity rebuilds instead of compounding cached opacity");
                    renders=preview.ZoomRenderCount;preview.DrawZoom(ag,a.Size,1,0,0,opacity,.25f,200,120);Check(preview.ZoomRenderCount==renders+1,"extending zoom-out rebuilds coverage before showing its edges");
                    preview.Clear();Check(preview.CacheBytes==0,"clearing a map releases its zoom texture");
                }
                string png=Path.Combine(root,"fixture.png");using(var image=new Bitmap(512,512))using(Graphics g=Graphics.FromImage(image)){g.Clear(Color.Navy);g.FillEllipse(Brushes.Coral,70,80,240,270);image.Save(png);}
                foreach(bool raster in new[]{false,true})
                using(var form=new Form {ClientSize=new Size(1250,650),FormBorderStyle=FormBorderStyle.None})
                using(var map=new MapCanvas {Dock=DockStyle.Fill,MapOnly=true})
                {
                    var definition=raster?new MapDefinition {Id="fixture",Name="Fixture",Author="Test",Layers=new[]{new MapLayer {Id="base",Width=512,Height=512,Affine=new[]{1d,0d,0d,1d,0d,0d}}}}:Array.Find(MapCatalog.Load(assets),m=>m.Id=="ground-zero");
                    var layer=definition.Layers[0];form.Controls.Add(map);form.Show();map.LoadMap(definition,layer,raster?png:Path.Combine(assets,layer.VectorImage));
                    map.ApplyAppearance(new AppPreferences {Language="pl",MapOpacity=65,BackgroundMode="plain"},assets);map.FitMap();using(var initial=Render(map)){}
                    const int ax=310,ay=270;float start=Field(map,"scale"),wx=(ax-Field(map,"offsetX"))/start,wy=(ay-Field(map,"offsetY"))/start;
                    typeof(MapCanvas).GetMethod("OnMouseWheel",Private).Invoke(map,new object[]{new MouseEventArgs(MouseButtons.None,0,ax,ay,-120)});
                    ((System.Windows.Forms.Timer)typeof(MapCanvas).GetField("zoomTimer",Private).GetValue(map)).Stop();
                    int before=map.ZoomPreviewRenderCount;
                    foreach(double time in new[]{.02,.06,.1,.14})
                    {
                        typeof(MapCanvas).GetField("zoomStarted",Private).SetValue(map,Stopwatch.GetTimestamp()-(long)(Stopwatch.Frequency*time));typeof(MapCanvas).GetMethod("AdvanceZoom",Private).Invoke(map,null);
                        using(var moving=Render(map)){if(time==.1)moving.Save(Path.Combine(root,raster?"raster-moving.png":"vector-moving.png"));}
                        float scale=Field(map,"scale");Check(Math.Abs((ax-Field(map,"offsetX"))/scale-wx)<.002 && Math.Abs((ay-Field(map,"offsetY"))/scale-wy)<.002,"cached zoom keeps the cursor's world point: "+raster);
                    }
                    Check(map.ZoomPreviewRenderCount==before+1,"MapCanvas shares one preview across animation frames: "+raster);
                    typeof(MapCanvas).GetField("zoomStarted",Private).SetValue(map,Stopwatch.GetTimestamp()-(long)(Stopwatch.Frequency*.2));typeof(MapCanvas).GetMethod("AdvanceZoom",Private).Invoke(map,null);
                    using(var resting=Render(map)){}
                    Check(!map.IsAnimating && map.IsZoomSettling && map.ZoomPreviewBytes>0,"full-quality refinement waits briefly for further wheel input: "+raster);
                    map.SmoothZoom(1.2f);Check(!map.IsZoomSettling,"further wheel input cancels the pending refinement: "+raster);
                    typeof(MapCanvas).GetField("zoomStarted",Private).SetValue(map,Stopwatch.GetTimestamp()-(long)(Stopwatch.Frequency*.2));typeof(MapCanvas).GetMethod("AdvanceZoom",Private).Invoke(map,null);
                    var wait=Stopwatch.StartNew();while(map.IsZoomSettling && wait.ElapsedMilliseconds<2000){Application.DoEvents();System.Threading.Thread.Sleep(2);}
                    using(var settled=Render(map))settled.Save(Path.Combine(root,raster?"raster-settled.png":"vector-settled.png"));
                    Check(!map.IsAnimating && !map.IsZoomSettling && map.ZoomPreviewBytes==0,"MapCanvas returns to full quality after the wheel is idle: "+raster);
                    map.SmoothZoom(1.2f);using(var next=Render(map)){}map.ClearMap();Check(map.ZoomPreviewBytes==0,"map replacement disposes active previews: "+raster);
                    form.Close();
                }
                Console.WriteLine("PASS: "+checks+" zoom-preview checks: coverage, opacity, cache reuse, cursor anchor, final quality, invalidation and bounded memory.");return 0;
            }
            catch(Exception error){Console.WriteLine(error);return 1;}
        }
    }
}
