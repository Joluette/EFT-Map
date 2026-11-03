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
static class LootBenchmark
{
    static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    [STAThread]static int Main(string[] args){try{return Run(args);}catch(Exception error){Console.WriteLine(error);return 1;}}
    static int Run(string[] args)
    {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        string assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets"),output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
        var catalog=LootCatalog.Load(Path.Combine(assets,"loot.json"));var definitions=MapCatalog.Load(assets);var rows=new List<string>{"map,width,height,zoom,grouped,enabled,icons,frames,medianMs,p95Ms,maxMs,hoverUs"};
        foreach(string id in new[]{"woods","ground-zero","factory","streets-of-tarkov"})
        using(var form=new Form {ClientSize=new Size(1920,1080),FormBorderStyle=FormBorderStyle.None,Location=Point.Empty,StartPosition=FormStartPosition.Manual})
        using(var map=new MapCanvas {Dock=DockStyle.Fill,MapOnly=true})
        {
            var definition=definitions.Single(d=>d.Id==id);var layer=definition.Layers[0];form.Controls.Add(map);form.Show();map.LoadMap(definition,layer,Path.Combine(assets,layer.VectorImage));map.ApplyAppearance(new AppPreferences {MapOpacity=100,BackgroundMode="tarkov"},assets);
            foreach(int zoom in new[]{1,4})foreach(bool grouped in new[]{true,false})foreach(bool enabled in new[]{false,true})
            {
                map.SetLoot(catalog,id,"regular",new LootPreferences {Enabled=enabled,GroupNearby=grouped,AllFloors=true});map.FitMap();map.Zoom(zoom);map.Refresh();map.SmoothZoom(1.2f);var times=new List<double>();
                for(int i=0;i<30;i++)
                {
                    typeof(MapCanvas).GetField("zoomStarted",Private).SetValue(map,Stopwatch.GetTimestamp()-(long)(Stopwatch.Frequency*.16*(i+.5)/30));typeof(MapCanvas).GetMethod("AdvanceZoom",Private).Invoke(map,null);
                    var clock=Stopwatch.StartNew();map.Refresh();clock.Stop();if(i>=4)times.Add(clock.Elapsed.TotalMilliseconds);
                }
                typeof(MapCanvas).GetField("zoomStarted",Private).SetValue(map,Stopwatch.GetTimestamp()-(long)(Stopwatch.Frequency*.2));typeof(MapCanvas).GetMethod("AdvanceZoom",Private).Invoke(map,null);typeof(MapCanvas).GetMethod("FinishZoomPreview",Private).Invoke(map,null);map.Refresh();
                var hover=Stopwatch.StartNew();for(int i=0;i<1000;i++)map.MarkerTooltipAt(new Point((i*173)%1920,(i*97)%1080));hover.Stop();times.Sort();
                string row=String.Format(CultureInfo.InvariantCulture,"{0},1920,1080,{1},{2},{3},{4},{5},{6:F3},{7:F3},{8:F3},{9:F2}",id,zoom,grouped,enabled,map.VisibleLootIcons,times.Count,times[times.Count/2],times[(int)(times.Count*.95)],times[times.Count-1],hover.Elapsed.TotalMilliseconds);rows.Add(row);Console.WriteLine(row);
            }
            form.Close();Application.DoEvents();
        }
        File.WriteAllLines(Path.Combine(output,"loot.csv"),rows);return 0;
    }
}
