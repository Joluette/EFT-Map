// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
namespace EftPosition
{
    internal sealed class MapLabelRenderer : IDisposable
    {
        readonly Dictionary<string,Bitmap> sprites=new Dictionary<string,Bitmap>(StringComparer.Ordinal);
        readonly List<RectangleF> occupied=new List<RectangleF>();
        internal int VisibleCount {get;private set;}
        Bitmap Label(string text,bool major)
        {
            string key=(major?"major:":"minor:")+text;
            Bitmap image;if(sprites.TryGetValue(key,out image))return image;
            float size=major?19:13,stroke=major?2:1.6f;
            using(var font=new Font("Bahnschrift",size,FontStyle.Regular,GraphicsUnit.Pixel))
            using(var path=new GraphicsPath())
            {
                path.AddString(text,font.FontFamily,(int)FontStyle.Regular,size,PointF.Empty,StringFormat.GenericTypographic);
                RectangleF bounds=path.GetBounds();const int padding=3;
                using(var translate=new Matrix()){translate.Translate(padding-bounds.X,padding-bounds.Y);path.Transform(translate);}
                image=new Bitmap(Math.Max(1,(int)Math.Ceiling(bounds.Width)+padding*2),Math.Max(1,(int)Math.Ceiling(bounds.Height)+padding*2+1),PixelFormat.Format32bppPArgb);
                using(var target=Graphics.FromImage(image))
                using(var edge=new Pen(Color.FromArgb(245,34,34,27),stroke))
                using(var textBrush=new SolidBrush(Color.FromArgb(222,218,190)))
                using(var shadow=new Pen(Color.FromArgb(100,0,0,0),stroke+1))
                {
                    target.SmoothingMode=SmoothingMode.AntiAlias;
                    edge.LineJoin=shadow.LineJoin=LineJoin.Round;
                    target.TranslateTransform(0,1);target.DrawPath(shadow,path);target.TranslateTransform(0,-1);
                    target.DrawPath(edge,path);target.FillPath(textBrush,path);
                }
            }
            sprites.Add(key,image);return image;
        }
        internal void Draw(Graphics g,MapDefinition definition,MapLayer layer,Size viewport,float scale,float x,float y)
        {
            occupied.Clear();VisibleCount=0;if(definition==null || definition.Labels==null)return;
            // Landmarks get space before the smaller local names.
            foreach(var label in definition.Labels.OrderByDescending(l=>l.Priority))
            {
                if(String.IsNullOrWhiteSpace(label.Text))continue;
                Bitmap image=Label(label.Text,label.Priority>0);var p=layer.Project(label.X,label.Z);
                float cx=x+(float)p.X*scale,cy=y+(float)p.Y*scale;
                var rect=new RectangleF((float)Math.Round(cx-image.Width/2f),(float)Math.Round(cy-image.Height/2f),image.Width,image.Height);
                if(rect.Right<0 || rect.Left>viewport.Width || rect.Bottom<0 || rect.Top>viewport.Height-30)continue;
                var padded=rect;padded.Inflate(4,3);bool overlap=false;foreach(var other in occupied)if(other.IntersectsWith(padded)){overlap=true;break;}
                if(overlap)continue;occupied.Add(rect);g.DrawImageUnscaled(image,(int)rect.X,(int)rect.Y);VisibleCount++;
            }
        }
        public void Dispose(){foreach(var image in sprites.Values)image.Dispose();sprites.Clear();}
    }
}
