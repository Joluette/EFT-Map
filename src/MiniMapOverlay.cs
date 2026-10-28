// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EftPosition
{
    public sealed class MiniMapOverlay : Form
    {
        readonly MapCanvas source;
        readonly AppPreferences preferences;
        Screen fallbackScreen;
        Rectangle appliedScreen;
        Bitmap snapshot;
        long revision=-1;
        int zoom=-1;
        bool cachedStale,editing,dragging,resizing;
        string cachedLanguage,ageText="";
        DateTime? screenshotTime;
        Rectangle initialBounds;
        Point initialMouse;
        public event EventHandler ConfigurationChanged,EditingChanged;
        public bool IsEditing {get{return editing;}}
        public long CacheBytes {get{return snapshot==null?0:(long)snapshot.Width*snapshot.Height*4;}}
        public int SnapshotCount {get;private set;}
        int L(int value){return Math.Max(1,(int)Math.Round(value*DeviceDpi/96.0));}
        Rectangle MapArea {get{return new Rectangle(L(8),L(40),Math.Max(1,ClientSize.Width-L(16)),Math.Max(1,ClientSize.Height-L(82)));}}
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongW")]static extern int SetWindowLong(IntPtr window,int index,int value);
        [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        public MiniMapOverlay(MapCanvas canvas,AppPreferences settings)
        {
            source=canvas;preferences=settings;Text="EFT Position · "+UiText.T("Minimapa");
            FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;ShowInTaskbar=false;
            AutoScaleMode=AutoScaleMode.None;TopMost=true;BackColor=UiTheme.Background;Font=UiTheme.Body;
            SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            AccessibleName=UiText.T("Minimapa nad grą");
        }
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams
        {
            get
            {
                var p=base.CreateParams;
                p.ExStyle|=0x00080000|0x00000080|0x08000000|0x00000008; // LAYERED, TOOLWINDOW, NOACTIVATE, TOPMOST
                if(!editing)p.ExStyle|=0x00000020;
                return p;
            }
        }
        public static Rectangle Placement(Rectangle screen,Size size,string corner,bool custom,int x,int y)
        {
            int margin=Math.Min(16,Math.Max(0,Math.Min(screen.Width,screen.Height)/20));
            int width=Math.Max(1,Math.Min(size.Width,screen.Width-margin*2)),height=Math.Max(1,Math.Min(size.Height,screen.Height-margin*2));
            int left=custom?x:corner.EndsWith("right",StringComparison.Ordinal)?screen.Right-width-margin:screen.Left+margin;
            int top=custom?y:corner.StartsWith("bottom",StringComparison.Ordinal)?screen.Bottom-height-margin:screen.Top+margin;
            return new Rectangle(Math.Max(screen.Left+margin,Math.Min(left,screen.Right-width-margin)),Math.Max(screen.Top+margin,Math.Min(top,screen.Bottom-height-margin)),width,height);
        }
        Screen SelectedScreen()
        {
            foreach(Screen screen in Screen.AllScreens)if(screen.DeviceName==preferences.OverlayMonitor)return screen;
            if(fallbackScreen!=null)foreach(Screen screen in Screen.AllScreens)if(screen.DeviceName==fallbackScreen.DeviceName)return screen;
            return Screen.PrimaryScreen;
        }
        public void ApplySettings(Screen fallback)
        {
            if(IsDisposed)return;
            fallbackScreen=fallback;preferences.Normalize();
            appliedScreen=SelectedScreen().Bounds;int width=Math.Min(1040,L(preferences.OverlaySize));
            Rectangle bounds=Placement(appliedScreen,new Size(width,width+L(82)),preferences.OverlayCorner,preferences.OverlayCustomPosition,preferences.OverlayX,preferences.OverlayY);
            if(!dragging && !resizing && Bounds!=bounds)Bounds=bounds;
            if(!preferences.OverlayCustomPosition){preferences.OverlayX=Left;preferences.OverlayY=Top;}
            Opacity=editing?1:preferences.OverlayOpacity/100.0;
            UpdateShape();
            if(preferences.OverlayEnabled){if(!Visible)Show();Pulse(screenshotTime);}
            else{SetEditing(false);if(Visible)Hide();ClearSnapshot();}
            Invalidate();
        }
        void UpdateShape()
        {
            Region next=null;
            if(preferences.RoundedCorners && Width>1 && Height>1)
                using(var path=UiTheme.Round(new RectangleF(0,0,Width,Height),L(12)))next=new Region(path);
            Region previous=Region;Region=next;if(previous!=null)previous.Dispose();
        }
        public void SetEditing(bool value)
        {
            if(value==editing)return;
            editing=value;Opacity=editing?1:preferences.OverlayOpacity/100.0;
            if(IsHandleCreated)
            {
                int style=GetWindowLong(Handle,-20);
                SetWindowLong(Handle,-20,editing?style&~0x20:style|0x20);
                SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x0033);
            }
            Cursor=editing?Cursors.SizeAll:Cursors.Default;Invalidate();
            if(EditingChanged!=null)EditingChanged(this,EventArgs.Empty);
        }
        public void RefreshLanguage()
        {Text="EFT Position · "+UiText.T("Minimapa");AccessibleName=UiText.T("Minimapa nad grą");cachedLanguage=null;Invalidate();}
        public void Pulse(DateTime? latestScreenshot)
        {
            screenshotTime=latestScreenshot;
            if(!Visible || IsDisposed)return;
            if(!dragging && !resizing && SelectedScreen().Bounds!=appliedScreen){ApplySettings(Screen.FromControl(source));return;}
            string nextAge;
            if(!source.MiniMapHasPosition)nextAge=source.Layer==null?UiText.T("Wczytywanie mapy…"):source.Position==null?UiText.T("Oczekiwanie na zrzut"):UiText.T("Poza obszarem mapy");
            else if(latestScreenshot.HasValue)
            {
                double age=Math.Max(0,(DateTime.UtcNow-latestScreenshot.Value).TotalSeconds);
                nextAge=UiText.T("Zrzut")+" · "+(age<60?((int)age)+UiText.T(" s temu"):age<3600?((int)(age/60))+UiText.T(" min temu"):((int)(age/3600))+UiText.T(" h temu"));
            }
            else nextAge=UiText.T("Ostatnia pozycja ze zrzutu");
            bool dirty=revision!=source.SceneRevision || zoom!=preferences.OverlayZoom || cachedStale!=source.Stale || cachedLanguage!=UiText.Language;
            if(dirty || nextAge!=ageText){ageText=nextAge;Invalidate();}
        }
        void ClearSnapshot(){if(snapshot!=null){snapshot.Dispose();snapshot=null;}revision=-1;}
        void EnsureSnapshot()
        {
            Rectangle area=MapArea;
            if(snapshot!=null && snapshot.Size==area.Size && revision==source.SceneRevision && zoom==preferences.OverlayZoom && cachedStale==source.Stale && cachedLanguage==UiText.Language)return;
            Bitmap next=source.RenderMiniMap(area.Size,preferences.OverlayZoom);
            ClearSnapshot();snapshot=next;revision=source.SceneRevision;zoom=preferences.OverlayZoom;cachedStale=source.Stale;cachedLanguage=UiText.Language;SnapshotCount++;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);if(source==null)return;
            EnsureSnapshot();Graphics g=e.Graphics;g.Clear(UiTheme.Background);
            Rectangle area=MapArea;g.DrawImageUnscaled(snapshot,area.Location);
            using(var border=new Pen(editing?Color.FromArgb(255,197,107):UiTheme.Accent,L(editing?2:1)))
                g.DrawRectangle(border,0,0,ClientSize.Width-1,ClientSize.Height-1);
            string title=editing?UiText.T("USTAWIANIE · przeciągnij"):source.Definition==null?UiText.T("Minimapa"):source.Definition.Name;
            TextRenderer.DrawText(g,title,UiTheme.Body,new Rectangle(L(12),L(7),Width-L(editing?50:24),L(27)),UiTheme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
            if(editing)TextRenderer.DrawText(g,"✓",UiTheme.Section,new Rectangle(Width-L(34),L(3),L(28),L(30)),UiTheme.Accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            int bottom=area.Bottom+L(5);
            string layer=source.Layer==null?"":UiText.T(source.Layer.Name);
            TextRenderer.DrawText(g,ageText+(layer.Length>0?" · "+layer:""),UiTheme.Small,new Rectangle(L(10),bottom,Width-L(20),L(22)),source.Stale?Color.FromArgb(244,183,83):UiTheme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            string author=source.Definition==null?"EFT Position":UiText.T("Mapa: ")+source.Definition.Author;
            TextRenderer.DrawText(g,author,UiTheme.Small,new Rectangle(L(10),bottom+L(19),Width-L(28),L(17)),UiTheme.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            if(editing)using(Pen handle=new Pen(UiTheme.Accent,L(2)))for(int n=5;n<=13;n+=4)g.DrawLine(handle,Width-L(n),Height-L(4),Width-L(4),Height-L(n));
        }
        protected override void OnResize(EventArgs e){base.OnResize(e);if(source!=null){ClearSnapshot();UpdateShape();}}
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==0x21){m.Result=new IntPtr(3);return;} // MA_NOACTIVATE: editing also leaves keyboard focus with the game.
            if(m.Msg==0x84 && !editing){m.Result=new IntPtr(-1);return;}
            base.WndProc(ref m);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);if(!editing || e.Button!=MouseButtons.Left)return;
            if(e.Y<L(36) && e.X>Width-L(38)){SetEditing(false);return;}
            initialMouse=PointToScreen(e.Location);initialBounds=Bounds;
            resizing=e.X>=Width-L(24) && e.Y>=Height-L(24);dragging=!resizing;Capture=true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);if(!editing)return;
            if(!dragging && !resizing){Cursor=e.X>=Width-L(24)&&e.Y>=Height-L(24)?Cursors.SizeNWSE:Cursors.SizeAll;return;}
            Point mouse=PointToScreen(e.Location);int dx=mouse.X-initialMouse.X,dy=mouse.Y-initialMouse.Y;
            Rectangle next;
            if(resizing)
            {int size=Math.Min(1040,Math.Max(L(240),Math.Min(L(520),initialBounds.Width+Math.Max(dx,dy))));next=new Rectangle(initialBounds.Location,new Size(size,size+L(82)));}
            else next=new Rectangle(initialBounds.X+dx,initialBounds.Y+dy,initialBounds.Width,initialBounds.Height);
            Screen screen=Screen.FromRectangle(next);Bounds=Placement(screen.Bounds,next.Size,preferences.OverlayCorner,true,next.X,next.Y);
        }
        void FinishDrag()
        {
            if(!dragging && !resizing)return;
            dragging=false;resizing=false;Capture=false;
            preferences.OverlaySize=(int)Math.Round(Width*96.0/DeviceDpi);preferences.OverlayX=Left;preferences.OverlayY=Top;preferences.OverlayCustomPosition=true;
            preferences.OverlayMonitor=Screen.FromControl(this).DeviceName;
            if(ConfigurationChanged!=null)ConfigurationChanged(this,EventArgs.Empty);
        }
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);FinishDrag();}
        protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture)FinishDrag();}
        protected override void OnFormClosing(FormClosingEventArgs e){FinishDrag();base.OnFormClosing(e);}
        protected override void Dispose(bool disposing){if(disposing)ClearSnapshot();base.Dispose(disposing);}
    }
}
