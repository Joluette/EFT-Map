// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.IO;
using System.Xml;
using EftPosition;

static class VectorMapTests
{
    static int assertions;
    static void Check(bool value,string message) {assertions++;if(!value)throw new Exception(message);}
    static int Count(Bitmap image,Color color)
    {
        int total=0;
        for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++) if(image.GetPixel(x,y).ToArgb()==color.ToArgb()) total++;
        return total;
    }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            string assets=Path.GetFullPath(args[0]),reports=Path.GetFullPath(args[1]); Directory.CreateDirectory(reports);
            Check(SvgPathCanonicalizer.Convert("m10 20 5 6z m3 4 5 6z")=="M 10 20 L 15 26 Z M 13 24 L 18 30 Z","relative moveto after closepath preserves current subpath origin");
            Check(SvgPathCanonicalizer.Convert("M1 2 q3 6 6 0 t6 0")=="M 1 2 C 3 6 5 6 7 2 C 9 -2 11 -2 13 2","quadratic and smooth reflection preserved");
            Check(SvgPathCanonicalizer.Convert("M0 0 a10 10 0 0110 20")=="M 0 0 A 10 10 0 0 1 10 20","packed arc flags parsed separately from coordinate");
            Check(SvgPathCanonicalizer.Convert("m10.5.5c1.5.5 2.5-1 3 4")=="M 10.5 0.5 C 12 1 13 -0.5 13.5 4.5","compact decimal syntax preserves geometry");
            int maps=0;
            foreach(string file in Directory.GetFiles(Path.Combine(assets,"vectors"),"*.svg"))
            {
                XmlDocument xml=new XmlDocument();xml.XmlResolver=null;xml.Load(file);
                int width=Int32.Parse(xml.DocumentElement.GetAttribute("width")),height=Int32.Parse(xml.DocumentElement.GetAttribute("height"));
                using(VectorMap map=new VectorMap(file,width,height)) using(Bitmap bitmap=new Bitmap(960,600)) using(Graphics g=Graphics.FromImage(bitmap))
                {
                    map.GetBuildingBounds();
                    float scale=Math.Min(960f/width,600f/height);
                    map.Draw(g,bitmap.Size,scale,0,0);map.Draw(g,bitmap.Size,scale,0,0);
                    Check(map.RenderCount==1,"unchanged viewport uses cache "+file);
                    map.Draw(g,bitmap.Size,4,-width*1.5f,-height*1.5f);
                    Check(map.RenderCount==2,"zoom rerenders vector geometry "+file);
                    Check(map.CacheBytes==960L*600*4,"only screen-sized bitmap retained "+file);
                }
                maps++;
            }
            Check(maps==30,"all thirty vector levels checked");
            using(VectorMap map=new VectorMap(Path.Combine(assets,"vectors","customs-base.svg"),3210,1632))
            using(Bitmap bitmap=new Bitmap(1070,544)) using(Graphics g=Graphics.FromImage(bitmap))
            {
                Check(map.GetBuildingBounds().Length>30,"building detection remains available after CSS normalization");
                map.Draw(g,bitmap.Size,1f/3,0,0);
                int water=Count(bitmap,Color.FromArgb(74,107,150));
                int trees=Count(bitmap,Color.FromArgb(20,64,67));
                int buildings=Count(bitmap,Color.FromArgb(26,38,50));
                Check(water>5000,"river is present, not lost through CSS inheritance");
                Check(trees>8000,"trees are present");Check(buildings>8000,"building silhouettes are present");
                int unexpected=0;
                for(int y=0;y<60;y++) for(int x=0;x<180;x++) if(bitmap.GetPixel(x,y).ToArgb()!=Color.FromArgb(24,39,43).ToArgb()) unexpected++;
                Check(unexpected==0,"no spurious diagonal strokes from the origin into buildings");
                bitmap.Save(Path.Combine(reports,"vector-fit-regression.png"),System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine("Customs pixels: water="+water+", trees="+trees+", buildings="+buildings+", unexpected origin pixels="+unexpected);
            }
            Console.WriteLine("PASS: "+assertions+" vector rendering checks; "+maps+" SVG levels, relative-subpath and CSS regressions.");return 0;
        }
        catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}
