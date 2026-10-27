// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using EftPosition;

static class RasterWindowBenchmark
{
    static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(var child in All(c))yield return child;}}
    static float Field(MapCanvas map,string name){return (float)typeof(MapCanvas).GetField(name,Private).GetValue(map);}
    static void Mouse(MapCanvas map,string method,int x,int y)
    {typeof(MapCanvas).GetMethod(method,Private).Invoke(map,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,y,0)});}
    static Rectangle Tiles(MapLayer l,int z,float scale,float x,float y,Size size,int margin)
    {
        double ratio=Math.Pow(2,z-l.TileZoom),left=Math.Max(0,(-margin-x)/scale),top=Math.Max(0,(-margin-y)/scale);
        double right=Math.Min(l.Width,(size.Width+margin-x)/scale),bottom=Math.Min(l.Height,(size.Height+margin-y)/scale);
        int a=(int)Math.Floor((l.TileLeft+left)*ratio/l.TileSize),b=(int)Math.Floor((l.TileTop+top)*ratio/l.TileSize);
        int c=(int)Math.Floor(((l.TileLeft+right)*ratio-.001)/l.TileSize),d=(int)Math.Floor(((l.TileTop+bottom)*ratio-.001)/l.TileSize);
        return Rectangle.FromLTRB(a,b,c+1,d+1);
    }
    static void Prepare(MapLayer layer,string cache,Size size,float scale,float x,float y)
    {
        using(var image=new Bitmap(256,256,PixelFormat.Format32bppPArgb))using(Graphics g=Graphics.FromImage(image))
        {
            g.Clear(Color.FromArgb(190,35,81,108));using(var pen=new Pen(Color.FromArgb(220,210,210,190),2))for(int i=0;i<256;i+=16){g.DrawLine(pen,i,0,i,255);g.DrawLine(pen,0,i,255,i);}g.DrawString("Offline map detail",SystemFonts.MessageBoxFont,Brushes.White,10,50);
            for(int z=layer.TileZoom;z<=layer.MaxTileZoom;z++)
            {
                Rectangle area=Tiles(layer,z,scale,x,y,size,180);
                for(int ty=area.Top;ty<area.Bottom;ty++)for(int tx=area.Left;tx<area.Right;tx++)
                {string file=TileMap.TileFile(layer,cache,z,tx,ty);Directory.CreateDirectory(Path.GetDirectoryName(file));if(!File.Exists(file))image.Save(file,ImageFormat.Png);}
            }
        }
    }
    static void Pump(){Application.DoEvents();Thread.Sleep(2);}
    [STAThread]static int Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string source=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]),label=args[2],assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets");Directory.CreateDirectory(output);
        var rows=new List<string>{"map,width,height,zoom,motion,tiles,frames,medianMs,p95Ms,maxMs,settleMs"};
        foreach(string id in new[]{"the-lab","the-labyrinth","icebreaker"})
        {
            string cache=Path.Combine(output,"fixtures",label+"-"+id);Directory.CreateDirectory(Path.Combine(cache,"maps"));
            var definition=MapCatalog.Load(assets).Single(m=>m.Id==id);var layer=definition.Layers[0];
            File.Copy(MapAssets.CachedPath(assets,source,layer),MapAssets.CachedPath(assets,cache,layer),true);
            new AppPreferences {MapOpacity=40,Theme="night",BackgroundMode="plain"}.Save(Path.Combine(cache,"app-preferences.json"));
            Size size=Screen.PrimaryScreen.Bounds.Size;float fit=Math.Min((size.Width-48f)/layer.Width,(size.Height-70f)/layer.Height),detailScale=fit*6;
            Prepare(layer,cache,size,detailScale,(size.Width-layer.Width*detailScale)/2,(size.Height-layer.Height*detailScale)/2);
            using(var form=new MainForm(null,null,0,false,true,null,cache,false,true,id))
            {
                form.Show();MapCanvas map=All(form).OfType<MapCanvas>().Single();var wait=Stopwatch.StartNew();while(map.Layer==null && wait.ElapsedMilliseconds<15000)Pump();if(map.Layer==null)throw new Exception("Map failed to load");
                foreach(int zoom in new[]{1,6})foreach(string motion in new[]{"pan","animated-zoom"})
                {
                    map.FitMap();map.Zoom(zoom);map.Refresh();var tiles=(TileMap)typeof(MapCanvas).GetField("tiles",Private).GetValue(map);
                    if(zoom==6)
                    {
                        int z=TileMap.ChooseZoom(layer,Field(map,"scale"));Rectangle wanted=Tiles(layer,z,Field(map,"scale"),Field(map,"offsetX"),Field(map,"offsetY"),map.ClientSize,0);
                        while(wanted.Width*wanted.Height>80 && z>layer.TileZoom){z--;wanted=Tiles(layer,z,Field(map,"scale"),Field(map,"offsetX"),Field(map,"offsetY"),map.ClientSize,0);}
                        wait.Restart();while(tiles.CacheCount<wanted.Width*wanted.Height && wait.ElapsedMilliseconds<10000){Pump();map.RefreshTiles();map.Refresh();}
                        if(tiles.CacheCount<wanted.Width*wanted.Height)throw new Exception("Offline tile warmup failed");
                    }
                    map.RefreshTiles();map.Refresh();var times=new List<double>();
                    if(motion=="pan")Mouse(map,"OnMouseDown",100,100);else map.SmoothZoom(1.2f);
                    for(int i=0;i<36;i++)
                    {
                        if(motion=="pan")Mouse(map,"OnMouseMove",100+(i+1)*4,100+(i+1)*2);
                        else
                        {
                            // Replay the same camera timeline independent of
                            // how long an old frame takes to paint.
                            long start=Stopwatch.GetTimestamp()-(long)(Stopwatch.Frequency*.16*(i+1)/36);
                            typeof(MapCanvas).GetField("zoomStarted",Private).SetValue(map,start);typeof(MapCanvas).GetMethod("AdvanceZoom",Private).Invoke(map,null);
                        }
                        var clock=Stopwatch.StartNew();map.Refresh();clock.Stop();if(i>=4)times.Add(clock.Elapsed.TotalMilliseconds);
                    }
                    var settle=Stopwatch.StartNew();if(motion=="pan")Mouse(map,"OnMouseUp",244,172);map.Refresh();settle.Stop();times.Sort();
                    if(tiles.DownloadCount!=0)throw new Exception("Benchmark must remain offline");
                    string row=String.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6},{7:F3},{8:F3},{9:F3},{10:F3}",id,map.Width,map.Height,zoom,motion,tiles.CacheCount,times.Count,times[times.Count/2],times[(int)(times.Count*.95)],times[times.Count-1],settle.Elapsed.TotalMilliseconds);rows.Add(row);Console.WriteLine(row);
                }
                form.Close();Application.DoEvents();
            }
        }
        File.WriteAllLines(Path.Combine(output,label+"-window.csv"),rows);return 0;
    }
}
