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
static class ZoomCadenceBenchmark
{
    static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(Control child in All(c))yield return child;}}
    static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(1);}}
    [STAThread]static int Main(string[] args){try{return Run(args);}catch(Exception error){Console.WriteLine(error);return 1;}}
    static int Run(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string root=Path.GetFullPath(args[0]),label=args[1],assets=Path.GetFullPath(args[2]),cache=Path.Combine(root,label+"-cadence-cache");Directory.CreateDirectory(root);
        new AppPreferences {WindowOpacity=100,MapOpacity=100,OverlayEnabled=false,AutoMapEnabled=false,DeleteScreenshots=false,BackgroundMode="tarkov"}.Save(Path.Combine(cache,"app-preferences.json"));
        var rows=new List<string>{"map,width,height,animations,frames,medianGapMs,p95GapMs,maxGapMs"};
        using(var form=new MainForm(null,null,0,false,true,null,cache,false,true,"ground-zero"))
        {
            form.Show();MapCanvas map=All(form).OfType<MapCanvas>().Single();var wait=Stopwatch.StartNew();while(map.Layer==null && wait.ElapsedMilliseconds<15000)Pump(5);if(map.Layer==null)throw new Exception("Initial map failed");Pump(100);
            foreach(string id in new[]{"ground-zero","streets-of-tarkov","the-lab"})
            {
                var definition=MapCatalog.Load(assets).Single(d=>d.Id==id);var layer=definition.Layers[0];string path=String.IsNullOrEmpty(layer.VectorImage)?MapAssets.CachedPath(assets,args[3],layer):Path.Combine(assets,layer.VectorImage);layer.Remote=false;map.LoadMap(definition,layer,path);map.FitMap();map.Zoom(4);map.Refresh();
                var gaps=new List<double>();int frames=0;long prior=0;
                PaintEventHandler painted=delegate{if(!map.IsAnimating)return;long now=Stopwatch.GetTimestamp();if(prior!=0)gaps.Add((now-prior)*1000.0/Stopwatch.Frequency);prior=now;frames++;};map.Paint+=painted;
                for(int i=0;i<6;i++){prior=0;map.SmoothZoom(i%2==0?1.2f:1/1.2f);wait.Restart();while(map.IsAnimating && wait.ElapsedMilliseconds<2000)Pump(2);if(map.IsAnimating)throw new Exception("Zoom stalled");Pump(200);}
                map.Paint-=painted;gaps.Sort();if(gaps.Count==0)throw new Exception("No animation frames");
                string row=String.Format(CultureInfo.InvariantCulture,"{0},{1},{2},6,{3},{4:F3},{5:F3},{6:F3}",id,map.Width,map.Height,frames,gaps[gaps.Count/2],gaps[(int)(gaps.Count*.95)],gaps[gaps.Count-1]);rows.Add(row);Console.WriteLine(row);
            }
            form.Close();
        }
        File.WriteAllLines(Path.Combine(root,label+"-cadence.csv"),rows);return 0;
    }
}
