// SPDX-License-Identifier: GPL-3.0-only
// Optional real-network smoke test. Only writes map tiles into the supplied work/cache directory.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using EftPosition;

static class HiresNetworkTests
{
    static int checks;
    static void Check(bool value,string message)
    { checks++; if(!value) throw new Exception("FAIL: "+message); }
    static MapDefinition Find(MapDefinition[] maps,string id)
    { foreach(MapDefinition map in maps) if(map.Id==id) return map; throw new Exception("Missing map "+id); }
    static MapLayer Find(MapDefinition map,string id)
    { foreach(MapLayer layer in map.Layers) if(layer.Id==id) return layer; throw new Exception("Missing layer "+map.Id+"/"+id); }
    static int Content(Bitmap bitmap)
    {
        int result=0,background=Color.Magenta.ToArgb();
        for(int y=0;y<bitmap.Height;y+=8) for(int x=0;x<bitmap.Width;x+=8)
            if(bitmap.GetPixel(x,y).ToArgb()!=background) result++;
        return result;
    }
    static void Run(MapDefinition map,MapLayer layer,string cache,double x,double z)
    {
        const float scale=12;
        MapPoint center=layer.Project(x,z);
        int changed=0;
        Stopwatch watch=Stopwatch.StartNew();
        using(Bitmap viewport=new Bitmap(800,600))
        using(Graphics g=Graphics.FromImage(viewport))
        using(TileMap tiles=new TileMap(layer,cache,delegate { Interlocked.Increment(ref changed); }))
        {
            float offsetX=400-(float)center.X*scale,offsetY=300-(float)center.Y*scale;
            int pixels=0;
            while(watch.Elapsed.TotalSeconds<30)
            {
                g.Clear(Color.Magenta);
                tiles.Draw(g,new Rectangle(0,0,800,600),scale,offsetX,offsetY);
                pixels=Content(viewport);
                Check(tiles.DecodedBytes<=TileMap.CacheLimitBytes,"memory bounded while requesting "+map.Id);
                Check(tiles.PendingCount<=64,"request queue bounded while requesting "+map.Id);
                if(tiles.CacheCount>=4 && pixels>30) break;
                Thread.Sleep(50);
            }
            int zoom=TileMap.ChooseZoom(layer,scale);
            Console.WriteLine(String.Format(CultureInfo.InvariantCulture,
                "{0}/{1}: sourceZoom={2}, baseZoom={3}, decodedTiles={4}, downloaded={5}, bytes={6}, contentSamples={7}, elapsed={8:F2}s",
                map.Id,layer.Id,zoom,layer.TileZoom,tiles.CacheCount,tiles.DownloadCount,tiles.DecodedBytes,pixels,watch.Elapsed.TotalSeconds));
            Check(zoom>layer.TileZoom,"higher source zoom selected "+map.Id);
            Check(zoom<=layer.MaxTileZoom,"source zoom cap "+map.Id);
            Check(tiles.CacheCount>=4,"at least four real native-zoom tiles "+map.Id);
            Check(pixels>30,"nonempty native-zoom viewport "+map.Id);
            Check(changed>0,"dirty signaling "+map.Id);
            Check(tiles.DecodedBytes<=TileMap.CacheLimitBytes,"final decoded cap "+map.Id);
            tiles.Dispose();
            Check(tiles.DecodedBytes==0 && tiles.CacheCount==0,"disposed decoded cache "+map.Id);
        }
    }
    static int Main(string[] args)
    {
        // In particular, do not let a failed background smoke test show a Windows crash dialog.
        AppDomain.CurrentDomain.UnhandledException+=delegate(object sender,UnhandledExceptionEventArgs e)
        { Console.WriteLine("Unhandled smoke-test error: "+e.ExceptionObject); Environment.Exit(2); };
        Thread.CurrentThread.CurrentCulture=CultureInfo.InvariantCulture;
        try
        {
            string assets=Path.GetFullPath(args[0]),cache=Path.GetFullPath(args[1]);
            Check(!cache.StartsWith(Path.GetDirectoryName(assets)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"network fixture cache is outside output package");
            MapDefinition[] maps=MapCatalog.Load(assets);
            int levels=0; foreach(MapDefinition map in maps) levels+=map.Layers.Length;
            Check(maps.Length==13 && levels==55,"metadata contains 13 maps and 55 levels");
            Console.WriteLine("Real-network native-zoom tests; 13 maps / 55 level metadata; no game, no screenshots.");
            Run(Find(maps,"the-lab"),Find(Find(maps,"the-lab"),"base"),cache,-180,-340);
            Run(Find(maps,"the-labyrinth"),Find(Find(maps,"the-labyrinth"),"base"),cache,.5,19.5);
            Run(Find(maps,"icebreaker"),Find(Find(maps,"icebreaker"),"base"),cache,0,22);
            Run(Find(maps,"customs"),Find(Find(maps,"customs"),"tile-2"),cache,262.5,-40.2);
            Run(Find(maps,"reserve"),Find(Find(maps,"reserve"),"tile-0"),cache,-67,-30);
            using(Process process=Process.GetCurrentProcess())
            {
                process.Refresh(); Console.WriteLine("Console process peak working set bytes: "+process.PeakWorkingSet64);
            }
            Console.WriteLine("PASS: "+checks+" checks; real source high-zoom tiles visible for all five tested map/layer combinations.");
            Console.WriteLine("Private test cache retained outside outputs: "+cache);
            return 0;
        }
        catch(Exception ex) { Console.WriteLine(ex.ToString()); return 1; }
    }
}
