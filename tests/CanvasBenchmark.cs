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

static class CanvasBenchmark
{
    static readonly MethodInfo Background=typeof(MapCanvas).GetMethod("OnPaintBackground",BindingFlags.NonPublic|BindingFlags.Instance);
    static readonly MethodInfo Paint=typeof(MapCanvas).GetMethod("OnPaint",BindingFlags.NonPublic|BindingFlags.Instance);
    static readonly MethodInfo Down=typeof(MapCanvas).GetMethod("OnMouseDown",BindingFlags.NonPublic|BindingFlags.Instance);
    static readonly MethodInfo Move=typeof(MapCanvas).GetMethod("OnMouseMove",BindingFlags.NonPublic|BindingFlags.Instance);
    static readonly MethodInfo Up=typeof(MapCanvas).GetMethod("OnMouseUp",BindingFlags.NonPublic|BindingFlags.Instance);
    static void Mouse(MethodInfo method,MapCanvas map,int x,int y)
    {method.Invoke(map,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,y,0)});}
    static void Draw(MapCanvas map,Graphics graphics)
    {using(var args=new PaintEventArgs(graphics,map.ClientRectangle)){Background.Invoke(map,new object[]{args});Paint.Invoke(map,new object[]{args});}}
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string assets=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]),label=args[2];
        Directory.CreateDirectory(output);
        var rows=new List<string>{"map,width,height,zoom,opacity,frames,medianMs,p95Ms,maxMs"};
        MarkerCatalog markers=MarkerCatalog.Load(Path.Combine(assets,"markers.json"));
        foreach(string id in new[]{"ground-zero","streets-of-tarkov"})
        foreach(Size size in args.Length>3?new[]{new Size(1920,1080)}:new[]{new Size(960,600),new Size(1920,1080)})
        using(var form=new Form {ClientSize=size,FormBorderStyle=FormBorderStyle.None})
        using(var map=new MapCanvas {Dock=DockStyle.Fill,MapOnly=true})
        using(var bitmap=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb))
        using(var graphics=Graphics.FromImage(bitmap))
        {
            form.Controls.Add(map);form.CreateControl();map.CreateControl();
            var definition=MapCatalog.Load(assets).Single(m=>m.Id==id);var layer=definition.Layers[0];
            map.LoadMap(definition,layer,Path.Combine(assets,layer.VectorImage));
            var selection=new MarkerSelection();foreach(var q in markers.ForMap(id,"regular").Quests)selection.ForMap(id).Add(q.Id);
            map.SetMarkers(id,selection.Visible(markers,id));
            foreach(int opacity in args.Length>3?new[]{96}:new[]{100,96,65})
            foreach(int zoom in new[]{1,6})
            {
                var preferences=new AppPreferences {MapOpacity=opacity};map.ApplyAppearance(preferences,assets);map.FitMap();map.Zoom(zoom);
                Draw(map,graphics);Mouse(Down,map,100,100);
                var times=new List<double>();
                for(int i=0;i<36;i++)
                {
                    if(args.Length>3)map.Zoom(1.005f);else Mouse(Move,map,100+(i+1)*4,100+(i+1)*2);
                    var clock=Stopwatch.StartNew();Draw(map,graphics);clock.Stop();
                    if(i>=6)times.Add(clock.Elapsed.TotalMilliseconds);
                }
                Mouse(Up,map,244,172);
                times.Sort();
                string row=String.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6:F3},{7:F3},{8:F3}",id,size.Width,size.Height,zoom,opacity,times.Count,times[times.Count/2],times[(int)(times.Count*.95)],times[times.Count-1]);
                rows.Add(row);Console.WriteLine(row);
                bitmap.Save(Path.Combine(output,label+"-"+id+"-"+size.Width+"-"+zoom+"-"+opacity+".png"));
            }
        }
        File.WriteAllLines(Path.Combine(output,label+".csv"),rows);return 0;
    }
}
