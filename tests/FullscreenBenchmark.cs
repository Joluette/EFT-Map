// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using EftPosition;

static class FullscreenBenchmark
{
    static readonly MethodInfo Down=typeof(MapCanvas).GetMethod("OnMouseDown",BindingFlags.NonPublic|BindingFlags.Instance);
    static readonly MethodInfo Move=typeof(MapCanvas).GetMethod("OnMouseMove",BindingFlags.NonPublic|BindingFlags.Instance);
    static readonly MethodInfo Up=typeof(MapCanvas).GetMethod("OnMouseUp",BindingFlags.NonPublic|BindingFlags.Instance);
    static void Mouse(MethodInfo method,MapCanvas map,int x,int y)
    {method.Invoke(map,new object[]{new MouseEventArgs(MouseButtons.Left,1,x,y,0)});}
    static IEnumerable<Control> All(Control root)
    {foreach(Control c in root.Controls){yield return c;foreach(var child in All(c))yield return child;}}
    [STAThread]static int Main(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string root=Path.GetFullPath(args[0]),label=args[1],assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets");
        Directory.CreateDirectory(root);
        var rows=new List<string>{"map,width,height,zoom,windowOpacity,frames,medianMs,p95Ms,maxMs"};
        var markers=MarkerCatalog.Load(Path.Combine(assets,"markers.json"));
        foreach(string id in new[]{"ground-zero","streets-of-tarkov"})
        foreach(int windowOpacity in new[]{100,75})
        {
            string cache=Path.Combine(root,label+"-"+id+"-"+windowOpacity);Directory.CreateDirectory(cache);
            new AppPreferences {MapOpacity=96,WindowOpacity=windowOpacity}.Save(Path.Combine(cache,"app-preferences.json"));
            using(var form=new MainForm(null,null,0,false,true,null,cache,false,true,id))
            {
                form.Show();var map=All(form).OfType<MapCanvas>().Single();
                var wait=Stopwatch.StartNew();while(map.Layer==null && wait.ElapsedMilliseconds<15000){Application.DoEvents();Thread.Sleep(5);}
                if(map.Layer==null || map.Definition.Id!=id)throw new Exception("Map did not load");
                var selection=new MarkerSelection();foreach(var q in markers.ForMap(id,"regular").Quests)selection.ForMap(id).Add(q.Id);
                map.SetMarkers(id,selection.Visible(markers,id));
                foreach(int zoom in new[]{1,6})
                {
                    map.FitMap();map.Zoom(zoom);map.Refresh();Mouse(Down,map,100,100);
                    var times=new List<double>();
                    for(int i=0;i<46;i++)
                    {
                        var clock=Stopwatch.StartNew();Mouse(Move,map,100+(i+1)*4,100+(i+1)*2);map.Refresh();clock.Stop();
                        if(i>=6)times.Add(clock.Elapsed.TotalMilliseconds);
                    }
                    Mouse(Up,map,284,192);times.Sort();
                    string row=String.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6:F3},{7:F3},{8:F3}",id,map.Width,map.Height,zoom,windowOpacity,times.Count,times[times.Count/2],times[(int)(times.Count*.95)],times[times.Count-1]);rows.Add(row);Console.WriteLine(row);
                }
                form.Close();Application.DoEvents();
            }
        }
        File.WriteAllLines(Path.Combine(root,label+"-fullscreen.csv"),rows);return 0;
    }
}
