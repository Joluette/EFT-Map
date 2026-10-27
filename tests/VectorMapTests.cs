// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.IO;
using System.Xml;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
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
    static void SamePixels(Bitmap a,Bitmap b,string message)
    {
        Rectangle area=new Rectangle(0,0,a.Width,a.Height);
        BitmapData first=a.LockBits(area,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        BitmapData second=b.LockBits(area,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        int different=0;
        try
        {
            int[] left=new int[a.Width],right=new int[a.Width];
            for(int y=0;y<a.Height;y++)
            {
                Marshal.Copy(IntPtr.Add(first.Scan0,y*first.Stride),left,0,left.Length);
                Marshal.Copy(IntPtr.Add(second.Scan0,y*second.Stride),right,0,right.Length);
                for(int x=0;x<left.Length;x++) if(left[x]!=right[x]) different++;
            }
        }
        finally {a.UnlockBits(first);b.UnlockBits(second);}
        Check(different<100,message+" (different pixels="+different+")");
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
            foreach(string name in new[]{"customs-base.svg","streets-of-tarkov-base.svg","woods-base.svg"})
            foreach(int opacity in new[]{100,96,65,40,0})
            {
                string path=Path.Combine(assets,"vectors",name);
                XmlDocument xml=new XmlDocument();xml.XmlResolver=null;xml.Load(path);
                int width=Int32.Parse(xml.DocumentElement.GetAttribute("width")),height=Int32.Parse(xml.DocumentElement.GetAttribute("height"));
                using(VectorMap moved=new VectorMap(path,width,height))
                using(VectorMap fresh=new VectorMap(path,width,height))
                using(Bitmap image=new Bitmap(960,600)) using(Graphics g=Graphics.FromImage(image))
                using(Bitmap reference=new Bitmap(960,600)) using(Graphics rg=Graphics.FromImage(reference))
                {
                    float scale=.8f,x=-110.25f,y=-205.5f;
                    moved.Draw(g,image.Size,scale,x,y,opacity);
                    foreach(Point delta in new[]{new Point(37,19),new Point(-61,0),new Point(0,-43),new Point(32,51),new Point(400,200),new Point(900,0),new Point(-950,-580)})
                    {
                        x+=delta.X;y+=delta.Y;
                        g.Clear(Color.Magenta);rg.Clear(Color.Magenta);
                        moved.Draw(g,image.Size,scale,x,y,opacity);
                        fresh.Draw(rg,reference.Size,scale*.99f,x,y,opacity);
                        rg.Clear(Color.Magenta);fresh.Draw(rg,reference.Size,scale,x,y,opacity);
                        SamePixels(image,reference,"integer pan matches full redraw at opacity "+opacity+": "+name);
                    }
                    Check(moved.ReusedPanCount==7,"all integer pans reuse old pixels: "+name);
                    Check(moved.CacheBytes==2L*960*600*4,"pan buffers are limited to two viewport bitmaps: "+name);
                    int renders=moved.RenderCount;
                    moved.Draw(g,image.Size,scale,x,y,opacity);Check(moved.RenderCount==renders,"unchanged translucent viewport reuses its cache");
                    int changedOpacity=opacity==40?96:40;
                    g.Clear(Color.Magenta);rg.Clear(Color.Magenta);
                    moved.Draw(g,image.Size,scale,x,y,changedOpacity);
                    fresh.Draw(rg,reference.Size,scale*.99f,x,y,changedOpacity);
                    rg.Clear(Color.Magenta);fresh.Draw(rg,reference.Size,scale,x,y,changedOpacity);
                    SamePixels(image,reference,"changing opacity at a fixed camera refreshes every pixel");
                    Check(moved.ReusedPanCount==7,"opacity changes never reuse previously faded pixels");
                    moved.Draw(g,image.Size,1.1f,x+.5f,y+.25f);
                    Check(moved.ReusedPanCount==7,"zoom/fractional movement is redrawn accurately: "+name);
                    using(Bitmap smaller=new Bitmap(320,200)) using(Graphics sg=Graphics.FromImage(smaller))
                    {moved.Draw(sg,smaller.Size,scale,x,y);Check(moved.CacheBytes==320L*200*4,"resize releases both old viewport buffers: "+name);}
                    moved.Dispose();Check(moved.CacheBytes==0,"both buffers released on disposal: "+name);
                }
            }
            // Compare the composed result against a single opacity pass over a
            // complete map. Markers/background are deliberately outside it.
            using(VectorMap map=new VectorMap(Path.Combine(assets,"vectors","ground-zero-base.svg"),720,1010))
            using(Bitmap source=new Bitmap(720,480,PixelFormat.Format32bppPArgb))
            using(Bitmap actual=new Bitmap(720,480))
            using(Bitmap expected=new Bitmap(720,480))
            using(Graphics sg=Graphics.FromImage(source))
            using(Graphics ag=Graphics.FromImage(actual))
            using(Graphics eg=Graphics.FromImage(expected))
            using(ImageAttributes alpha=new ImageAttributes())
            {
                foreach(int opacity in new[]{40,65,96,100})
                {
                    sg.Clear(Color.Transparent);map.Draw(sg,source.Size,.75f,210.25f,-25.5f);
                    var matrix=new ColorMatrix();matrix.Matrix33=opacity/100f;alpha.SetColorMatrix(matrix);
                    eg.Clear(Color.FromArgb(31,72,113));ag.Clear(Color.FromArgb(31,72,113));
                    eg.DrawImage(source,new Rectangle(Point.Empty,source.Size),0,0,source.Width,source.Height,GraphicsUnit.Pixel,alpha);
                    map.Draw(ag,actual.Size,.75f,210.25f,-25.5f,opacity);
                    int different=0;
                    for(int y=0;y<actual.Height;y+=3)for(int x=0;x<actual.Width;x+=3)
                    {Color a=actual.GetPixel(x,y),b=expected.GetPixel(x,y);if(Math.Abs(a.R-b.R)>2 || Math.Abs(a.G-b.G)>2 || Math.Abs(a.B-b.B)>2)different++;}
                    Check(different==0,"premultiplied opacity preserves colors and transparent edges at "+opacity+"% (different="+different+")");
                }
            }
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
