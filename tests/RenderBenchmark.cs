// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using EftPosition;

static class RenderBenchmark
{
    [STAThread]
    static int Main(string[] args)
    {
        string assets=args[0], output=args[1];
        var rows=new List<string>();
        rows.Add("map,width,height,zoom,motion,frames,medianMs,p95Ms,maxMs");
        foreach(string id in new[]{"customs","streets-of-tarkov","woods"})
        {
            string path=Path.Combine(assets,"vectors",id+"-base.svg");
            var xml=new System.Xml.XmlDocument();xml.XmlResolver=null;xml.Load(path);
            int w=Int32.Parse(xml.DocumentElement.GetAttribute("width")),h=Int32.Parse(xml.DocumentElement.GetAttribute("height"));
            using(var map=new VectorMap(path,w,h))
            foreach(Size size in new[]{new Size(960,600),new Size(1920,1080)})
            using(var image=new Bitmap(size.Width,size.Height))
            using(var g=Graphics.FromImage(image))
            foreach(float zoom in new[]{1f,8f})
            foreach(string motion in new[]{"pan","zoom"})
            {
                float scale=Math.Min((float)size.Width/w,(float)size.Height/h)*zoom;
                float x=(size.Width-w*scale)/2,y=(size.Height-h*scale)/2;
                map.Draw(g,size,scale,x,y);
                var times=new List<double>();
                for(int i=0;i<24;i++)
                {
                    var watch=Stopwatch.StartNew();
                    map.Draw(g,size,motion=="pan"?scale:scale*(1+i*.005f),x+i*4,y+i*2);
                    times.Add(watch.Elapsed.TotalMilliseconds);
                }
                times.Sort();
                string row=String.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6:F3},{7:F3},{8:F3}",id,size.Width,size.Height,zoom,motion,times.Count,times[times.Count/2],times[(int)(times.Count*.95)],times[times.Count-1]);
                rows.Add(row); Console.WriteLine(row);
            }
        }
        File.WriteAllLines(output,rows);
        return 0;
    }
}
