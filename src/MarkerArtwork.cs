// SPDX-License-Identifier: GPL-3.0-only
using System.Drawing;
using System.Drawing.Drawing2D;

namespace EftPosition
{
    internal static class MarkerArtwork
    {
        internal static Bitmap Create(MapMarkerKind kind,bool dim)
        {
            // Six pre-rendered badges: panning never rebuilds their geometry.
            using(Bitmap large=new Bitmap(96,120))
            {
                using(Graphics g=Graphics.FromImage(large))
                {
                    g.ScaleTransform(4,4);g.SmoothingMode=SmoothingMode.AntiAlias;
                    Color hue=kind==MapMarkerKind.Quest?Color.FromArgb(255,199,96):kind==MapMarkerKind.Pmc?Color.FromArgb(100,230,175):Color.FromArgb(127,187,255);
                    if(dim)hue=Color.FromArgb(115,130,139);
                    using(GraphicsPath body=new GraphicsPath())
                    using(Brush shadow=new SolidBrush(Color.FromArgb(dim?32:110,0,0,0)))
                    using(Brush fill=new SolidBrush(Color.FromArgb(dim?110:248,18,30,39)))
                    using(Pen edge=new Pen(Color.FromArgb(dim?100:255,hue),1.5f))
                    using(Pen glyph=new Pen(Color.FromArgb(dim?105:255,hue),1.6f))
                    {
                        glyph.StartCap=glyph.EndCap=LineCap.Round;glyph.LineJoin=LineJoin.Round;
                        g.FillEllipse(shadow,2,6,21,22);
                        if(kind==MapMarkerKind.Quest)
                        {
                            body.StartFigure();body.AddBezier(12,27,10,24,2,19,2,12);body.AddBezier(2,12,2,-1,22,-1,22,12);body.AddBezier(22,12,22,19,14,24,12,27);body.CloseFigure();
                        }
                        else
                        {
                            body.FillMode=FillMode.Winding;
                            body.AddPolygon(new[]{new PointF(9,20),new PointF(12,26),new PointF(15,20)});
                            if(kind==MapMarkerKind.Scav)body.AddEllipse(2.5f,2.5f,19,19);
                            else using(GraphicsPath rounded=UiTheme.Round(new RectangleF(2.5f,2.5f,19,19),5))body.AddPath(rounded,false);
                        }
                        g.FillPath(fill,body);g.DrawPath(edge,body);
                        if(kind==MapMarkerKind.Quest)
                        {
                            g.DrawLine(glyph,9,7,9,18);
                            g.DrawLines(glyph,new[]{new PointF(9,7),new PointF(16,8),new PointF(14,11),new PointF(16,14),new PointF(9,13)});
                        }
                        else
                        {
                            g.DrawLines(glyph,new[]{new PointF(11,7),new PointF(7,7),new PointF(7,17),new PointF(11,17)});
                            g.DrawLine(glyph,10,12,17,12);g.DrawLines(glyph,new[]{new PointF(14,9),new PointF(17,12),new PointF(14,15)});
                        }
                    }
                }
                Bitmap result=new Bitmap(24,30);
                using(Graphics g=Graphics.FromImage(result)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(large,0,0,24,30);}
                return result;
            }
        }
    }
}
