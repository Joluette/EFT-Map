// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.IO;
using EftPosition;

static class MapAssetTests
{
    static int Main(string[] args)
    {
        try
        {
            string assets=Path.GetFullPath(args[0]),cache=Path.GetFullPath(args[1]);
            MapDefinition[] maps=MapCatalog.Load(assets);
            int layers=0;
            foreach(MapDefinition m in maps)
            {
              foreach(MapLayer l in args.Length>2 && args[2]=="--all-levels" ? m.Layers : new[]{m.Layers[0]})
              {
                Console.WriteLine(m.Name+" / "+l.Name+" "+l.Width+"x"+l.Height);
                string path=MapAssets.Ensure(assets,cache,l,delegate{return false;},delegate(string s){ });
                using(Bitmap bitmap=new Bitmap(path))
                {
                    if(bitmap.Width!=l.Width || bitmap.Height!=l.Height) throw new Exception("Wrong bitmap dimensions");
                    int nonBackground=0;
                    for(int x=0;x<bitmap.Width && nonBackground==0;x+=3) for(int y=0;y<bitmap.Height && nonBackground==0;y+=3)
                        if(bitmap.GetPixel(x,y).ToArgb()!=Color.FromArgb(24,39,43).ToArgb()) nonBackground++;
                    if(nonBackground==0) throw new Exception("Blank map: "+m.Name+" / "+l.Name);
                }
                layers++;
              }
            }
            Console.WriteLine("PASS: "+layers+" actual map images; dimensions and visible content.");
            return 0;
        }
        catch(Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
