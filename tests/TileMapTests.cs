// SPDX-License-Identifier: GPL-3.0-only
// Standalone, no EFT and no network. Tiles are synthetic color/line fixtures on disk.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using EftPosition;

static class TileMapTests
{
    static int assertions;
    static void Check(bool ok,string message) { assertions++; if(!ok) throw new Exception("FAIL: "+message); }
    static void Near(double a,double b,string message) { Check(Math.Abs(a-b)<.001,message+" actual="+a+" expected="+b); }
    static MapLayer Layer(int tileSize)
    {
        return new MapLayer { Width=4096,Height=4096,Remote=true,TileSize=tileSize,TileZoom=2,MaxTileZoom=6,
            TileLeft=100,TileTop=-50,TileTemplate="https://assets.tarkov.dev/maps/test-private-fixture/{z}/{x}/{y}.png" };
    }
    static void Tile(MapLayer layer,string cache,int z,int x,int y)
    {
        string path=TileMap.TileFile(layer,cache,z,x,y);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using(Bitmap tile=new Bitmap(256,256,PixelFormat.Format32bppArgb))
        using(Graphics g=Graphics.FromImage(tile))
        {
            g.Clear(Color.CornflowerBlue);
            using(Pen line=new Pen(Color.White,1)) for(int i=0;i<256;i+=8) g.DrawLine(line,i,0,i,255);
            tile.Save(path,ImageFormat.Png);
        }
    }
    static void Prepare(MapLayer layer,string cache,int z,int maxX,int maxY)
    { for(int y=-2;y<maxY;y++) for(int x=-2;x<maxX;x++) Tile(layer,cache,z,x,y); }
    static void DrawUntil(TileMap map,Bitmap image,Graphics g,int expected,float scale,float x,float y)
    {
        DateTime limit=DateTime.UtcNow.AddSeconds(10);
        while(map.CacheCount<expected && DateTime.UtcNow<limit)
        { map.Draw(g,new Rectangle(0,0,image.Width,image.Height),scale,x,y); Thread.Sleep(10); }
        Check(map.CacheCount>=expected,"cached disk tiles decoded");
    }
    static int Main(string[] args)
    {
        try
        {
            string directory=Path.GetFullPath(args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"EftPosition-tile-tests-"+Guid.NewGuid().ToString("N")));
            MapLayer normal=Layer(256),labs=Layer(175);
            Check(TileMap.ChooseZoom(normal,1)==2,"native base zoom");
            Check(TileMap.ChooseZoom(normal,4)==4,"source zoom grows with viewport magnification");
            Check(TileMap.ChooseZoom(normal,200)==6,"source max zoom cap");
            Check(TileMap.ChooseZoom(labs,1.5f)==2,"Labs source texels preserve 256/175 pitch");
            Check(TileMap.ChooseZoom(labs,3)==3,"Labs larger zoom");
            RectangleF rect=TileMap.Destination(normal,4,3,2,2,10,20);
            Near(rect.X,194,"tile left converts base global coordinates");
            Near(rect.Y,376,"tile top supports negative map origin");
            Near(rect.Width,128,"destination width uses source zoom ratio");
            rect=TileMap.Destination(labs,3,2,1,3,20,30);
            Near(rect.X,245,"Labs 175-pixel pitch X"); Near(rect.Y,442.5,"Labs 175-pixel pitch Y");
            Check(TileMap.CacheDirectory(directory,normal.TileTemplate)!=TileMap.CacheDirectory(directory,normal.TileTemplate+"other"),"source/floor cache separation");
            Prepare(normal,directory,4,36,36);
            int dirty=0;
            using(Bitmap image=new Bitmap(1024,768))
            using(Graphics graphics=Graphics.FromImage(image))
            using(TileMap map=new TileMap(normal,directory,delegate { Interlocked.Increment(ref dirty); }))
            {
                graphics.Clear(Color.Magenta);
                DrawUntil(map,image,graphics,12,4,0,0);
                map.Draw(graphics,new Rectangle(0,0,1024,768),4,0,0);
                Check(image.GetPixel(500,400).ToArgb()!=Color.Magenta.ToArgb(),"native detail is overlaid on fallback");
                Check(dirty>0,"worker signals UI dirty flag");
                Check(map.DownloadCount==0,"cached overlay works without internet");
                for(int i=0;i<30;i++)
                {
                    map.Draw(graphics,new Rectangle(0,0,1024,768),4,-i*120,-i*70);
                    Thread.Sleep(20);
                    Check(map.PendingCount<=64,"bounded request queue");
                    Check(map.DecodedBytes<=TileMap.CacheLimitBytes,"bounded decoded image bytes");
                    Check(map.CacheCount<=96,"bounded decoded entry count");
                }
                map.Dispose();
                Check(map.DecodedBytes==0 && map.CacheCount==0,"dispose releases decoded image cache");
                map.Draw(graphics,new Rectangle(0,0,1024,768),4,0,0);
                Check(map.PendingCount==0,"disposed renderer does not request tiles");
            }
            Console.WriteLine("PASS: "+assertions+" high-resolution tile checks; fixtures retained: "+directory);
            return 0;
        }
        catch(Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
