// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace EftPosition
{
    public sealed class MapLabel
    { public string Text { get; set; } public double X { get; set; } public double Z { get; set; } }

    public sealed class MapLayer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Image { get; set; }
        public string VectorImage { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double[] Affine { get; set; }
        public bool Remote { get; set; }
        public string TileTemplate { get; set; }
        public int TileSize { get; set; }
        public int TileZoom { get; set; }
        public int TileLeft { get; set; }
        public int TileTop { get; set; }
        public int MaxTileZoom { get; set; }
        public override string ToString() { return Name; }
        public MapPoint Project(double x,double z)
        { return new MapPoint(Affine[0]*x+Affine[1]*z+Affine[4],Affine[2]*x+Affine[3]*z+Affine[5]); }
        public MapPoint Unproject(double x,double y)
        {
            double det=Affine[0]*Affine[3]-Affine[1]*Affine[2]; x-=Affine[4]; y-=Affine[5];
            return new MapPoint((Affine[3]*x-Affine[1]*y)/det,(-Affine[2]*x+Affine[0]*y)/det);
        }
        public bool Contains(Position p)
        {
            MapPoint v=Project(p.X,p.Z);
            return v.X>=-.001 && v.Y>=-.001 && v.X<=Width+.001 && v.Y<=Height+.001;
        }
        public MapPoint Direction(double yaw)
        {
            double a=yaw*Math.PI/180, sx=Math.Sin(a),sz=Math.Cos(a);
            double x=Affine[0]*sx+Affine[1]*sz,y=Affine[2]*sx+Affine[3]*sz,n=Math.Sqrt(x*x+y*y);
            return new MapPoint(x/n,y/n);
        }
    }
    public sealed class MapDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string AuthorLink { get; set; }
        public string License { get; set; }
        public double Rotation { get; set; }
        public double[][] Bounds { get; set; }
        public MapLayer[] Layers { get; set; }
        public MapLabel[] Labels { get; set; }
        public override string ToString() { return Name; }
    }
    public static class MapCatalog
    {
        public static MapDefinition[] Load(string assets)
        {
            string json=File.ReadAllText(Path.Combine(assets,"maps.json"));
            MapDefinition[] maps=new JavaScriptSerializer().Deserialize<MapDefinition[]>(json);
            if(maps==null || maps.Length==0 || maps.Length>64) throw new InvalidDataException("Brak poprawnego spisu map.");
            foreach(MapDefinition m in maps)
            {
                if(m.Layers==null || m.Layers.Length==0 || m.Layers.Length>32) throw new InvalidDataException("Nieprawidłowe poziomy mapy.");
                foreach(MapLayer l in m.Layers)
                {
                    if(l.Width<1 || l.Height<1 || l.Width>4096 || l.Height>4096 || l.Affine==null || l.Affine.Length!=6)
                        throw new InvalidDataException("Nieprawidłowa kalibracja mapy: "+m.Name);
                    foreach(double v in l.Affine) if(Double.IsNaN(v)||Double.IsInfinity(v)) throw new InvalidDataException("Niepoprawna macierz mapy.");
                    if(Math.Abs(l.Affine[0]*l.Affine[3]-l.Affine[1]*l.Affine[2])<1e-10) throw new InvalidDataException("Osobliwa macierz mapy.");
                    if(l.Remote && (!l.TileTemplate.StartsWith("https://assets.tarkov.dev/maps/",StringComparison.Ordinal) || l.TileSize<1 || l.TileSize>512))
                        throw new InvalidDataException("Nieprawidłowe źródło mapy.");
                }
            }
            return maps;
        }
    }
}
