// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net;

namespace EftPosition
{
    public static class MapAssets
    {
        static string Within(string directory,string name)
        {
            string root=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string path=Path.GetFullPath(Path.Combine(root,name.Replace('/',Path.DirectorySeparatorChar)));
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ścieżka mapy wykracza poza jej folder.");
            return path;
        }
        public static string CachedPath(string assets,string cache,MapLayer layer)
        { return Within(layer.Remote?cache:assets,layer.Image); }

        public static string Ensure(string assets,string cache,MapLayer layer,Func<bool> cancelled,Action<string> progress)
        {
            string target=CachedPath(assets,cache,layer);
            if(File.Exists(target)) return target;
            if(!layer.Remote) throw new FileNotFoundException("Brakuje obrazu mapy. Rozpakuj całą paczkę.",target);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            int size=layer.TileSize,firstX=(int)Math.Floor((double)layer.TileLeft/size),firstY=(int)Math.Floor((double)layer.TileTop/size);
            int lastX=(int)Math.Floor((double)(layer.TileLeft+layer.Width-1)/size),lastY=(int)Math.Floor((double)(layer.TileTop+layer.Height-1)/size);
            int total=(lastX-firstX+1)*(lastY-firstY+1),done=0,found=0;
            if(total>400) throw new InvalidDataException("Mapa wymaga zbyt wielu kafelków.");
            string temporary=target+"."+Guid.NewGuid().ToString("N")+".part";
            try
            {
                // One output bitmap; decode and dispose one tile at a time.
                using(Bitmap image=new Bitmap(layer.Width,layer.Height,PixelFormat.Format32bppArgb))
                using(Graphics g=Graphics.FromImage(image))
                {
                    g.Clear(Color.FromArgb(24,39,43));
                    for(int y=firstY;y<=lastY;y++) for(int x=firstX;x<=lastX;x++)
                    {
                        if(cancelled()) throw new OperationCanceledException();
                        progress("Pobieranie mapy: "+(++done)+" / "+total);
                        string url=layer.TileTemplate.Replace("{z}",layer.TileZoom.ToString(CultureInfo.InvariantCulture))
                            .Replace("{x}",x.ToString(CultureInfo.InvariantCulture)).Replace("{y}",y.ToString(CultureInfo.InvariantCulture));
                        HttpWebRequest request=(HttpWebRequest)WebRequest.Create(url);
                        request.UserAgent="EftPosition/2.0 (personal map viewer)"; request.Timeout=8000; request.ReadWriteTimeout=8000;
                        request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
                        try
                        {
                            using(WebResponse response=request.GetResponse())
                            using(Stream input=response.GetResponseStream())
                            using(MemoryStream bytes=new MemoryStream())
                            {
                                byte[] buffer=new byte[16384]; int n;
                                while((n=input.Read(buffer,0,buffer.Length))>0)
                                {
                                    if(cancelled()) throw new OperationCanceledException();
                                    if(bytes.Length+n>4*1024*1024) throw new InvalidDataException("Kafelek mapy jest zbyt duży.");
                                    bytes.Write(buffer,0,n);
                                }
                                bytes.Position=0;
                                using(Image tile=Image.FromStream(bytes))
                                {
                                    // Leaflet's tileSize is the display pitch. Labs serves 256 px images in 175 px cells.
                                    if(tile.Width<1 || tile.Height<1 || tile.Width>1024 || tile.Height>1024)
                                        throw new InvalidDataException("Niepoprawny rozmiar kafelka mapy.");
                                    g.DrawImage(tile,new Rectangle(x*size-layer.TileLeft,y*size-layer.TileTop,size,size)); found++;
                                }
                            }
                        }
                        catch(WebException ex)
                        {
                            HttpWebResponse response=ex.Response as HttpWebResponse;
                            bool blank=response!=null && response.StatusCode==HttpStatusCode.NotFound;
                            if(response!=null) response.Dispose();
                            if(!blank) throw;
                        }
                    }
                    if(found==0) throw new IOException("Źródło nie zwróciło obrazu mapy.");
                    if(cancelled()) throw new OperationCanceledException();
                    image.Save(temporary,ImageFormat.Png);
                }
                if(cancelled()) throw new OperationCanceledException();
                File.Move(temporary,target);
                return target;
            }
            finally { if(File.Exists(temporary)) File.Delete(temporary); } // Only our incomplete cache, never screenshots.
        }
    }
}
