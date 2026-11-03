// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EftPosition;

static class ZoomWindowBenchmark
{
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [STAThread]static int Main(string[] args)
    {try{return Run(args);}catch(Exception error){Console.WriteLine(error);return 1;}}
    static int Run(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string assets=args.Length>4?Path.GetFullPath(args[4]):Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets"),source=args[0],output=args[1],label=args[2];Directory.CreateDirectory(output);
        var rows=new List<string>{"map,width,height,direction,opacity,frames,firstMs,medianMs,p95Ms,maxMs,settleMs"};
        var catalog=MapCatalog.Load(assets);bool all=args.Length>3;
        foreach(var definition in catalog.Where(d=>all || new[]{"ground-zero","streets-of-tarkov","the-lab"}.Contains(d.Id)))
        foreach(Size size in all?new[]{new Size(1920,1080)}:new[]{new Size(1250,650),new Size(1920,1080)})
        using(var form=new Form {ClientSize=size,FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Location=Point.Empty})
        using(var map=new MapCanvas {Dock=DockStyle.Fill,MapOnly=true})
        {
            form.Controls.Add(map);form.Show();var layer=definition.Layers[0];string path=String.IsNullOrEmpty(layer.VectorImage)?MapAssets.CachedPath(assets,source,layer):Path.Combine(assets,layer.VectorImage);
            bool remote=layer.Remote;layer.Remote=false;try{map.LoadMap(definition,layer,path);}finally{layer.Remote=remote;}
            map.ApplyAppearance(new AppPreferences {MapOpacity=100,BackgroundMode="tarkov",OverlayEnabled=false},assets);
            foreach(int opacity in all?new[]{100,65}:new[]{100})foreach(float factor in new[]{1.2f,1/1.2f})
            {
                map.ApplyAppearance(new AppPreferences {MapOpacity=opacity,BackgroundMode="tarkov"},assets);map.FitMap();map.Zoom(4);map.Refresh();
                map.SmoothZoom(factor);var times=new List<double>();double first=0;
                for(int i=0;i<22;i++)
                {
                    long started=Stopwatch.GetTimestamp()-(long)(Stopwatch.Frequency*.16*(i+.5)/22);
                    typeof(MapCanvas).GetField("zoomStarted",Private).SetValue(map,started);typeof(MapCanvas).GetMethod("AdvanceZoom",Private).Invoke(map,null);
                    var clock=Stopwatch.StartNew();map.Refresh();clock.Stop();if(i==0)first=clock.Elapsed.TotalMilliseconds;else times.Add(clock.Elapsed.TotalMilliseconds);
                }
                typeof(MapCanvas).GetField("zoomStarted",Private).SetValue(map,Stopwatch.GetTimestamp()-(long)(Stopwatch.Frequency*.2));typeof(MapCanvas).GetMethod("AdvanceZoom",Private).Invoke(map,null);
                var refine=typeof(MapCanvas).GetMethod("FinishZoomPreview",Private);if(refine!=null)refine.Invoke(map,null);
                var settle=Stopwatch.StartNew();map.Refresh();settle.Stop();times.Sort();
                string row=String.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6:F3},{7:F3},{8:F3},{9:F3},{10:F3}",definition.Id,map.Width,map.Height,factor>1?"in":"out",opacity,times.Count,first,times[times.Count/2],times[(int)(times.Count*.95)],times[times.Count-1],settle.Elapsed.TotalMilliseconds);
                rows.Add(row);Console.WriteLine(row);
            }
            form.Close();Application.DoEvents();
        }
        File.WriteAllLines(Path.Combine(output,label+".csv"),rows);return 0;
    }
}
