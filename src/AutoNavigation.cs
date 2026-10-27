// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace EftPosition
{
    public sealed class WorldBounds
    {
        public readonly double MinX, MinZ, MaxX, MaxZ;
        public WorldBounds(double minX,double minZ,double maxX,double maxZ)
        { MinX=Math.Min(minX,maxX); MinZ=Math.Min(minZ,maxZ); MaxX=Math.Max(minX,maxX); MaxZ=Math.Max(minZ,maxZ); }
        public double Width { get { return MaxX-MinX; } }
        public double Depth { get { return MaxZ-MinZ; } }
        public double CenterX { get { return (MinX+MaxX)/2; } }
        public double CenterZ { get { return (MinZ+MaxZ)/2; } }
        public bool Contains(double x,double z,double margin)
        { return x>=MinX-margin && x<=MaxX+margin && z>=MinZ-margin && z<=MaxZ+margin; }
        public WorldBounds Union(WorldBounds b)
        { return new WorldBounds(Math.Min(MinX,b.MinX),Math.Min(MinZ,b.MinZ),Math.Max(MaxX,b.MaxX),Math.Max(MaxZ,b.MaxZ)); }
        internal bool Touches(WorldBounds b,double margin)
        { return MinX<=b.MaxX+margin && MaxX>=b.MinX-margin && MinZ<=b.MaxZ+margin && MaxZ>=b.MinZ-margin; }
    }

    public sealed class NavigationDecision
    {
        public string LayerId { get; internal set; }
        public WorldBounds FocusBounds { get; internal set; }
        public string ContextKey { get; internal set; }
        public string ContextName { get; internal set; }
        public bool Changed { get; internal set; }
        public bool IsInterior { get; internal set; }
        public bool ResetView { get; internal set; }
        public bool LayerAvailable { get; internal set; }
        public string SourceLevelName { get; internal set; }
    }

    // Uses only the X/Y/Z in each screenshot and the public tarkov.dev extents.
    // It deliberately does not guess a map, read EFT memory/logs or inspect pixels.
    public sealed class AutoNavigator
    {
        const double HeightMargin=.20, SpatialMargin=.8;
        readonly MapDefinition map;
        readonly List<Band> bands=new List<Band>();
        readonly List<Building> buildings=new List<Building>();
        readonly bool wholeMapInterior;
        Band previousBand;
        Building previousBuilding;
        string lastContext;
        bool lastInterior;
        bool initialized;
        int nextRegion;

        sealed class Region
        {
            internal string Name;
            internal MapPoint[] Points;
            internal WorldBounds Bounds;
            internal int Serial;
            internal bool Contains(double x,double z,double margin)
            {
                if(!Bounds.Contains(x,z,margin)) return false;
                if(Points.Length==2) return true;
                bool inside=false;
                for(int i=0,j=Points.Length-1;i<Points.Length;j=i++)
                {
                    double zi=Points[i].Y,zj=Points[j].Y;
                    if((zi>z)!=(zj>z) && x<(Points[j].X-Points[i].X)*(z-zi)/(zj-zi)+Points[i].X) inside=!inside;
                }
                if(inside) return true;
                if(margin<=0) return false;
                for(int i=0,j=Points.Length-1;i<Points.Length;j=i++)
                {
                    double dx=Points[i].X-Points[j].X,dz=Points[i].Y-Points[j].Y;
                    double length=dx*dx+dz*dz;
                    double along=length==0?0:((x-Points[j].X)*dx+(z-Points[j].Y)*dz)/length;
                    along=Math.Max(0,Math.Min(1,along));
                    double ex=x-(Points[j].X+along*dx),ez=z-(Points[j].Y+along*dz);
                    if(ex*ex+ez*ez<=margin*margin) return true;
                }
                return false;
            }
        }
        sealed class Band
        {
            internal string LayerId,Name;
            internal double Min,Max;
            internal Region Region;
            internal bool Underground;
            internal int Order;
            internal bool Applies(Position p,double heightMargin,double spatialMargin)
            { return p.Y>=Min-heightMargin && p.Y<=Max+heightMargin && (Region==null || Region.Contains(p.X,p.Z,spatialMargin)); }
        }
        sealed class Building
        {
            internal string Name;
            internal int Serial;
            internal WorldBounds Bounds;
            internal List<Region> Parts=new List<Region>();
            internal bool Contains(double x,double z,double margin)
            { foreach(Region r in Parts) if(r.Contains(x,z,margin)) return true; return false; }
        }
        sealed class UpstreamLocation { public string normalizedName {get;set;} public UpstreamMap[] maps {get;set;} }
        sealed class UpstreamMap
        {
            public string key {get;set;} public string projection {get;set;}
            public string tilePath {get;set;} public UpstreamLayer[] layers {get;set;}
        }
        sealed class UpstreamLayer
        {
            public string name {get;set;} public string svgLayer {get;set;}
            public string tilePath {get;set;} public Extent[] extents {get;set;}
        }
        sealed class Extent { public double[] height {get;set;} public object[][] bounds {get;set;} }

        public AutoNavigator(MapDefinition selectedMap,string upstreamJsonPath)
        {
            if(selectedMap==null) throw new ArgumentNullException("selectedMap");
            map=selectedMap;
            wholeMapInterior=map.Id=="factory" || map.Id=="the-lab" || map.Id=="the-labyrinth" || map.Id=="icebreaker";
            UpstreamLocation[] locations=new JavaScriptSerializer().Deserialize<UpstreamLocation[]>(File.ReadAllText(upstreamJsonPath));
            UpstreamMap source=null;
            foreach(UpstreamLocation loc in locations)
                if(loc.maps!=null) foreach(UpstreamMap m in loc.maps)
                    if(m.key==map.Id && m.projection=="interactive") source=m;
            if(source==null) throw new InvalidDataException("Brak metadanych automatycznej nawigacji: "+map.Name);
            int order=0;
            if(source.layers!=null) foreach(UpstreamLayer layer in source.layers)
            {
                string id=FindLayer(layer);
                bool underground=IsUnderground(layer.name);
                if(layer.extents==null) continue;
                foreach(Extent extent in layer.extents)
                {
                    if(extent.height==null || extent.height.Length!=2) continue;
                    double low=Math.Min(extent.height[0],extent.height[1]),high=Math.Max(extent.height[0],extent.height[1]);
                    if(extent.bounds==null || extent.bounds.Length==0)
                        bands.Add(new Band {LayerId=id,Name=layer.name,Min=low,Max=high,Underground=underground,Order=order++});
                    else foreach(object[] raw in extent.bounds)
                    {
                        Region region=ReadRegion(raw);
                        if(region==null) continue;
                        bands.Add(new Band {LayerId=id,Name=layer.name,Min=low,Max=high,Region=region,Underground=underground,Order=order++});
                        // Small named underground building footprints (e.g. Resort wings)
                        // are useful at ground level too. Large tunnel networks are not buildings.
                        string label=region.Name.ToLowerInvariant();
                        bool network=label.Contains("bunker") || label.Contains("tunnel") || label.Contains("underpass") || label=="d2" || label=="garage";
                        if(!underground || (!network && region.Bounds.Width*region.Bounds.Depth<=12000)) AddBuilding(region);
                    }
                }
            }
        }

        string FindLayer(UpstreamLayer source)
        {
            if(!String.IsNullOrEmpty(source.svgLayer))
                foreach(MapLayer layer in map.Layers) if(layer.Id==source.svgLayer) return layer.Id;
            if(!String.IsNullOrEmpty(source.tilePath))
                foreach(MapLayer layer in map.Layers) if(layer.TileTemplate==source.tilePath) return layer.Id;
            return null;
        }
        static bool IsUnderground(string name)
        {
            string n=(name??"").ToLowerInvariant();
            return n.Contains("underground") || n.Contains("tunnel") || n.Contains("bunker") || n.Contains("technical") || n.Contains("garage");
        }
        Region ReadRegion(object[] raw)
        {
            if(raw==null) return null;
            List<MapPoint> points=new List<MapPoint>(); string name="budynek";
            foreach(object value in raw)
            {
                if(value is string) { name=(string)value; continue; }
                IList pair=value as IList;
                if(pair==null || pair.Count<2) continue;
                double x,z;
                try { x=Convert.ToDouble(pair[0]); z=Convert.ToDouble(pair[1]); }
                catch(Exception ex) { if(ex is FormatException || ex is InvalidCastException) continue; throw; }
                if(Double.IsNaN(x)||Double.IsInfinity(x)||Double.IsNaN(z)||Double.IsInfinity(z)) continue;
                points.Add(new MapPoint(x,z));
            }
            if(points.Count<2) return null;
            double minX=points[0].X,maxX=minX,minZ=points[0].Y,maxZ=minZ;
            foreach(MapPoint p in points) {minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);minZ=Math.Min(minZ,p.Y);maxZ=Math.Max(maxZ,p.Y);}
            return new Region {Name=name,Points=points.ToArray(),Bounds=new WorldBounds(minX,minZ,maxX,maxZ),Serial=nextRegion++};
        }
        void AddBuilding(Region region)
        {
            Building joined=null;
            // Merge overlapping footprints for floors of the same building. Do not
            // replace the footprint union with its enclosing rectangle for hit testing.
            for(int i=buildings.Count-1;i>=0;i--)
            {
                Building b=buildings[i]; bool touches=false;
                foreach(Region part in b.Parts) if(part.Bounds.Touches(region.Bounds,.25)) { touches=true; break; }
                if(!touches) continue;
                if(joined==null) joined=new Building {Name=b.Name,Serial=b.Serial,Bounds=b.Bounds};
                joined.Bounds=joined.Bounds.Union(b.Bounds); joined.Parts.AddRange(b.Parts); buildings.RemoveAt(i);
            }
            if(joined==null) joined=new Building {Name=region.Name,Serial=region.Serial,Bounds=region.Bounds};
            joined.Bounds=joined.Bounds.Union(region.Bounds); joined.Parts.Add(region); buildings.Add(joined);
        }

        public void Reset()
        { initialized=false; previousBand=null; previousBuilding=null; lastContext=null; lastInterior=false; }

        static bool Better(Band candidate,Band current)
        {
            if(current==null) return true;
            if((candidate.Region!=null)!=(current.Region!=null)) return candidate.Region!=null;
            if(candidate.Min!=current.Min) return candidate.Min>current.Min;
            if(candidate.Region!=null)
            {
                double a=candidate.Region.Bounds.Width*candidate.Region.Bounds.Depth,b=current.Region.Bounds.Width*current.Region.Bounds.Depth;
                if(a!=b) return a<b;
            }
            return candidate.Order>current.Order;
        }

        // Stateless classification for quest locations; never changes player hysteresis.
        public string LevelAt(double x,double y,double z)
        {
            Position p=new Position("",DateTime.MinValue,x,y,z,0,false,0);
            if(!map.Layers[0].Contains(p))return null;
            Band best=null;
            foreach(Band b in bands)if(b.Applies(p,0,0) && Better(b,best))best=b;
            return best==null?"base":best.LayerId;
        }
        public NavigationDecision Update(Position position,string currentLayerId)
        {
            if(position==null) throw new ArgumentNullException("position");
            bool onMap=map.Layers[0].Contains(position);
            Band best=null;
            if(onMap) foreach(Band b in bands) if(b.Applies(position,0,0) && Better(b,best)) best=b;
            if(initialized && onMap)
            {
                if(previousBand!=null && best!=previousBand && previousBand.Applies(position,HeightMargin,SpatialMargin))
                {
                    // Overlapping height bands use the next floor's entry threshold,
                    // rather than waiting for the previous band's sometimes broad maximum.
                    if(best==null || (best.Min>previousBand.Min && position.Y<best.Min+HeightMargin) ||
                        (best.Min<previousBand.Min && position.Y>previousBand.Min-HeightMargin)) best=previousBand;
                }
                else if(previousBand==null && best!=null)
                {
                    if(best.Underground && position.Y>best.Max-HeightMargin) best=null;
                    else if(!best.Underground && position.Y<best.Min+HeightMargin) best=null;
                }
            }
            Building building=null;
            if(onMap)
            {
                if(previousBuilding!=null && previousBuilding.Contains(position.X,position.Z,SpatialMargin)) building=previousBuilding;
                else foreach(Building b in buildings) if(b.Contains(position.X,position.Z,0))
                { if(building==null || b.Bounds.Width*b.Bounds.Depth<building.Bounds.Width*building.Bounds.Depth) building=b; }
            }
            string desired=best==null || best.LayerId==null ? "base" : best.LayerId;
            bool available=best==null || best.LayerId!=null;
            bool interior=onMap && (best!=null || building!=null || wholeMapInterior);
            WorldBounds focus=null;
            string context=onMap?"outdoor":"outside-map",name=onMap?"Teren otwarty":"Poza obszarem mapy";
            if(interior)
            {
                if(best!=null && best.Underground && best.Region!=null)
                {
                    focus=best.Region.Bounds; name=best.Region.Name; context="underground:"+best.Region.Serial;
                }
                else if(building!=null)
                {
                    focus=building.Bounds; name=building.Name; context="building:"+building.Serial;
                }
                else
                {
                    // A global height band gives a floor but no room outline. Focus a
                    // local neighbourhood once on entry instead of pretending to know it.
                    focus=new WorldBounds(position.X-25,position.Z-25,position.X+25,position.Z+25);
                    name=best==null?map.Name:best.Name; context="indoor";
                }
                context+="/"+(best==null?"base":best.Name)+"/"+desired;
            }
            bool changed=!initialized || context!=lastContext || desired!=currentLayerId;
            NavigationDecision decision=new NavigationDecision {
                LayerId=desired,FocusBounds=focus,ContextKey=context,ContextName=name,Changed=changed,
                IsInterior=interior,ResetView=initialized && lastInterior && !interior && changed,
                LayerAvailable=available,SourceLevelName=best==null?null:best.Name
            };
            previousBand=best; previousBuilding=building; lastContext=context; lastInterior=interior; initialized=true;
            return decision;
        }
    }
}
