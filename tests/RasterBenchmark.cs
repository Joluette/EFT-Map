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
using System.Windows.Forms;
using EftPosition;

static class RasterBenchmark
{
    static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static readonly MethodInfo Background=typeof(MapCanvas).GetMethod("OnPaintBackground",Private),Paint=typeof(MapCanvas).GetMethod("OnPaint",Private);
    static readonly MethodInfo Down=typeof(MapCanvas).GetMethod("OnMouseDown",Private),Move=typeof(MapCanvas).GetMethod("OnMouseMove",Private),Up=typeof(MapCanvas).GetMethod("OnMouseUp",Private);
    static void Mouse(MethodInfo method,MapCanvas map,int x,int y)
    {method.Invoke(map,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,y,0)});}
    static void Draw(MapCanvas map,Graphics graphics)
    {using(var e=new PaintEventArgs(graphics,map.ClientRectangle)){Background.Invoke(map,new object[]{e});Paint.Invoke(map,new object[]{e});}}
    [STAThread]static int Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string assets=Path.GetFullPath(args[0]),cache=Path.GetFullPath(args[1]),output=Path.GetFullPath(args[2]),label=args[3];Directory.CreateDirectory(output);
        var rows=new List<string>{"map,width,height,zoom,opacity,motion,frames,medianMs,p95Ms,maxMs"};
        foreach(string id in new[]{"the-lab","the-labyrinth","icebreaker"})
        foreach(Size size in id=="icebreaker"?new[]{new Size(1920,1080)}:new[]{new Size(960,600),new Size(1920,1080)})
        using(var form=new Form {ClientSize=size,FormBorderStyle=FormBorderStyle.None})
        using(var map=new MapCanvas {Dock=DockStyle.Fill,MapOnly=true})
        using(var bitmap=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb))
        using(var graphics=Graphics.FromImage(bitmap))
        {
            form.Controls.Add(map);form.CreateControl();map.CreateControl();
            var definition=MapCatalog.Load(assets).Single(m=>m.Id==id);var layer=definition.Layers[0];
            map.LoadMap(definition,layer,MapAssets.CachedPath(assets,cache,layer),cache);
            // Isolate resampling/compositing of the actual base PNG. Detail
            // arrivals/network timing must not change between the two builds.
            var field=typeof(MapCanvas).GetField("tiles",Private);((TileMap)field.GetValue(map)).Dispose();field.SetValue(map,null);
            foreach(int opacity in id=="icebreaker"?new[]{40}:new[]{100,40})
            foreach(int zoom in new[]{1,6})
            foreach(string motion in new[]{"pan","zoom"})
            {
                map.ApplyAppearance(new AppPreferences {MapOpacity=opacity,BackgroundMode="plain"},assets);map.FitMap();map.Zoom(zoom);Draw(map,graphics);Mouse(Down,map,100,100);
                var times=new List<double>();
                for(int i=0;i<28;i++)
                {
                    if(motion=="pan")Mouse(Move,map,100+(i+1)*4,100+(i+1)*2);else map.Zoom(1.005f);
                    var watch=Stopwatch.StartNew();Draw(map,graphics);watch.Stop();if(i>=4)times.Add(watch.Elapsed.TotalMilliseconds);
                }
                Mouse(Up,map,212,156);times.Sort();Draw(map,graphics);
                string row=String.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6},{7:F3},{8:F3},{9:F3}",id,size.Width,size.Height,zoom,opacity,motion,times.Count,times[times.Count/2],times[(int)(times.Count*.95)],times[times.Count-1]);rows.Add(row);Console.WriteLine(row);
                if(size.Width==1920 && opacity==40 && motion=="pan")bitmap.Save(Path.Combine(output,label+"-"+id+"-"+zoom+".png"));
            }
        }
        File.WriteAllLines(Path.Combine(output,label+".csv"),rows);return 0;
    }
}
