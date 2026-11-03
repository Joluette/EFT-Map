// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace EftPosition
{
    // Original, size-independent pictograms. Rasterized once and shared by map and legend.
    internal static class LootArtwork
    {
        internal static Color ColorFor(string icon)
        {
            if(icon=="mine")return Color.FromArgb(255,206,119);
            if(icon=="sniper")return Color.FromArgb(255,147,147);
            if(icon=="warning")return Color.FromArgb(255,172,111);
            if(icon=="lock")return Color.FromArgb(220,190,255);
            if(icon=="injectors" || icon.Contains("med"))return Color.FromArgb(255,147,167);
            if(icon=="valuables" || icon.Contains("safe") || icon.Contains("register"))return Color.FromArgb(245,212,129);
            if(icon.Contains("weapon") || icon.Contains("ammo") || icon=="stationary" || icon=="grenade-box")return Color.FromArgb(152,193,255);
            if(icon=="electronics" || icon=="pc-block" || icon=="energy-elements")return Color.FromArgb(124,207,245);
            if(icon.Contains("key"))return Color.FromArgb(203,179,255);
            if(icon=="drinks" || icon=="ration-supply-crate")return Color.FromArgb(184,218,139);
            if(icon=="mixed")return Color.FromArgb(226,235,244);
            return Color.FromArgb(135,224,199);
        }
        internal static Bitmap Create(string icon,int size)
        {
            icon=icon??"others";
            using(var large=new Bitmap(104,104,PixelFormat.Format32bppPArgb))
            {
                using(var g=Graphics.FromImage(large))
                using(var fill=new SolidBrush(Color.FromArgb(247,17,27,35)))
                using(var shadow=new SolidBrush(Color.FromArgb(90,0,0,0)))
                using(var edge=new Pen(Color.FromArgb(165,ColorFor(icon)),.85f))
                using(var pen=new Pen(ColorFor(icon),1.55f))
                {
                    g.ScaleTransform(4,4);g.SmoothingMode=SmoothingMode.AntiAlias;
                    pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
                    bool hazard=icon=="mine" || icon=="sniper" || icon=="warning";
                    using(var shape=hazard?Triangle():UiTheme.Round(new RectangleF(2,2,22,22),6))
                    {
                        g.TranslateTransform(0,1);g.FillPath(shadow,shape);g.TranslateTransform(0,-1);
                        g.FillPath(fill,shape);g.DrawPath(edge,shape);
                    }
                    DrawGlyph(g,pen,icon);
                }
                var image=new Bitmap(size,size,PixelFormat.Format32bppPArgb);
                using(var g=Graphics.FromImage(image)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(large,new Rectangle(0,0,size,size));}return image;
            }
        }
        static GraphicsPath Triangle(){var p=new GraphicsPath();p.AddPolygon(new[]{new PointF(13,2),new PointF(25,23),new PointF(1,23)});return p;}
        static void Line(Graphics g,Pen p,params float[] xy){var points=new PointF[xy.Length/2];for(int i=0;i<points.Length;i++)points[i]=new PointF(xy[i*2],xy[i*2+1]);g.DrawLines(p,points);}
        static void Cross(Graphics g,Pen p,float x,float y){g.DrawLine(p,x-3,y,x+3,y);g.DrawLine(p,x,y-3,x,y+3);}
        static void Box(Graphics g,Pen p){g.DrawRectangle(p,6,8,14,11);g.DrawLine(p,6,11,20,11);g.DrawLine(p,9,8,9,19);g.DrawLine(p,17,8,17,19);}
        static void DrawGlyph(Graphics g,Pen p,string icon)
        {
            switch(icon)
            {
                case "safe":case "bank-safe":
                    g.DrawRectangle(p,6,6,14,14);g.DrawLine(p,9,6,9,20);g.DrawEllipse(p,12,10,5,5);Cross(g,p,14.5f,12.5f);if(icon=="bank-safe")g.DrawLine(p,11,18,18,18);break;
                case "cash-register":case "bank-cash-register":
                    Line(g,p,6,17,7,10,18,10,20,17,20,20,6,20,6,17,20,17);g.DrawRectangle(p,10,5,7,4);g.DrawLine(p,10,13,16,13);if(icon=="bank-cash-register")g.DrawLine(p,13,5,13,9);break;
                case "duffle-bag":
                    using(var shape=UiTheme.Round(new RectangleF(5,10,16,10),3))g.DrawPath(p,shape);g.DrawArc(p,9,6,8,9,180,180);g.DrawLine(p,9,11,9,19);g.DrawLine(p,17,11,17,19);break;
                case "jacket":
                    Line(g,p,9,6,6,8,4,15,8,16,8,21,18,21,18,16,22,15,20,8,17,6,13,10,9,6);g.DrawLine(p,13,10,13,21);g.DrawLine(p,10,16,11,16);break;
                case "drawer":
                    g.DrawRectangle(p,7,5,12,16);g.DrawLine(p,7,10,19,10);g.DrawLine(p,7,15,19,15);for(int y=8;y<20;y+=5)g.DrawLine(p,12,y,14,y);break;
                case "pc-block":
                    g.DrawRectangle(p,7,5,12,16);g.DrawRectangle(p,10,8,6,3);g.DrawLine(p,10,14,16,14);g.DrawEllipse(p,13,17,1,1);break;
                case "medbag":case "medcase":
                    using(var shape=UiTheme.Round(new RectangleF(6,9,14,11),icon=="medbag"?4:1))g.DrawPath(p,shape);g.DrawArc(p,10,5,6,8,180,180);Cross(g,p,13,14);break;
                case "wooden-crate":case "wooden-ammo-box":case "technical-supply-crate":case "medical-supply-crate":case "ration-supply-crate":
                    Box(g,p);
                    if(icon=="wooden-ammo-box"){Line(g,p,12,17,12,14,13,12,14,14,14,17);}
                    else if(icon=="medical-supply-crate")Cross(g,p,13,15);
                    else if(icon=="ration-supply-crate"){g.DrawLine(p,12,13,12,17);g.DrawLine(p,15,13,15,17);g.DrawLine(p,11,14,13,14);}
                    else if(icon=="technical-supply-crate"){g.DrawLine(p,11,17,15,13);g.DrawArc(p,13,12,4,4,0,270);}
                    else{g.DrawLine(p,10,12,16,18);g.DrawLine(p,16,12,10,18);}break;
                case "toolbox":
                    g.DrawRectangle(p,5,10,16,10);g.DrawRectangle(p,10,6,6,4);g.DrawLine(p,5,14,21,14);g.DrawRectangle(p,11,13,4,3);break;
                case "weapon-box":
                    g.DrawRectangle(p,4,8,18,11);Line(g,p,7,14,10,12,17,12,19,13);g.DrawLine(p,12,13,12,16);g.DrawLine(p,7,10,7,8);g.DrawLine(p,19,10,19,8);break;
                case "grenade-box":
                    g.DrawRectangle(p,5,8,16,12);g.DrawEllipse(p,10,12,6,6);Line(g,p,13,12,13,10,16,10,17,12);break;
                case "plastic-suitcase":
                    using(var shape=UiTheme.Round(new RectangleF(7,8,12,13),2))g.DrawPath(p,shape);g.DrawRectangle(p,10,5,6,3);g.DrawLine(p,10,11,10,18);g.DrawLine(p,16,11,16,18);break;
                case "ground-cache":
                    g.DrawEllipse(p,5,9,16,10);g.DrawEllipse(p,8,11,10,6);g.DrawLine(p,4,20,22,20);break;
                case "buried-barrel-cache":
                    g.DrawEllipse(p,7,6,12,4);Line(g,p,7,8,7,19,10,21,16,21,19,19,19,8);g.DrawLine(p,7,13,19,13);g.DrawLine(p,7,17,19,17);break;
                case "shturmans-stash":
                    Box(g,p);g.DrawRectangle(p,11,13,4,4);g.DrawArc(p,11,11,4,5,180,180);break;
                case "dead-scav":case "scav-body":case "pmc-body":case "civilian-body":case "lab-technician-body":
                    g.DrawEllipse(p,10,5,6,6);Line(g,p,7,20,8,15,11,13,15,13,18,15,19,20);g.DrawLine(p,10,20,16,20);
                    if(icon=="lab-technician-body")Cross(g,p,13,17);else if(icon=="pmc-body")g.DrawLine(p,10,7,16,7);else if(icon=="dead-scav")g.DrawLine(p,9,12,17,20);else if(icon=="scav-body")g.DrawLine(p,10,9,16,9);else g.DrawLine(p,13,14,13,19);break;
                case "injectors":
                    Line(g,p,7,20,18,9,14,5,3,16,7,20);g.DrawLine(p,5,18,3,20);g.DrawLine(p,14,5,16,3);g.DrawLine(p,18,9,20,7);g.DrawLine(p,15,2,21,8);g.DrawLine(p,9,12,12,15);g.DrawLine(p,11,10,13,12);break;
                case "electronics":
                    g.DrawRectangle(p,8,8,10,10);g.DrawRectangle(p,11,11,4,4);for(int i=9;i<18;i+=4){g.DrawLine(p,5,i,8,i);g.DrawLine(p,18,i,21,i);g.DrawLine(p,i,5,i,8);g.DrawLine(p,i,18,i,21);}break;
                case "medical-supplies":
                    g.DrawRectangle(p,8,8,11,12);g.DrawRectangle(p,10,5,7,3);Cross(g,p,13.5f,14);break;
                case "valuables":
                    Line(g,p,5,10,9,5,17,5,21,10,13,21,5,10,21,10);Line(g,p,9,5,10,10,13,21,16,10,17,5);break;
                case "info-items":case "battle-pass-documents":
                    Line(g,p,7,5,16,5,20,9,20,21,7,21,7,5);Line(g,p,16,5,16,9,20,9);g.DrawLine(p,10,13,17,13);g.DrawLine(p,10,17,15,17);if(icon=="battle-pass-documents")g.DrawEllipse(p,3,7,6,6);break;
                case "electronic-keys":
                    g.DrawRectangle(p,5,7,16,13);g.DrawRectangle(p,8,10,4,4);g.DrawLine(p,15,11,18,11);g.DrawLine(p,15,15,18,15);break;
                case "mechanical-keys":case "lock":
                    if(icon=="lock"){using(var shape=UiTheme.Round(new RectangleF(6,11,14,10),2))g.DrawPath(p,shape);g.DrawArc(p,9,4,8,12,180,180);g.DrawLine(p,13,15,13,18);}
                    else{g.DrawEllipse(p,5,5,9,9);Line(g,p,13,13,21,21,23,19);g.DrawLine(p,17,17,19,15);}break;
                case "drinks":
                    Line(g,p,10,5,16,5,16,9,19,12,19,21,7,21,7,12,10,9,10,5);g.DrawLine(p,8,14,18,14);break;
                case "energy-elements":
                    g.DrawRectangle(p,6,8,14,12);g.DrawRectangle(p,10,5,6,3);Line(g,p,14,10,11,15,15,15,12,19);break;
                case "flammable-materials":
                    Line(g,p,13,4,14,10,18,8,20,14,18,20,12,22,7,19,6,14,10,10,10,15,13,4);break;
                case "storage-containers":
                    Line(g,p,5,10,13,6,21,10,21,19,13,23,5,19,5,10,13,14,21,10);g.DrawLine(p,13,14,13,23);break;
                case "special-equipment":
                    g.DrawArc(p,12,4,8,8,0,270);Line(g,p,13,11,5,19,8,22,16,14);break;
                case "stationary":
                    Line(g,p,5,10,18,10,21,8,21,12,11,12,9,15);g.DrawLine(p,13,12,13,17);g.DrawLine(p,13,17,7,22);g.DrawLine(p,13,17,19,22);break;
                case "mine":
                    g.DrawEllipse(p,8,14,10,5);g.DrawLine(p,13,11,13,14);g.DrawLine(p,7,12,9,14);g.DrawLine(p,19,12,17,14);g.DrawLine(p,13,17,13,17.2f);break;
                case "sniper":
                    g.DrawEllipse(p,8,11,10,10);g.DrawLine(p,13,9,13,13);g.DrawLine(p,13,19,13,22);g.DrawLine(p,6,16,10,16);g.DrawLine(p,16,16,20,16);break;
                case "warning":g.DrawLine(p,13,10,13,16);g.DrawEllipse(p,12.5f,19,1,1);break;
                case "mixed":
                    Line(g,p,5,10,13,6,21,10,13,14,5,10);Line(g,p,5,14,13,18,21,14);Line(g,p,5,18,13,22,21,18);break;
                default:
                    g.DrawEllipse(p,7,7,12,12);g.DrawLine(p,13,10,13,16);g.DrawLine(p,10,13,16,13);break;
            }
        }
    }
}
