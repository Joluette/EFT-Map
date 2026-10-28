// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace EftPosition
{
    // Same class, separate source file: no second map, renderer, tile worker or large source bitmap.
    public sealed partial class MapCanvas
    {
        public bool MiniMapHasPosition {get{return layer!=null && position!=null && layer.Contains(position);}}
        public Bitmap RenderMiniMap(Size size,int zoom)
        {
            if(size.Width<1 || size.Height<1 || size.Width>1040 || size.Height>1040)throw new ArgumentOutOfRangeException("size");
            var result=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb);
            try
            {
                using(Graphics g=Graphics.FromImage(result))
                {
                    g.Clear(Color.FromArgb(18,30,35));
                    if(!MiniMapHasPosition)
                    {
                        using(Pen grid=new Pen(Color.FromArgb(30,49,54)))
                        {for(int gx=0;gx<size.Width;gx+=24)g.DrawLine(grid,gx,0,gx,size.Height);for(int gy=0;gy<size.Height;gy+=24)g.DrawLine(grid,0,gy,size.Width,gy);}
                        string message=layer==null?UiText.T("Wczytywanie mapy…"):position==null?UiText.T("Brak pozycji.\nZrób zrzut w grze."):UiText.T("Poza obszarem mapy.\nWybierz właściwą mapę.");
                        TextRenderer.DrawText(g,message,UiTheme.Body,new Rectangle(18,18,Math.Max(1,size.Width-36),Math.Max(1,size.Height-36)),UiTheme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
                        return result;
                    }
                    double half=15000.0/Math.Max(100,Math.Min(400,zoom));
                    MapPoint[] corners={layer.Project(position.X-half,position.Z-half),layer.Project(position.X+half,position.Z-half),layer.Project(position.X-half,position.Z+half),layer.Project(position.X+half,position.Z+half)};
                    double minX=corners[0].X,maxX=minX,minY=corners[0].Y,maxY=minY;
                    foreach(MapPoint corner in corners){minX=Math.Min(minX,corner.X);maxX=Math.Max(maxX,corner.X);minY=Math.Min(minY,corner.Y);maxY=Math.Max(maxY,corner.Y);}
                    float viewScale=(float)Math.Min(size.Width/Math.Max(1,maxX-minX),size.Height/Math.Max(1,maxY-minY));
                    MapPoint center=layer.Project(position.X,position.Z);
                    float x=size.Width/2f-(float)center.X*viewScale,y=size.Height/2f-(float)center.Y*viewScale;
                    GraphicsState saved=g.Save();
                    try
                    {
                        if(vector!=null)vector.DrawShared(g,size,viewScale,x,y);
                        else if(map!=null){map.Draw(g,size,viewScale,x,y);if(tiles!=null)tiles.DrawCached(g,new Rectangle(Point.Empty,size),viewScale,x,y);}
                    }
                    finally{g.Restore(saved);}
                    g.SmoothingMode=SmoothingMode.AntiAlias;
                    if(definition!=null && definition.Id==markerMapId)
                    {
                        float markerScale=markerSize/100f*.80f;
                        for(int pass=0;pass<2;pass++)foreach(MapMarker marker in markers)
                        {
                            bool dim=IsMarkerDimmed(marker);if(dim!=(pass==0))continue;
                            MapPoint p=layer.Project(marker.X,marker.Z);float px=x+(float)p.X*viewScale,py=y+(float)p.Y*viewScale;
                            if(px< -20 || py< -20 || px>size.Width+20 || py>size.Height+35)continue;
                            g.DrawImage(markerArtwork[(int)marker.Kind+(dim?3:0)],new RectangleF(px-12*markerScale,py-27*markerScale,24*markerScale,30*markerScale));
                        }
                    }
                    float cx=size.Width/2f,cy=size.Height/2f;
                    Color color=Stale?Color.FromArgb(244,183,83):Color.FromArgb(74,231,206);
                    if(position.HasHeading)
                    {
                        MapPoint direction=layer.Direction(position.Yaw);float dx=(float)direction.X,dy=(float)direction.Y;
                        using(Brush cone=new SolidBrush(Color.FromArgb(70,color)))g.FillPolygon(cone,new[]{new PointF(cx,cy),new PointF(cx+dx*34-dy*16,cy+dy*34+dx*16),new PointF(cx+dx*34+dy*16,cy+dy*34-dx*16)});
                        using(Pen arrow=new Pen(color,2.5f))g.DrawLine(arrow,cx,cy,cx+dx*24,cy+dy*24);
                    }
                    using(Brush halo=new SolidBrush(Color.FromArgb(65,color)))g.FillEllipse(halo,cx-12,cy-12,24,24);
                    using(Brush dot=new SolidBrush(color))g.FillEllipse(dot,cx-5,cy-5,10,10);
                    using(Pen edge=new Pen(Color.White,2))g.DrawEllipse(edge,cx-6,cy-6,12,12);
                }
                return result;
            }
            catch{result.Dispose();throw;}
        }
    }
}
