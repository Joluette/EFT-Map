// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EftPosition
{
    // Retain native sizing borders, minimize/maximize styles and system
    // commands. Paint the caption with the same palette on Windows 10 and 11.
    public class ThemedForm : Form
    {
        readonly WindowCaption caption;
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        public ThemedForm()
        {
            caption=new WindowCaption(this) {Dock=DockStyle.Top,Height=32,AccessibleName=UiText.T("Pasek tytułu okna"),AccessibleRole=AccessibleRole.TitleBar};
            Controls.Add(caption);
        }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters=base.CreateParams;
                if(FormBorderStyle!=FormBorderStyle.None && ControlBox)parameters.Style&=~0x00C00000; // WS_CAPTION
                return parameters;
            }
        }
        public void ApplyWindowTheme()
        {
            if(caption==null || IsDisposed)return;
            caption.RefreshTheme();
            if(!IsHandleCreated)return;
            int dark=1,border=ColorTranslator.ToWin32(UiTheme.Border),surface=ColorTranslator.ToWin32(UiTheme.Surface),text=ColorTranslator.ToWin32(UiTheme.Text);
            // Unsupported attributes are harmless HRESULT failures on older
            // Windows versions; the client-painted caption works on both.
            DwmSetWindowAttribute(Handle,20,ref dark,4);
            DwmSetWindowAttribute(Handle,34,ref border,4);
            DwmSetWindowAttribute(Handle,35,ref surface,4);
            DwmSetWindowAttribute(Handle,36,ref text,4);
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);UpdateCaption();ApplyWindowTheme();
            SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x37); // Recalculate frame without moving/activating the window.
        }
        protected override void OnControlAdded(ControlEventArgs e)
        {base.OnControlAdded(e);if(caption!=null && !caption.IsDisposed)caption.SendToBack();}
        protected override void OnStyleChanged(EventArgs e)
        {base.OnStyleChanged(e);UpdateCaption();}
        protected override void OnResize(EventArgs e)
        {base.OnResize(e);UpdateCaption();}
        protected override void OnTextChanged(EventArgs e)
        {base.OnTextChanged(e);if(caption!=null)caption.Invalidate();}
        protected override void OnActivated(EventArgs e)
        {base.OnActivated(e);if(caption!=null)caption.Invalidate();}
        protected override void OnDeactivate(EventArgs e)
        {base.OnDeactivate(e);if(caption!=null)caption.Invalidate();}
        void UpdateCaption()
        {
            if(caption==null || caption.IsDisposed)return;
            caption.Visible=FormBorderStyle!=FormBorderStyle.None && ControlBox;
            caption.Height=(int)Math.Round(32*DeviceDpi/96.0);caption.UpdateButtons();
        }
        protected override void WndProc(ref Message m)
        {
            const int NcHitTest=0x84;
            bool custom=caption!=null && FormBorderStyle!=FormBorderStyle.None && ControlBox;
            base.WndProc(ref m);
            if(custom && m.Msg==NcHitTest)
            {
                long value=m.LParam.ToInt64();Point point=PointToClient(new Point((short)(value&0xffff),(short)((value>>16)&0xffff)));
                if(caption.Bounds.Contains(point))m.Result=(IntPtr)caption.HitTest(new Point(point.X-caption.Left,point.Y-caption.Top));
            }
        }
    }

    sealed class WindowCaption : Control
    {
        readonly Form window;
        readonly CaptionButton minimize,maximize,close;
        int Logical(int value){return (int)Math.Round(value*DeviceDpi/96.0);}
        public WindowCaption(Form form)
        {
            window=form;TabStop=false;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
            minimize=new CaptionButton("minimize",UiText.T("Minimalizuj okno"));maximize=new CaptionButton("maximize",UiText.T("Maksymalizuj okno"));close=new CaptionButton("close",UiText.T("Zamknij okno"));
            minimize.Click+=delegate{window.WindowState=FormWindowState.Minimized;};
            maximize.Click+=delegate{window.WindowState=window.WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;};
            close.Click+=delegate{window.Close();};
            Controls.AddRange(new Control[]{minimize,maximize,close});RefreshTheme();
        }
        public void RefreshTheme(){BackColor=UiTheme.Surface;ForeColor=UiTheme.Text;Invalidate(true);}
        public void UpdateButtons()
        {
            bool tool=window.FormBorderStyle==FormBorderStyle.SizableToolWindow || window.FormBorderStyle==FormBorderStyle.FixedToolWindow;
            minimize.Visible=window.MinimizeBox && !tool;maximize.Visible=window.MaximizeBox && !tool;close.Visible=window.ControlBox;
            maximize.Glyph=window.WindowState==FormWindowState.Maximized?"restore":"maximize";
            maximize.AccessibleName=window.WindowState==FormWindowState.Maximized?UiText.T("Przywróć rozmiar okna"):UiText.T("Maksymalizuj okno");
            PerformLayout();Invalidate(true);
        }
        protected override void OnResize(EventArgs e){base.OnResize(e);PerformLayout();}
        protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(Visible)UpdateButtons();}
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);if(close==null)return;
            int x=Width,w=Logical(46);
            foreach(CaptionButton button in new[]{close,maximize,minimize})if(button.Visible){x-=w;button.SetBounds(x,0,w,Height);}
        }
        public int HitTest(Point point)
        {
            foreach(Control button in Controls)if(button.Visible && button.Bounds.Contains(point))return 1; // HTCLIENT
            if(window.ShowIcon && point.X<Logical(32))return 3; // HTSYSMENU
            return 2; // HTCAPTION: native dragging, double-click and system menu.
        }
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==0x84){m.Result=(IntPtr)(-1);return;} // Let the parent hit-test caption background.
            base.WndProc(ref m);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Graphics g=e.Graphics;g.Clear(UiTheme.Surface);
            int left=Logical(12),iconSize=Logical(16),right=Width;
            if(window.ShowIcon && window.Icon!=null){g.DrawIcon(window.Icon,new Rectangle(Logical(8),(Height-iconSize)/2,iconSize,iconSize));left=Logical(34);}
            foreach(Control button in Controls)if(button.Visible)right=Math.Min(right,button.Left);
            TextRenderer.DrawText(g,window.Text,UiTheme.Small,new Rectangle(left,0,Math.Max(0,right-left-Logical(8)),Height),Form.ActiveForm==window?UiTheme.Text:UiTheme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
            using(var pen=new Pen(UiTheme.Border))g.DrawLine(pen,0,Height-1,Width,Height-1);
        }
    }

    sealed class CaptionButton : Button
    {
        public string Glyph;
        bool hover,pressed;
        public CaptionButton(string glyph,string name)
        {
            Glyph=glyph;AccessibleName=name;TabStop=false;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            MouseEnter+=delegate{hover=true;Invalidate();};MouseLeave+=delegate{hover=false;pressed=false;Invalidate();};
            MouseDown+=delegate{pressed=true;Invalidate();};MouseUp+=delegate{pressed=false;Invalidate();};
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Color fill=hover?(Glyph=="close"?Color.FromArgb(196,43,56):pressed?UiTheme.Active:UiTheme.Hover):UiTheme.Surface;
            Color ink=hover && Glyph=="close"?Color.White:UiTheme.Text;
            e.Graphics.Clear(fill);e.Graphics.SmoothingMode=SmoothingMode.None;
            float size=(float)Math.Round(10*DeviceDpi/96.0),x=(float)Math.Floor((Width-size)/2)+.5f,y=(float)Math.Floor((Height-size)/2)+.5f;
            using(var pen=new Pen(ink,Math.Max(1,DeviceDpi/96f)))
            {
                if(Glyph=="minimize")e.Graphics.DrawLine(pen,x,y+size/2,x+size,y+size/2);
                else if(Glyph=="close"){e.Graphics.DrawLine(pen,x,y,x+size,y+size);e.Graphics.DrawLine(pen,x+size,y,x,y+size);}
                else if(Glyph=="restore")
                {float d=size*.25f;e.Graphics.DrawRectangle(pen,x+d,y,size-d,size-d);using(var brush=new SolidBrush(fill))e.Graphics.FillRectangle(brush,x,y+d,size-d,size-d);e.Graphics.DrawRectangle(pen,x,y+d,size-d,size-d);}
                else e.Graphics.DrawRectangle(pen,x,y,size,size);
            }
        }
    }
}
