// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class CreateAppIcon
{
    static GraphicsPath Round(float x,float y,float w,float h,float r)
    {
        var p=new GraphicsPath();p.AddArc(x,y,r*2,r*2,180,90);p.AddArc(x+w-r*2,y,r*2,r*2,270,90);
        p.AddArc(x+w-r*2,y+h-r*2,r*2,r*2,0,90);p.AddArc(x,y+h-r*2,r*2,r*2,90,90);p.CloseFigure();return p;
    }
    static Bitmap Draw(int size)
    {
        using(var large=new Bitmap(size*4,size*4))
        {
            using(Graphics g=Graphics.FromImage(large))
            {
                g.ScaleTransform(size/64f,size/64f);g.SmoothingMode=SmoothingMode.AntiAlias;
                using(var bg=Round(10,10,236,236,49))
                using(var gradient=new LinearGradientBrush(new Rectangle(0,0,256,256),Color.FromArgb(29,49,62),Color.FromArgb(11,22,31),65))
                using(var edge=new Pen(Color.FromArgb(62,89,104),2))
                {g.FillPath(gradient,bg);g.DrawPath(edge,bg);}
                PointF[] map={new PointF(30,88),new PointF(88,70),new PointF(161,88),new PointF(222,68),new PointF(222,178),new PointF(164,196),new PointF(92,178),new PointF(30,197)};
                using(var fill=new SolidBrush(Color.FromArgb(28,64,71)))g.FillPolygon(fill,map);
                using(var edge=new Pen(Color.FromArgb(61,110,113),3)){edge.LineJoin=LineJoin.Round;g.DrawPolygon(edge,map);g.DrawLine(edge,89,74,92,177);g.DrawLine(edge,162,90,164,192);}
                using(var route=new Pen(Color.FromArgb(74,139,144),3)){route.DashPattern=new float[]{2,3};g.DrawLines(route,new[]{new PointF(42,168),new PointF(66,135),new PointF(111,152),new PointF(185,116),new PointF(209,126)});}
                using(var pin=new GraphicsPath())
                using(var fill=new LinearGradientBrush(new Rectangle(78,50,100,155),Color.FromArgb(130,255,221),Color.FromArgb(40,185,158),75))
                using(var shadow=new Pen(Color.FromArgb(130,6,18,24),12))
                {
                    pin.AddBezier(128,208,113,186,78,143,78,102);pin.AddBezier(78,102,78,36,178,36,178,102);pin.AddBezier(178,102,178,143,143,186,128,208);pin.CloseFigure();
                    g.DrawPath(shadow,pin);g.FillPath(fill,pin);
                }
                using(var hole=new SolidBrush(Color.FromArgb(18,42,49)))g.FillEllipse(hole,103,77,50,50);
                using(var needle=new SolidBrush(Color.FromArgb(255,207,112)))g.FillPolygon(needle,new[]{new PointF(142,85),new PointF(134,108),new PointF(125,103),new PointF(118,94)});
                using(var needle=new SolidBrush(Color.FromArgb(239,255,248)))g.FillPolygon(needle,new[]{new PointF(114,118),new PointF(121,96),new PointF(130,101),new PointF(137,110)});
            }
            var result=new Bitmap(size,size);
            using(Graphics g=Graphics.FromImage(result)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(large,0,0,size,size);}
            return result;
        }
    }
    static void Main(string[] args)
    {
        string folder=Path.GetFullPath(args[0]);Directory.CreateDirectory(folder);
        int[] sizes={16,20,24,32,40,48,64,128,256};var images=new List<byte[]>();
        foreach(int size in sizes)using(Bitmap b=Draw(size))using(var bytes=new MemoryStream())
        {b.Save(bytes,ImageFormat.Png);images.Add(bytes.ToArray());if(size==256)b.Save(Path.Combine(folder,"app-icon.png"),ImageFormat.Png);}
        using(var file=new BinaryWriter(File.Create(Path.Combine(folder,"app.ico"))))
        {
            file.Write((ushort)0);file.Write((ushort)1);file.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++){file.Write((byte)(sizes[i]%256));file.Write((byte)(sizes[i]%256));file.Write((ushort)0);file.Write((ushort)1);file.Write((ushort)32);file.Write(images[i].Length);file.Write(offset);offset+=images[i].Length;}
            foreach(byte[] png in images)file.Write(png);
        }
        Console.WriteLine("Created app.ico: nine resolutions (16–256 px), transparent background.");
    }
}
