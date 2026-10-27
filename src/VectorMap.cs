// SPDX-License-Identifier: GPL-3.0-only
// SharpVectors 1.8.5 is BSD-3-Clause; complete upstream notices are in licenses/.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Xml;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using SharpVectors.Dom.Svg;
using SharpVectors.Renderers;
using SharpVectors.Renderers.Gdi;

namespace EftPosition
{
    // Records SVG geometry once as native vector commands. Zooms replay curves
    // at screen resolution; integer pans reuse pixels and repaint exposed strips.
    public sealed class VectorMap : IDisposable
    {
        SvgDocument document;
        GdiSvgWindow window;
        GdiGraphicsRenderer renderer;
        readonly MapViewport viewportCache;
        Metafile drawing;
        readonly int logicalWidth,logicalHeight;
        public VectorMap(string path,int width,int height)
        {
            if(width<=0 || height<=0) throw new ArgumentOutOfRangeException("width");
            logicalWidth=width; logicalHeight=height;viewportCache=new MapViewport(PaintDrawing);
            renderer=new GdiGraphicsRenderer(true);
            window=new GdiSvgWindow(width,height,renderer);
            renderer.Window=window;
            try
            {
                XmlDocument prepared=new XmlDocument();
                prepared.XmlResolver=null;
                prepared.Load(Path.GetFullPath(path));
                InlineClassStyles(prepared);
                foreach(XmlElement geometry in prepared.GetElementsByTagName("path"))
                    if(geometry.HasAttribute("d")) geometry.SetAttribute("d",SvgPathCanonicalizer.Convert(geometry.GetAttribute("d")));
                document=new SvgDocument(window);
                document.LoadXml(prepared.OuterXml);
                window.Document=document;
                // Prepared SVG dimensions define the same logical rectangle as
                // the existing calibrated PNG. preserveAspectRatio is retained.
                document.RootElement.SetAttribute("width",width.ToString(System.Globalization.CultureInfo.InvariantCulture));
                document.RootElement.SetAttribute("height",height.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch { Dispose(); throw; }
        }
        public int RenderCount {get{return viewportCache.RenderCount;}}
        public int ReusedPanCount {get{return viewportCache.ReusedPanCount;}}
        public long CacheBytes {get{return viewportCache.CacheBytes;}}
        void PrepareDrawing()
        {
            if(drawing!=null) return;
            using(Bitmap reference=new Bitmap(1,1))
            using(Graphics graphics=Graphics.FromImage(reference))
            {
                IntPtr hdc=graphics.GetHdc();
                try { drawing=new Metafile(hdc,new Rectangle(0,0,logicalWidth,logicalHeight),MetafileFrameUnit.Pixel,EmfType.EmfPlusOnly); }
                finally { graphics.ReleaseHdc(hdc); }
            }
            try
            {
                using(GdiGraphics target=GdiGraphics.Create(drawing,true))
                {
                    renderer.GdiGraphics=target;
                    try
                    {
                        target.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
                        target.Graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                        renderer.Render(document);
                    }
                    finally { renderer.GdiGraphics=null; }
                }
            }
            catch { drawing.Dispose(); drawing=null; throw; }
        }
        void PaintDrawing(Graphics target,Size viewport,float scale,float x,float y)
        {
            PrepareDrawing();
            target.SmoothingMode=SmoothingMode.AntiAlias;
            target.PixelOffsetMode=PixelOffsetMode.HighQuality;
            target.TranslateTransform(x,y);
            target.ScaleTransform(scale,scale);
            using(Brush background=new SolidBrush(Color.FromArgb(24,39,43)))
                target.FillRectangle(background,0,0,logicalWidth,logicalHeight);
            target.DrawImage(drawing,new Rectangle(0,0,logicalWidth,logicalHeight),0,0,logicalWidth,logicalHeight,GraphicsUnit.Pixel);
        }
        // The native renderer has an inheritance defect for stylesheet classes.
        // Keep class names for building discovery, but materialize declarations
        // only on their original element; child inheritance must stay intact.
        static void InlineClassStyles(XmlDocument xml)
        {
            var rules=new List<KeyValuePair<string,Dictionary<string,string>>>();
            var styles=new List<XmlElement>();
            foreach(XmlElement style in xml.GetElementsByTagName("style"))
            {
                styles.Add(style);
                foreach(Match rule in Regex.Matches(style.InnerText,@"([^{}]+)\{([^{}]+)\}"))
                    foreach(string selector in rule.Groups[1].Value.Split(','))
                    {
                        string token=selector.Trim();
                        if(Regex.IsMatch(token,@"^\.[\w-]+$",RegexOptions.CultureInvariant))
                            rules.Add(new KeyValuePair<string,Dictionary<string,string>>(token.Substring(1),Declarations(rule.Groups[2].Value)));
                    }
            }
            foreach(XmlElement element in xml.GetElementsByTagName("*"))
            {
                string classes=" "+Regex.Replace(element.GetAttribute("class"),@"\s+"," ")+" ";
                foreach(var rule in rules) if(classes.IndexOf(" "+rule.Key+" ",StringComparison.Ordinal)>=0)
                    foreach(var property in rule.Value) element.SetAttribute(property.Key,property.Value);
                foreach(var property in Declarations(element.GetAttribute("style"))) element.SetAttribute(property.Key,property.Value);
                element.RemoveAttribute("style");
            }
            foreach(XmlElement style in styles) if(style.ParentNode!=null) style.ParentNode.RemoveChild(style);
        }
        static Dictionary<string,string> Declarations(string css)
        {
            var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(string part in css.Split(';'))
            {
                int colon=part.IndexOf(':');
                if(colon>0) result[part.Substring(0,colon).Trim()]=part.Substring(colon+1).Trim();
            }
            return result;
        }

        public void Draw(Graphics graphics,Size viewport,float scale,float offsetX,float offsetY)
        {Draw(graphics,viewport,scale,offsetX,offsetY,100);}
        public void Draw(Graphics graphics,Size viewport,float scale,float offsetX,float offsetY,int opacity)
        {
            if(document==null)throw new ObjectDisposedException("VectorMap");
            viewportCache.Draw(graphics,viewport,scale,offsetX,offsetY,opacity);
        }
        // All returned rectangles are calibrated logical map pixels, independent
        // of the current viewport/zoom; empty/non-rendered groups return Empty.
        public RectangleF GetElementBounds(string id)
        {
            if(document==null || String.IsNullOrEmpty(id)) return RectangleF.Empty;
            SvgElement element=document.GetElementById(id) as SvgElement;
            if(element==null) return RectangleF.Empty;
            return BoundsFor(element);
        }
        RectangleF BoundsFor(SvgElement element)
        {
            try
            {
                ISvgRect b=renderer.GetRenderedBounds(element,0);
                if(b==null || b.Width<=0 || b.Height<=0 || Double.IsNaN(b.X) || Double.IsInfinity(b.X)) return RectangleF.Empty;
                return new RectangleF((float)b.X,(float)b.Y,(float)b.Width,(float)b.Height);
            }
            catch(ArgumentException) { return RectangleF.Empty; }
        }
        public RectangleF[] GetBuildingBounds()
        {
            List<RectangleF> result=new List<RectangleF>();
            if(document!=null) GatherBuildings(document.RootElement as XmlNode,false,result);
            return result.ToArray();
        }
        void GatherBuildings(XmlNode node,bool inherited,List<RectangleF> result)
        {
            XmlElement xml=node as XmlElement;
            if(xml==null || xml.LocalName=="defs" || xml.LocalName=="clipPath") return;
            bool building=inherited;
            string classes=" "+xml.GetAttribute("class").Replace('\t',' ').Replace('\n',' ')+" ";
            if(classes.IndexOf(" building ",StringComparison.Ordinal)>=0) building=true;
            SvgElement svg=node as SvgElement;
            // Leaf geometry, never the giant group combining all buildings.
            if(building && svg!=null && (xml.LocalName=="path" || xml.LocalName=="rect" || xml.LocalName=="polygon"))
            {
                RectangleF rect=BoundsFor(svg);
                if(rect.Width>0 && rect.Height>0 && rect.Width<logicalWidth*.35f && rect.Height<logicalHeight*.35f && result.Count<1024)
                    result.Add(rect);
            }
            foreach(XmlNode child in node.ChildNodes) if(child is XmlElement) GatherBuildings(child,building,result);
        }
        public void Dispose()
        {
            viewportCache.Dispose();
            if(drawing!=null) { drawing.Dispose(); drawing=null; }
            if(renderer!=null) { renderer.Dispose(); renderer=null; }
            if(window!=null) { window.Document=null; window=null; }
            if(document!=null) { document.RemoveAll(); document=null; }
        }
    }
}
