// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
namespace EftPosition
{
    public sealed partial class MapCanvas
    {
        readonly LootRenderer loot=new LootRenderer();
        public int VisibleLootIcons {get{return loot.VisibleCount;}}
        internal int LootViewBuildCount {get{return loot.ViewBuildCount;}}
        public void SetLoot(LootCatalog catalog,string mapId,string mode,LootPreferences options)
        {loot.Configure(catalog,mapId,mode,options);loot.Bind(definition,layer,levelClassifier);UpdateLootRegions();SceneRevision++;SetMarkerTip("");Invalidate();}
        long rasterRegionRevision=-1;
        void UpdateLootRegions(){if(vector!=null)vector.SetOverlay(loot.DrawRegions,loot.RegionRevision);if(rasterRegionRevision!=loot.RegionRevision){rasterRegionRevision=loot.RegionRevision;rasterViewport.Invalidate();}}
    }
    sealed class LootRenderer : IDisposable
    {
        sealed class Projected {public LootPoint Source;public float X,Y;public string Floor;public PointF[] Outline;}
        sealed class Cell {public int X,Y;public readonly List<Projected> Points=new List<Projected>();}
        sealed class Glyph {public float X,Y;public string Icon;public readonly List<Projected> Points=new List<Projected>();}
        sealed class Area {public Projected Point;public PointF[] Polygon;public RectangleF Bounds;}
        sealed class View
        {
            public bool Valid;public Size Size;public float Scale,X,Y;public int Signature;public readonly List<Glyph> Glyphs=new List<Glyph>();
            public readonly List<Area> Areas=new List<Area>();public readonly Dictionary<long,List<Glyph>> Hits=new Dictionary<long,List<Glyph>>();
            public readonly GraphicsPath HazardPath=new GraphicsPath(FillMode.Winding);
        }
        readonly Dictionary<long,Cell> cells=new Dictionary<long,Cell>();
        readonly List<Projected> hazards=new List<Projected>();
        readonly GraphicsPath regionPath=new GraphicsPath(FillMode.Winding);
        string regionState;long builtRegionRevision=-1;
        public long RegionRevision {get;private set;}
        readonly Dictionary<string,Bitmap> artwork=new Dictionary<string,Bitmap>(StringComparer.Ordinal);
        readonly Bitmap[] badges=new Bitmap[100];
        readonly View main=new View(),mini=new View();
        bool[] enabledTypes=new bool[0];
        LootCatalog catalog;LootPreferences preferences;string mapId,mode;MapDefinition definition;MapLayer layer;AutoNavigator classifier;
        int minCellX,maxCellX,minCellY,maxCellY;
        public int VisibleCount {get{return main.Glyphs.Count;}}
        public int ViewBuildCount {get;private set;}
        public LootRenderer()
        {
            for(int i=2;i<100;i++)
            {
                var b=new Bitmap(17,13,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(b))using(var f=new Font("Segoe UI",8.5f,FontStyle.Bold,GraphicsUnit.Pixel))using(var bg=new SolidBrush(Color.FromArgb(250,40,56,69)))using(var fg=new SolidBrush(Color.FromArgb(241,247,251)))
                {g.SmoothingMode=SmoothingMode.AntiAlias;using(var path=UiTheme.Round(new RectangleF(.5f,.5f,16,12),4))g.FillPath(bg,path);using(var format=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})g.DrawString(i==99?"99+":i.ToString(),f,fg,new RectangleF(0,0,17,13),format);}badges[i]=b;
            }
        }
        static long Key(int x,int y){return ((long)x<<32)^(uint)y;}
        public void Configure(LootCatalog data,string map,string gameMode,LootPreferences options)
        {
            bool changed=catalog!=data || mapId!=map;catalog=data;mapId=map;mode=gameMode;preferences=options;Invalidate();
            string state=preferences==null?"":preferences.Enabled+"|"+preferences.AllFloors+"|"+mode+"|"+String.Join(",",catalog==null?new string[0]:catalog.Types.Where(t=>t.Group=="hazard").Select(t=>preferences.Allows(t)?t.Id:"").ToArray());
            if(state!=regionState){regionState=state;RegionRevision++;}
            if(data!=null)foreach(var type in data.Types){Sprite(Icon(type),false);Sprite(Icon(type),true);}Sprite("mixed",false);Sprite("mixed",true);
            if(changed)BuildIndex();
        }
        void Invalidate(){main.Valid=mini.Valid=false;}
        static string Icon(LootType type){return type.Icon??(type.Category==0?"others":type.Category==6?"safe":"wooden-crate");}
        Bitmap Sprite(string icon,bool small)
        {string key=(small?"s:":"m:")+icon;Bitmap b;if(!artwork.TryGetValue(key,out b))artwork.Add(key,b=LootArtwork.Create(icon,small?20:26));return b;}
        public void Bind(MapDefinition map,MapLayer mapLayer,AutoNavigator navigation)
        {if(definition==map && layer==mapLayer && classifier==navigation)return;definition=map;layer=mapLayer;classifier=navigation;BuildIndex();}
        void BuildIndex()
        {
            RegionRevision++;
            cells.Clear();hazards.Clear();foreach(var v in new[]{main,mini}){v.Glyphs.Clear();v.Areas.Clear();v.Hits.Clear();v.HazardPath.Reset();}Invalidate();
            if(catalog==null || layer==null || definition==null || definition.Id!=mapId)return;
            minCellX=minCellY=Int32.MaxValue;maxCellX=maxCellY=Int32.MinValue;
            foreach(var point in catalog.ForMap(mapId))
            {
                MapPoint pixel=layer.Project(point.X,point.Z);int cx=(int)Math.Floor(pixel.X/128),cy=(int)Math.Floor(pixel.Y/128);Cell cell;
                if(!cells.TryGetValue(Key(cx,cy),out cell)){cell=new Cell {X=cx,Y=cy};cells.Add(Key(cx,cy),cell);}
                var p=new Projected {Source=point,X=(float)pixel.X,Y=(float)pixel.Y,Floor=classifier==null?null:classifier.LevelAt(point.X,point.Y,point.Z)};
                if(point.Outline!=null){p.Outline=point.Outline.Select(v=>{var q=layer.Project(v[0],v[1]);return new PointF((float)q.X,(float)q.Y);}).ToArray();hazards.Add(p);}
                cell.Points.Add(p);minCellX=Math.Min(minCellX,cx);maxCellX=Math.Max(maxCellX,cx);minCellY=Math.Min(minCellY,cy);maxCellY=Math.Max(maxCellY,cy);
            }
        }
        int Signature(bool isMini)
        {
            if(preferences==null)return 0;
            unchecked{int hash=preferences.Categories|(preferences.Enabled?1<<10:0)|(preferences.GroupNearby?1<<11:0)|(preferences.AllFloors?1<<12:0)|(isMini && preferences.ShowOnMiniMap?1<<13:0)|LootCatalog.ModeBit(mode)<<16;foreach(var id in preferences.DisabledTypes??new string[0])hash=hash*31+id.GetHashCode();return hash;}
        }
        bool OnFloor(Projected p){return preferences.AllFloors || p.Floor==null || p.Floor==layer.Id;}
        public void DrawRegions(Graphics graphics,float scale)
        {
            if(builtRegionRevision!=RegionRevision)
            {
                builtRegionRevision=RegionRevision;regionPath.Reset();regionPath.FillMode=FillMode.Winding;
                if(preferences!=null && preferences.Enabled && catalog!=null)
                foreach(var p in hazards)
                {
                    if(!preferences.Allows(catalog.Types[p.Source.Type]) || (p.Source.Modes&LootCatalog.ModeBit(mode))==0 || !OnFloor(p))continue;
                    var polygon=(PointF[])p.Outline.Clone();double winding=0;for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)winding+=(polygon[i].X-polygon[j].X)*(polygon[i].Y+polygon[j].Y);if(winding<0)Array.Reverse(polygon);regionPath.AddPolygon(polygon);
                }
            }
            if(regionPath.PointCount==0)return;
            using(var fill=new SolidBrush(Color.FromArgb(26,255,100,72)))using(var edge=new Pen(Color.FromArgb(150,255,133,101),1.2f/Math.Max(.001f,scale)))
            {edge.DashPattern=new[]{4f,3f};graphics.FillPath(fill,regionPath);graphics.DrawPath(edge,regionPath);}
        }
        public void DrawRegionsScreen(Graphics graphics,float scale,float x,float y)
        {var saved=graphics.Save();try{graphics.TranslateTransform(x,y);graphics.ScaleTransform(scale,scale);DrawRegions(graphics,scale);}finally{graphics.Restore(saved);}}
        string SelectedIcon(LootPoint point)
        {
            if(point.Items==null)return enabledTypes[point.Type]?Icon(catalog.Types[point.Type]):null;
            string icon=null;bool mixed=false;foreach(int i in point.Items){int type=catalog.Items[i].Type;if(!enabledTypes[type])continue;string next=Icon(catalog.Types[type]);if(icon!=null && icon!=next)mixed=true;icon=next;}return mixed?"mixed":icon;
        }
        View Ensure(Size size,float scale,float x,float y,bool isMini)
        {
            View view=isMini?mini:main;int signature=Signature(isMini);
            if(view.Valid && view.Size==size && view.Scale==scale && view.X==x && view.Y==y && view.Signature==signature)return view;
            view.Valid=true;view.Size=size;view.Scale=scale;view.X=x;view.Y=y;view.Signature=signature;view.Glyphs.Clear();view.Areas.Clear();view.Hits.Clear();view.HazardPath.Reset();view.HazardPath.FillMode=FillMode.Winding;ViewBuildCount++;
            if(catalog==null || preferences==null || !preferences.Enabled || (isMini && !preferences.ShowOnMiniMap) || preferences.Categories==0 || cells.Count==0 || scale<=0)return view;
            enabledTypes=catalog.Types.Select(t=>preferences.Allows(t)).ToArray();
            float left=(-36-x)/scale,top=(-36-y)/scale,right=(size.Width+36-x)/scale,bottom=(size.Height+36-y)/scale;
            int a=Math.Max(minCellX,(int)Math.Floor(left/128)),b=Math.Max(minCellY,(int)Math.Floor(top/128)),c=Math.Min(maxCellX,(int)Math.Floor(right/128)),d=Math.Min(maxCellY,(int)Math.Floor(bottom/128));
            var groups=new Dictionary<string,Glyph>(StringComparer.Ordinal);int bit=LootCatalog.ModeBit(mode);float pitch=isMini?29:38;
            foreach(var p in hazards)
            {
                if((p.Source.Modes&bit)==0 || !enabledTypes[p.Source.Type] || !OnFloor(p))continue;
                float minX=p.Outline.Min(v=>v.X),minY=p.Outline.Min(v=>v.Y),maxX=p.Outline.Max(v=>v.X),maxY=p.Outline.Max(v=>v.Y);
                if(maxX<left || minX>right || maxY<top || minY>bottom)continue;
                var polygon=p.Outline.Select(v=>new PointF(x+v.X*scale,y+v.Y*scale)).ToArray();
                // Consistent winding makes overlap a union, never a hole or repeated alpha fill.
                double winding=0;for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++)winding+=(polygon[i].X-polygon[j].X)*(polygon[i].Y+polygon[j].Y);
                if(winding<0)Array.Reverse(polygon);
                view.HazardPath.AddPolygon(polygon);
                view.Areas.Add(new Area {Point=p,Polygon=polygon,Bounds=new RectangleF(x+minX*scale,y+minY*scale,(maxX-minX)*scale,(maxY-minY)*scale)});
            }
            Action<Cell> add=delegate(Cell cell)
            {
                foreach(var p in cell.Points)
                {
                    if(p.X<left || p.X>right || p.Y<top || p.Y>bottom || (p.Source.Modes&bit)==0 || !OnFloor(p))continue;
                    string icon=SelectedIcon(p.Source);if(icon==null)continue;
                    float px=x+p.X*scale,py=y+p.Y*scale;Glyph glyph;
                    if(preferences.GroupNearby)
                    {
                        // Keep hazards and utilities distinct from ordinary loot groups.
                        string section=catalog.Types[p.Source.Type].Group;string key=(section=="hazard" || section=="utility"?icon:"loot")+":"+Key((int)Math.Floor(p.X*scale/pitch),(int)Math.Floor(p.Y*scale/pitch));
                        if(!groups.TryGetValue(key,out glyph)){glyph=new Glyph {Icon=icon};groups.Add(key,glyph);view.Glyphs.Add(glyph);}else if(glyph.Icon!=icon)glyph.Icon="mixed";
                    }
                    else{glyph=new Glyph {Icon=icon};view.Glyphs.Add(glyph);}
                    glyph.X+=px;glyph.Y+=py;glyph.Points.Add(p);
                }
            };
            if(a<=c && b<=d)
            {
                if((long)(c-a+1)*(d-b+1)<=4096){for(int cy=b;cy<=d;cy++)for(int cx=a;cx<=c;cx++){Cell cell;if(cells.TryGetValue(Key(cx,cy),out cell))add(cell);}}
                else foreach(var cell in cells.Values)if(cell.X>=a && cell.X<=c && cell.Y>=b && cell.Y<=d)add(cell);
            }
            foreach(var glyph in view.Glyphs)
            {
                glyph.X/=glyph.Points.Count;glyph.Y/=glyph.Points.Count;
                if(!isMini){long key=Key((int)Math.Floor(glyph.X/32),(int)Math.Floor(glyph.Y/32));List<Glyph> hits;if(!view.Hits.TryGetValue(key,out hits))view.Hits.Add(key,hits=new List<Glyph>());hits.Add(glyph);}
            }
            return view;
        }
        public void Draw(Graphics graphics,Size size,float scale,float x,float y,bool isMini)
        {
            var view=Ensure(size,scale,x,y,isMini);
            if(isMini && view.HazardPath.PointCount>0)
            using(var fill=new SolidBrush(Color.FromArgb(isMini?22:26,255,100,72)))using(var edge=new Pen(Color.FromArgb(150,255,133,101),1.2f))
            {edge.DashPattern=new[]{4f,3f};var smoothing=graphics.SmoothingMode;graphics.SmoothingMode=SmoothingMode.None;graphics.FillPath(fill,view.HazardPath);graphics.DrawPath(edge,view.HazardPath);graphics.SmoothingMode=smoothing;}
            foreach(var glyph in view.Glyphs)
            {
                Bitmap image=Sprite(glyph.Icon,isMini);int px=(int)Math.Round(glyph.X),py=(int)Math.Round(glyph.Y);graphics.DrawImageUnscaled(image,px-image.Width/2,py-image.Height/2);
                if(glyph.Points.Count>1)graphics.DrawImageUnscaled(badges[Math.Min(99,glyph.Points.Count)],px+image.Width/2-9,py-image.Height/2-3);
            }
        }
        public string Tooltip(Size size,float scale,float x,float y,Point point)
        {
            var view=Ensure(size,scale,x,y,false);int cx=(int)Math.Floor(point.X/32f),cy=(int)Math.Floor(point.Y/32f);Glyph nearest=null;float distance=256;
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {List<Glyph> hits;if(!view.Hits.TryGetValue(Key(cx+dx,cy+dy),out hits))continue;foreach(var glyph in hits){float vx=point.X-glyph.X,vy=point.Y-glyph.Y,dist=vx*vx+vy*vy;if(dist<distance){nearest=glyph;distance=dist;}}}
            if(nearest==null)
            {
                foreach(var area in view.Areas)if(area.Bounds.Contains(point) && Inside(area.Polygon,point))return Describe(area.Point.Source);
                return "";
            }
            if(nearest.Points.Count==1)return Describe(nearest.Points[0].Source);
            var descriptions=nearest.Points.Select(p=>Describe(p.Source)).Distinct().Take(6).ToArray();
            return UiText.Format("Grupa: {0} miejsc lootu",nearest.Points.Count)+"\n"+String.Join("\n",descriptions);
        }
        string Describe(LootPoint point)
        {
            if(point.Items!=null)
            {
                var filtered=point.Items.Where(i=>enabledTypes[catalog.Items[i].Type]).Select(i=>catalog.Items[i].DisplayName).Distinct().ToArray();
                return UiText.T("Possible loose loot")+"\n"+String.Join("\n",filtered.Take(8).ToArray())+(filtered.Length>8?"\n"+UiText.Format("+ {0} more",filtered.Length-8):"");
            }
            string text=catalog.Types[point.Type].DisplayName;
            if(point.Key.HasValue)text+="\n"+UiText.T("Required key: ")+catalog.Items[point.Key.Value].DisplayName;
            else if(!String.IsNullOrEmpty(point.KeyName))text+="\n"+UiText.T("Required key: ")+(UiText.Language=="pl" && !String.IsNullOrEmpty(point.LocalKeyName)?point.LocalKeyName:point.KeyName);
            if(point.NeedsPower)text+="\n"+UiText.T("Requires power");
            if(!String.IsNullOrEmpty(point.Detail))text+="\n"+point.Detail;
            return text;
        }
        static bool Inside(PointF[] polygon,Point p)
        {bool inside=false;for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++){var a=polygon[i];var b=polygon[j];if((a.Y>p.Y)!=(b.Y>p.Y) && p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;}return inside;}
        public void Dispose(){regionPath.Dispose();main.HazardPath.Dispose();mini.HazardPath.Dispose();foreach(var b in artwork.Values)b.Dispose();foreach(var b in badges)if(b!=null)b.Dispose();artwork.Clear();cells.Clear();hazards.Clear();}
    }
}
