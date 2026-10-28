// SPDX-License-Identifier: GPL-3.0-only
// Viewport-only high-resolution overlay for the three public tile map sources.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace EftPosition
{
    public sealed class TileMap : IDisposable
    {
        public const long CacheLimitBytes = 24L * 1024 * 1024;

        const int CacheLimitEntries = 96;
        const int PendingLimit = 64;
        readonly object gate = new object();
        readonly MapLayer layer;
        readonly string directory;
        readonly Action invalidate;
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly Thread worker;
        readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
        readonly Dictionary<string, DateTime> failures = new Dictionary<string, DateTime>();
        readonly Queue<Request> pending = new Queue<Request>();
        HashSet<string> wanted = new HashSet<string>();
        string inFlight;
        HttpWebRequest activeRequest;
        bool disposed;
        long bytes, clock, downloads;




        sealed class Entry { public Bitmap Image; public long Bytes, Used; }
        sealed class Request
        {
            public int Z, X, Y;
            public string Key;
            public double Priority;
        }

        public long DecodedBytes { get { lock(gate) return bytes; } }
        public int CacheCount { get { lock(gate) return cache.Count; } }
        public int PendingCount { get { lock(gate) return pending.Count; } }
        public long DownloadCount { get { lock(gate) return downloads; } }


        public TileMap(MapLayer selectedLayer, string cacheDirectory, Action changed)
        {
            if(selectedLayer == null || !selectedLayer.Remote || selectedLayer.TileSize < 1)
                throw new ArgumentException("TileMap requires a remote map layer.");
            if(!selectedLayer.TileTemplate.StartsWith("https://assets.tarkov.dev/maps/", StringComparison.Ordinal))
                throw new ArgumentException("Unknown tile server.");
            layer = selectedLayer;
            directory = CacheDirectory(cacheDirectory, layer.TileTemplate);
            invalidate = changed;
            worker = new Thread(Work) { IsBackground = true, Name = "EFT high resolution map tiles" };
            worker.Start();
        }

        // A nearest source zoom avoids excessive over-fetch while keeping source texels close to screen pixels.
        // Labs' 256px images are displayed in 175px cells; TileSize is a pitch, not decoded image width.
        public static int ChooseZoom(MapLayer selected, float scale)
        {
            if(scale <= 0 || Single.IsNaN(scale) || Single.IsInfinity(scale)) return selected.TileZoom;
            double relative = Math.Max(1, scale * selected.TileSize / 256.0);
            int extra = (int)Math.Floor(Math.Log(relative, 2) + .5);
            int maximum = selected.MaxTileZoom > 0 ? selected.MaxTileZoom : 6;
            return Math.Max(selected.TileZoom, Math.Min(maximum, selected.TileZoom + extra));
        }

        public static RectangleF Destination(MapLayer selected, int zoom, int x, int y, float scale, float offsetX, float offsetY)
        {
            double ratio = Math.Pow(2, zoom - selected.TileZoom);
            return new RectangleF(
                (float)(offsetX + (x * selected.TileSize / ratio - selected.TileLeft) * scale),
                (float)(offsetY + (y * selected.TileSize / ratio - selected.TileTop) * scale),
                (float)(selected.TileSize / ratio * scale), (float)(selected.TileSize / ratio * scale));
        }

        public static string CacheDirectory(string root, string template)
        {
            byte[] digest;
            using(SHA256 sha = SHA256.Create()) digest = sha.ComputeHash(Encoding.UTF8.GetBytes(template));
            string id = BitConverter.ToString(digest).Replace("-", "").Substring(0,24);
            return Path.Combine(Path.GetFullPath(root), "tiles-v3", id);
        }

        public static string TileFile(MapLayer selected, string root, int zoom, int x, int y)
        { return Path.Combine(CacheDirectory(root, selected.TileTemplate), Key(zoom,x,y) + ".png"); }

        static string Key(int z,int x,int y)
        { return z.ToString(CultureInfo.InvariantCulture)+"_"+x.ToString(CultureInfo.InvariantCulture)+"_"+y.ToString(CultureInfo.InvariantCulture); }

        List<Request> Visible(Rectangle client,float scale,float offsetX,float offsetY,int zoom)
        {
            List<Request> result = new List<Request>();
            double l = Math.Max(0,(client.Left-offsetX)/scale), t = Math.Max(0,(client.Top-offsetY)/scale);
            double r = Math.Min(layer.Width,(client.Right-offsetX)/scale), b = Math.Min(layer.Height,(client.Bottom-offsetY)/scale);
            if(r <= l || b <= t) return result;
            double ratio = Math.Pow(2,zoom-layer.TileZoom);
            int firstX=(int)Math.Floor((layer.TileLeft+l)*ratio/layer.TileSize);
            int firstY=(int)Math.Floor((layer.TileTop+t)*ratio/layer.TileSize);
            int lastX=(int)Math.Floor(((layer.TileLeft+r)*ratio-.001)/layer.TileSize);
            int lastY=(int)Math.Floor(((layer.TileTop+b)*ratio-.001)/layer.TileSize);
            if((long)(lastX-firstX+1)*(lastY-firstY+1)>4096) return result;
            double centerX=(firstX+lastX)/2.0, centerY=(firstY+lastY)/2.0;
            for(int y=firstY;y<=lastY;y++) for(int x=firstX;x<=lastX;x++)
                result.Add(new Request { Z=zoom,X=x,Y=y,Key=Key(zoom,x,y),Priority=(x-centerX)*(x-centerX)+(y-centerY)*(y-centerY) });
            return result;
        }

        // Call after painting the existing base PNG. Missing/unavailable detail never hides that fallback.
        // The callback is invoked on the worker; root should just set a dirty flag and invalidate on its UI timer.
        public void Draw(Graphics graphics, Rectangle client, float scale, float offsetX, float offsetY)
        {Draw(graphics,client,scale,offsetX,offsetY,false);}
        public void Draw(Graphics graphics, Rectangle client, float scale, float offsetX, float offsetY,bool preview)
        {
            if(scale <= 1 || Single.IsNaN(scale) || Single.IsInfinity(scale)) { ClearPending(); return; }
            int zoom=ChooseZoom(layer,scale);
            List<Request> visible=Visible(client,scale,offsetX,offsetY,zoom);
            // In a very large window, prefer one coarser zoom rather than repeatedly evicting visible tiles.
            // 80 typical 256px tiles = 20MB, leaving room within the hard 24MB decoded cache cap.
            while(visible.Count>80 && zoom>layer.TileZoom)
            { zoom--; visible=Visible(client,scale,offsetX,offsetY,zoom); }
            if(visible.Count>80) { ClearPending(); return; }
            visible.Sort(delegate(Request a,Request b) { return a.Priority.CompareTo(b.Priority); });
            bool queued=false;
            lock(gate)
            {
                if(disposed) return;
                wanted.Clear();
                foreach(Request request in visible) wanted.Add(request.Key);
                pending.Clear();
                DateTime now=DateTime.UtcNow;
                GraphicsState old=graphics.Save();
                try
                {
                    graphics.SetClip(client,CombineMode.Intersect);
                    graphics.SetClip(new RectangleF(offsetX,offsetY,layer.Width*scale,layer.Height*scale),CombineMode.Intersect);
                    graphics.InterpolationMode=preview?InterpolationMode.NearestNeighbor:InterpolationMode.HighQualityBilinear;
                    graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    foreach(Request request in visible)
                    {
                        Entry item;
                        if(cache.TryGetValue(request.Key,out item))
                        {
                            item.Used=++clock;
                        }
                        else if(pending.Count<PendingLimit && inFlight!=request.Key)
                        {
                            DateTime retry;
                            if(!failures.TryGetValue(request.Key,out retry) || retry<=now)
                            { pending.Enqueue(request); queued=true; }
                        }
                    }
                    foreach(Request request in visible)
                    {Entry item;if(cache.TryGetValue(request.Key,out item))graphics.DrawImage(item.Image,Destination(layer,request.Z,request.X,request.Y,scale,offsetX,offsetY));}
                }
                finally { graphics.Restore(old); }
            }
            if(queued) Signal();
        }

        void ClearPending()
        { lock(gate) { if(disposed) return; wanted.Clear(); pending.Clear(); } }
        // Secondary previews may reuse decoded details, but never replace the main download queue.
        public void DrawCached(Graphics graphics,Rectangle client,float scale,float offsetX,float offsetY)
        {
            if(scale<=1 || Single.IsNaN(scale) || Single.IsInfinity(scale))return;
            int zoom=ChooseZoom(layer,scale);
            List<Request> visible=Visible(client,scale,offsetX,offsetY,zoom);
            while(visible.Count>80 && zoom>layer.TileZoom){zoom--;visible=Visible(client,scale,offsetX,offsetY,zoom);}
            if(visible.Count>80)return;
            lock(gate)
            {
                if(disposed)return;
                GraphicsState old=graphics.Save();
                try
                {
                    graphics.SetClip(client,CombineMode.Intersect);
                    graphics.SetClip(new RectangleF(offsetX,offsetY,layer.Width*scale,layer.Height*scale),CombineMode.Intersect);
                    graphics.InterpolationMode=InterpolationMode.HighQualityBilinear;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    foreach(Request request in visible)
                    {Entry item;if(cache.TryGetValue(request.Key,out item))graphics.DrawImage(item.Image,Destination(layer,request.Z,request.X,request.Y,scale,offsetX,offsetY));}
                }
                finally{graphics.Restore(old);}
            }
        }

        void Signal() { try { wake.Set(); } catch(ObjectDisposedException) { } }

        void Work()
        {
            try
            {
                while(true)
                {
                    Request request=null;
                    lock(gate)
                    {
                        if(disposed) return;
                        if(pending.Count>0) { request=pending.Dequeue(); inFlight=request.Key; }
                    }
                    if(request==null) { wake.WaitOne(); continue; }
                    Bitmap image=null;
                    try
                    {
                        image=ReadTile(request);
                        lock(gate)
                        {
                            if(disposed || !wanted.Contains(request.Key)) { image.Dispose(); image=null; }
                            else
                            {
                                long imageBytes=(long)image.Width*image.Height*4;
                                if(imageBytes<=CacheLimitBytes)
                                {
                                    while((bytes+imageBytes>CacheLimitBytes || cache.Count>=CacheLimitEntries) && cache.Count>0) EvictOne();
                                    Entry old;
                                    if(cache.TryGetValue(request.Key,out old)) { old.Image.Dispose(); bytes-=old.Bytes; }
                                    cache[request.Key]=new Entry { Image=image,Bytes=imageBytes,Used=++clock };
                                    bytes+=imageBytes; image=null;
                                }
                                if(image!=null) { image.Dispose(); image=null; }
                            }
                        }
                        Changed();
                    }
                    catch(Exception ex)
                    {
                        if(image!=null) image.Dispose();
                        if(!(ex is IOException) && !(ex is WebException) && !(ex is ArgumentException) &&
                           !(ex is System.Runtime.InteropServices.ExternalException) && !(ex is OperationCanceledException))
                        { /* Failed detail must never take down the map viewer. */ }
                        lock(gate)
                        {
                            if(!disposed)
                            {
                                if(failures.Count>=128)
                                {
                                    string remove=null;
                                    foreach(string key in failures.Keys) { remove=key; break; }
                                    if(remove!=null) failures.Remove(remove);
                                }
                                failures[request.Key]=DateTime.UtcNow.AddMinutes(2);
                            }
                        }
                    }
                    finally { lock(gate) { inFlight=null; activeRequest=null; } }
                }
            }
            finally { wake.Dispose(); }
        }

        Bitmap Decode(byte[] data)
        {
            using(MemoryStream stream=new MemoryStream(data,false))
            using(Image source=Image.FromStream(stream))
            {
                if(source.Width<1 || source.Height<1 || source.Width>1024 || source.Height>1024)
                    throw new InvalidDataException("Niepoprawny kafelek mapy.");
                Bitmap result=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppPArgb);
                try{using(Graphics g=Graphics.FromImage(result)){g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(source,0,0);}return result;}
                catch{result.Dispose();throw;}
            }
        }

        Bitmap ReadTile(Request tile)
        {
            string path=Path.Combine(directory,tile.Key+".png");
            if(File.Exists(path))
            {
                try
                {
                    if(new FileInfo(path).Length>4*1024*1024) throw new InvalidDataException("Tile is too large.");
                    return Decode(File.ReadAllBytes(path));
                }
                catch(Exception ex)
                {
                    if(!(ex is IOException) && !(ex is ArgumentException) && !(ex is System.Runtime.InteropServices.ExternalException)) throw;
                    // Preserve a damaged cached tile for inspection. Never operates in a screenshot folder.
                    try { File.Move(path,path+".bad-"+Guid.NewGuid().ToString("N")); } catch(IOException) { }
                }
            }
            string url=layer.TileTemplate.Replace("{z}",tile.Z.ToString(CultureInfo.InvariantCulture))
                .Replace("{x}",tile.X.ToString(CultureInfo.InvariantCulture)).Replace("{y}",tile.Y.ToString(CultureInfo.InvariantCulture));
            HttpWebRequest request=(HttpWebRequest)WebRequest.Create(url);
            request.Timeout=5000; request.ReadWriteTimeout=5000;
            request.UserAgent="EftPosition/3.0 (personal map viewer)";
            request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            lock(gate)
            {
                if(disposed) throw new OperationCanceledException();
                activeRequest=request;
            }
            byte[] data;
            using(WebResponse response=request.GetResponse())
            using(Stream input=response.GetResponseStream())
            using(MemoryStream output=new MemoryStream())
            {
                byte[] buffer=new byte[16384]; int count;
                while((count=input.Read(buffer,0,buffer.Length))>0)
                {
                    lock(gate) if(disposed) throw new OperationCanceledException();
                    if(output.Length+count>4*1024*1024) throw new InvalidDataException("Tile is too large.");
                    output.Write(buffer,0,count);
                }
                data=output.ToArray();
            }
            Bitmap decoded=Decode(data);
            try
            {
                lock(gate) if(disposed) { decoded.Dispose(); throw new OperationCanceledException(); }
                Directory.CreateDirectory(directory);
                string temporary=path+"."+Guid.NewGuid().ToString("N")+".part";
                try
                {
                    File.WriteAllBytes(temporary,data);
                    if(!File.Exists(path)) File.Move(temporary,path);
                }
                finally { if(File.Exists(temporary)) File.Delete(temporary); }
                lock(gate) downloads++;
                return decoded;
            }
            catch { decoded.Dispose(); throw; }
        }

        void EvictOne()
        {
            string key=null; long least=Int64.MaxValue;
            foreach(KeyValuePair<string,Entry> pair in cache)
                if(!wanted.Contains(pair.Key) && pair.Value.Used<least) { key=pair.Key; least=pair.Value.Used; }
            if(key==null) foreach(KeyValuePair<string,Entry> pair in cache)
                if(pair.Value.Used<least) { key=pair.Key; least=pair.Value.Used; }
            if(key!=null) { Entry entry=cache[key]; entry.Image.Dispose(); bytes-=entry.Bytes; cache.Remove(key); }
        }

        void Changed()
        {
            lock(gate) if(disposed) return;
            try { if(invalidate!=null) invalidate(); } catch(ObjectDisposedException) { } catch(InvalidOperationException) { }
        }

        public void Dispose()
        {
            HttpWebRequest abort;
            lock(gate)
            {
                if(disposed) return;
                disposed=true; abort=activeRequest;
                pending.Clear(); wanted.Clear(); failures.Clear();
                foreach(Entry entry in cache.Values) entry.Image.Dispose();
                cache.Clear(); bytes=0;

            }
            if(abort!=null) try { abort.Abort(); } catch(WebException) { }
            Signal();
        }
    }
}
