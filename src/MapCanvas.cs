// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace EftPosition
{
    public sealed class MapCanvas : Control
    {
        Bitmap map;
        VectorMap vector;
        TileMap tiles;
        volatile bool tilesChanged;
        MapDefinition definition;
        MapLayer layer;
        public string LoadingText { get; set; }
        public MapDefinition Definition { get { return definition; } }
        public MapLayer Layer { get { return layer; } }
        readonly Font labelFont = new Font("Segoe UI", 9, FontStyle.Bold);
        readonly Font smallFont = new Font("Segoe UI", 8);
        float scale = 1, offsetX, offsetY;
        Point dragStart;
        bool dragging, fitted;
        readonly Timer zoomTimer=new Timer { Interval=15 };
        bool zoomAnimating;
        long zoomStarted;
        float zoomFromScale,zoomFromX,zoomFromY,zoomToScale,zoomToX,zoomToY;
        const double ZoomDurationSeconds=.16;
        Size priorSize;
        readonly List<WorldBounds> buildingBounds=new List<WorldBounds>();
        Position position;
        MapMarker[] markers=new MapMarker[0];
        readonly Bitmap[] markerArtwork=new Bitmap[6];
        readonly Dictionary<MapMarker,string> markerLevels=new Dictionary<MapMarker,string>();
        AutoNavigator levelClassifier;
        string playerLevel;
        string markerMapId,markerTipText="";
        readonly ToolTip markerTip=new ToolTip {InitialDelay=350,ReshowDelay=100,AutoPopDelay=10000,ShowAlways=true};
        struct MarkerHit {public MapMarker Marker;public PointF Point;}
        readonly List<MarkerHit> markerHits=new List<MarkerHit>();
        public int VisibleMarkerCount {get{return markers.Length;}}
        public bool Follow { get; set; }
        public bool Stale { get; set; }
        public Position Position { get { return position; } }
        public bool MapOnly { get; set; }
        public bool IsVector { get { return vector!=null; } }
        public bool IsAnimating { get { return zoomAnimating; } }
        public MapCanvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(15, 23, 27);
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleName = "Mapa: kółko przybliża, przeciągnięcie przesuwa";
            zoomTimer.Tick+=delegate { AdvanceZoom(); };
            for(int i=0;i<6;i++)markerArtwork[i]=MarkerArtwork.Create((MapMarkerKind)(i%3),i>=3);
        }
        public void ClearMap()
        {
            StopZoom();
            markerHits.Clear();SetMarkerTip("");
            if(tiles!=null) { tiles.Dispose(); tiles=null; }
            if(vector!=null) { vector.Dispose(); vector=null; }
            if(map!=null) { map.Dispose(); map=null; }
            buildingBounds.Clear();
            definition=null; layer=null; fitted=false; Invalidate();
        }
        public void LoadMap(MapDefinition selected,MapLayer selectedLayer,string path,string cacheDirectory=null)
        {
            ClearMap();
            if(!String.IsNullOrEmpty(selectedLayer.VectorImage)) vector=new VectorMap(path,selectedLayer.Width,selectedLayer.Height);
            else using(Image img=Image.FromFile(path))
            {
                if(img.Width!=selectedLayer.Width || img.Height!=selectedLayer.Height)
                    throw new InvalidDataException("Nieprawidłowy obraz mapy: "+selected.Name);
                map=new Bitmap(img);
            }
            if(selectedLayer.Remote) tiles=new TileMap(selectedLayer,cacheDirectory??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cache"),delegate { tilesChanged=true; });
            definition=selected; layer=selectedLayer; LoadingText=null; FitMap();
            RefreshMarkerLevels();
            if(vector!=null)
            {
                foreach(RectangleF rectangle in vector.GetBuildingBounds())
                {
                    MapPoint a=layer.Unproject(rectangle.Left,rectangle.Top),b=layer.Unproject(rectangle.Right,rectangle.Bottom);
                    WorldBounds box=new WorldBounds(a.X,a.Y,b.X,b.Y);
                    if(box.Width>=3 && box.Depth>=3 && box.Width<=250 && box.Depth<=250) buildingBounds.Add(box);
                }
            }
            if(Follow) CenterPosition();
        }
        public WorldBounds BuildingAt(Position p)
        {
            WorldBounds found=null;
            foreach(WorldBounds box in buildingBounds) if(box.Contains(p.X,p.Z,0) && (found==null || box.Width*box.Depth<found.Width*found.Depth)) found=box;
            return found;
        }
        public void RefreshTiles()
        { if(tilesChanged) { tilesChanged=false; Invalidate(); } }
        public void SetPosition(Position p)
        {
            position = p;
            RefreshPlayerLevel();
            if (Follow) CenterPosition();
            Invalidate();
        }
        public void SetMarkers(string mapId,MapMarker[] points)
        {markerMapId=mapId;markers=points??new MapMarker[0];RefreshMarkerLevels();markerHits.Clear();SetMarkerTip("");Invalidate();}
        public void SetMarkerLevels(AutoNavigator classifier)
        {levelClassifier=classifier;RefreshMarkerLevels();Invalidate();}
        void RefreshPlayerLevel()
        {playerLevel=levelClassifier!=null && position!=null?levelClassifier.LevelAt(position.X,position.Y,position.Z):null;}
        void RefreshMarkerLevels()
        {
            markerLevels.Clear();RefreshPlayerLevel();
            if(levelClassifier==null)return;
            foreach(MapMarker marker in markers)if(marker.Kind==MapMarkerKind.Quest && marker.Y.HasValue)
                markerLevels[marker]=levelClassifier.LevelAt(marker.X,marker.Y.Value,marker.Z);
        }
        public bool IsMarkerDimmed(MapMarker marker)
        {
            string target;
            string current=playerLevel??(layer==null?null:layer.Id);
            return current!=null && marker.Kind==MapMarkerKind.Quest && markerLevels.TryGetValue(marker,out target) && target!=null && target!=current;
        }
        public PointF MarkerScreenPoint(double x,double z)
        {if(layer==null)return PointF.Empty;MapPoint p=layer.Project(x,z);return new PointF(offsetX+(float)p.X*scale,offsetY+(float)p.Y*scale);}
        public string MarkerTooltipAt(Point point)
        {
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(MarkerHit hit in markerHits)
            {if(new RectangleF(hit.Point.X-13,hit.Point.Y-28,26,31).Contains(point))names.Add(hit.Marker.Name);}
            var sorted=new List<string>(names);sorted.Sort(StringComparer.OrdinalIgnoreCase);return String.Join("\n",sorted.ToArray());
        }
        void SetMarkerTip(string text)
        {if(text!=markerTipText){markerTipText=text;markerTip.SetToolTip(this,text.Length==0?null:text);}}
        float FitScale { get { return layer==null?1:Math.Max(.01f, Math.Min((Width-48f)/layer.Width, (Height-70f)/layer.Height)); } }
        public void FitMap()
        {
            StopZoom();
            scale = FitScale;
            offsetX = layer==null?0:(Width-layer.Width*scale)/2;
            offsetY = layer==null?0:(Height-layer.Height*scale)/2;
            fitted = true;
            Invalidate();
        }
        public void CenterPosition()
        {
            if (position == null || layer==null || !layer.Contains(position)) return;
            StopZoom();
            MapPoint p = layer.Project(position.X, position.Z);
            offsetX = Width/2f-(float)p.X*scale;
            offsetY = Height/2f-(float)p.Y*scale;
            Invalidate();
        }
        public void FocusArea(WorldBounds bounds)
        {
            if(bounds==null || layer==null) return;
            StopZoom();
            MapPoint[] points={layer.Project(bounds.MinX,bounds.MinZ),layer.Project(bounds.MinX,bounds.MaxZ),
                layer.Project(bounds.MaxX,bounds.MinZ),layer.Project(bounds.MaxX,bounds.MaxZ)};
            double minX=points[0].X,maxX=minX,minY=points[0].Y,maxY=minY;
            foreach(MapPoint p in points) {minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);minY=Math.Min(minY,p.Y);maxY=Math.Max(maxY,p.Y);}
            float next=(float)Math.Min(Math.Max(1,Width-96)/(Math.Max(1,maxX-minX)*1.12),Math.Max(1,Height-96)/(Math.Max(1,maxY-minY)*1.12));
            scale=Math.Max(FitScale/2,Math.Min(64,next));
            offsetX=Width/2f-(float)((minX+maxX)/2)*scale;
            offsetY=Height/2f-(float)((minY+maxY)/2)*scale;
            fitted=true; Invalidate();
        }
        public void FocusNeighbourhood(double metres)
        {
            if(position==null) return;
            FocusArea(new WorldBounds(position.X-metres/2,position.Z-metres/2,position.X+metres/2,position.Z+metres/2));
        }
        public void Zoom(float factor) { ZoomAt(factor, Width/2, Height/2); }
        public void SmoothZoom(float factor) { SmoothZoomAt(factor,Width/2,Height/2); }
        void StopZoom()
        { zoomAnimating=false; zoomTimer.Stop(); }
        void AdvanceZoom()
        {
            if(!zoomAnimating) return;
            double t=Math.Min(1,(Stopwatch.GetTimestamp()-zoomStarted)/(double)Stopwatch.Frequency/ZoomDurationSeconds);
            float ease=(float)(1-Math.Pow(1-t,3));
            scale=zoomFromScale+(zoomToScale-zoomFromScale)*ease;
            offsetX=zoomFromX+(zoomToX-zoomFromX)*ease;
            offsetY=zoomFromY+(zoomToY-zoomFromY)*ease;
            if(t>=1) { scale=zoomToScale;offsetX=zoomToX;offsetY=zoomToY;StopZoom(); }
            Invalidate();
        }
        void SmoothZoomAt(float factor,int x,int y)
        {
            SetMarkerTip("");
            if(layer==null || factor<=0 || Single.IsNaN(factor) || Single.IsInfinity(factor)) return;
            if(dragging) { ZoomAt(factor,x,y);return; }
            float desired=zoomAnimating?zoomToScale:scale;
            AdvanceZoom();
            float next=Math.Max(FitScale/2,Math.Min(64,desired*factor));
            zoomFromScale=scale;zoomFromX=offsetX;zoomFromY=offsetY;
            zoomToScale=next;
            zoomToX=x-(x-offsetX)*(next/scale);
            zoomToY=y-(y-offsetY)*(next/scale);
            zoomStarted=Stopwatch.GetTimestamp();
            zoomAnimating=true;zoomTimer.Start();
        }
        void ZoomAt(float factor, int x, int y)
        {
            SetMarkerTip("");
            if(factor<=0 || Single.IsNaN(factor) || Single.IsInfinity(factor)) return;
            StopZoom();
            float next = Math.Max(FitScale/2, Math.Min(64, scale*factor));
            float ratio = next/scale;
            offsetX = x-(x-offsetX)*ratio;
            offsetY = y-(y-offsetY)*ratio;
            scale = next;
            Invalidate();
        }
        // Used by the optional memory benchmark, never sends keyboard/mouse input.
        public void ExerciseView(int step)
        {
            int phase=step%24;
            if(phase==0) FitMap();
            else if(phase<8) SmoothZoom(1.17f);
            else if(phase<16) { StopZoom();offsetX+=4;offsetY-=2;Invalidate(); }
            else SmoothZoom(.95f);
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            SetMarkerTip("");markerHits.Clear();
            Size before=priorSize; priorSize=ClientSize;
            if(layer==null) return;
            StopZoom();
            // Resizing/fullscreen should retain the chosen zoom instead of zooming out.
            if(Follow) CenterPosition();
            else { if(before.Width>0 && before.Height>0) {offsetX+=(Width-before.Width)/2f;offsetY+=(Height-before.Height)/2f;} Invalidate(); }
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        { base.OnMouseWheel(e); if(e.Delta!=0) SmoothZoomAt((float)Math.Pow(1.2,e.Delta/120.0),e.X,e.Y); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            SetMarkerTip("");
            AdvanceZoom(); StopZoom();
            Focus(); dragging = true; dragStart = e.Location; Capture = true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) {SetMarkerTip(zoomAnimating?"":MarkerTooltipAt(e.Location));return;}
            offsetX += e.X-dragStart.X; offsetY += e.Y-dragStart.Y; dragStart = e.Location;
            Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; Capture = false; }
        protected override void OnMouseLeave(EventArgs e) {SetMarkerTip("");base.OnMouseLeave(e);}
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) dragging = false; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if((map==null && vector==null) || layer==null)
            {
                using(Brush text=new SolidBrush(Color.FromArgb(180,194,199)))
                    e.Graphics.DrawString(LoadingText??"Wybierz mapę z menu.",labelFont,text,24,32);
                return;
            }
            if (!fitted) FitMap();
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            if(vector!=null) vector.Draw(g,ClientSize,scale,offsetX,offsetY);
            else
            {
                g.DrawImage(map, new RectangleF(offsetX, offsetY, map.Width*scale, map.Height*scale));
                if(tiles!=null) tiles.Draw(g,ClientRectangle,scale,offsetX,offsetY);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if(definition.Labels!=null) foreach(MapLabel label in definition.Labels) DrawLabel(g,label.Text,label.X,label.Z);
            DrawMarkers(g);
            if (position != null && layer.Contains(position))
            {
                MapPoint p = layer.Project(position.X, position.Z);
                float cx = offsetX+(float)p.X*scale, cy = offsetY+(float)p.Y*scale;
                Color color = Stale ? Color.FromArgb(244, 183, 83) : Color.FromArgb(74, 231, 206);
                if (position.HasHeading)
                {
                    MapPoint d = layer.Direction(position.Yaw);
                    float dx = (float)d.X, dy = (float)d.Y;
                    PointF[] cone = { new PointF(cx,cy), new PointF(cx+dx*52-dy*23,cy+dy*52+dx*23),
                        new PointF(cx+dx*52+dy*23,cy+dy*52-dx*23) };
                    using (Brush brush = new SolidBrush(Color.FromArgb(75, color))) g.FillPolygon(brush, cone);
                    using (Pen pen = new Pen(color, 3)) g.DrawLine(pen, cx, cy, cx+dx*32, cy+dy*32);
                }
                using (Brush halo = new SolidBrush(Color.FromArgb(65, color))) g.FillEllipse(halo,cx-16,cy-16,32,32);
                using (Brush center = new SolidBrush(color)) g.FillEllipse(center,cx-7,cy-7,14,14);
                using (Pen outline = new Pen(Color.White, 2)) g.DrawEllipse(outline,cx-8,cy-8,16,16);
            }
            if(!MapOnly)
            {
              using (Brush strip = new SolidBrush(Color.FromArgb(230, 15, 23, 27))) g.FillRectangle(strip,0,Height-30,Width,30);
              using (Brush text = new SolidBrush(Color.FromArgb(180,194,199)))
              {
                g.DrawString("Mapa: "+definition.Author+" · "+(layer.Remote?"prawa autorów · cache lokalny":"CC BY-NC-SA 4.0")+" · "+layer.Name, smallFont,text,12,Height-23);
                string zoom = (scale/FitScale).ToString("0.0") + "×";
                g.DrawString(zoom,smallFont,text,Width-44,Height-23);
              }
            }
        }
        void DrawMarkers(Graphics graphics)
        {
            markerHits.Clear();if(definition.Id!=markerMapId)return;
            // Paint dimmed targets first, beneath targets on the player's floor.
            for(int pass=0;pass<2;pass++)
            {
                foreach(MapMarker marker in markers)
                {
                    bool dim=IsMarkerDimmed(marker);if(dim!=(pass==0))continue;
                    PointF p=MarkerScreenPoint(marker.X,marker.Z);
                    if(p.X< -13 || p.X>Width+13 || p.Y< -3 || p.Y>(MapOnly?Height:Height-30)+28)continue;
                    markerHits.Add(new MarkerHit {Marker=marker,Point=p});
                    graphics.DrawImageUnscaled(markerArtwork[(int)marker.Kind+(dim?3:0)],(int)Math.Round(p.X)-12,(int)Math.Round(p.Y)-27);
                }
            }
        }
        void DrawLabel(Graphics g, string text, double x, double z)
        {
            MapPoint p = layer.Project(x,z);
            float cx=offsetX+(float)p.X*scale, cy=offsetY+(float)p.Y*scale;
            SizeF size=g.MeasureString(text,labelFont);
            if (cx < -size.Width || cx > Width+size.Width || cy < 0 || cy > Height-35) return;
            using (Brush bg = new SolidBrush(Color.FromArgb(175,15,23,27))) g.FillRectangle(bg,cx-size.Width/2-3,cy-10,size.Width+6,size.Height+2);
            using (Brush fg = new SolidBrush(Color.FromArgb(225,231,228))) g.DrawString(text,labelFont,fg,cx-size.Width/2,cy-9);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { ClearMap(); foreach(Bitmap image in markerArtwork)if(image!=null)image.Dispose();markerTip.Dispose(); zoomTimer.Dispose(); labelFont.Dispose(); smallFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
