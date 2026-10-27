// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EftPosition
{
    static class UiTheme
    {
        public static int CornerRadius=8;
        public static Color Background=Color.FromArgb(13,20,26),Surface=Color.FromArgb(20,30,38),
            Card=Color.FromArgb(25,37,46),Field=Color.FromArgb(16,26,34),Border=Color.FromArgb(45,62,74),
            Text=Color.FromArgb(234,241,245),Muted=Color.FromArgb(154,173,187),Accent=Color.FromArgb(81,224,195),
            Active=Color.FromArgb(28,67,65),Hover=Color.FromArgb(36,53,65);
        public static Color Contrast(Color color)
        {return color.R*.299+color.G*.587+color.B*.114>145?Color.FromArgb(15,20,22):Color.FromArgb(245,245,236);}
        public static Dictionary<int,Color> Change(string theme,int customAccent)
        {
            Color[] old={Background,Surface,Card,Field,Border,Text,Muted,Accent,Active,Hover};
            if(theme=="night")
            {Background=Color.FromArgb(13,20,26);Surface=Color.FromArgb(20,30,38);Card=Color.FromArgb(25,37,46);Field=Color.FromArgb(16,26,34);Border=Color.FromArgb(45,62,74);Text=Color.FromArgb(234,241,245);Muted=Color.FromArgb(154,173,187);Accent=Color.FromArgb(81,224,195);}
            else if(theme=="forest")
            {Background=Color.FromArgb(19,23,20);Surface=Color.FromArgb(28,33,28);Card=Color.FromArgb(35,41,34);Field=Color.FromArgb(23,28,23);Border=Color.FromArgb(60,69,54);Text=Color.FromArgb(232,234,218);Muted=Color.FromArgb(164,173,150);Accent=Color.FromArgb(182,199,128);}
            else
            {Background=Color.FromArgb(17,20,22);Surface=Color.FromArgb(25,29,31);Card=Color.FromArgb(33,38,40);Field=Color.FromArgb(20,24,26);Border=Color.FromArgb(53,61,64);Text=Color.FromArgb(232,232,222);Muted=Color.FromArgb(161,170,167);Accent=Color.FromArgb(203,181,138);}
            if(customAccent!=0){Color c=Color.FromArgb(customAccent);Accent=Color.FromArgb(c.R,c.G,c.B);}
            Active=Color.FromArgb((Accent.R+Surface.R*3)/4,(Accent.G+Surface.G*3)/4,(Accent.B+Surface.B*3)/4);
            Hover=Color.FromArgb(Math.Min(255,Card.R+12),Math.Min(255,Card.G+12),Math.Min(255,Card.B+12));
            Color[] current={Background,Surface,Card,Field,Border,Text,Muted,Accent,Active,Hover};
            var map=new Dictionary<int,Color>();for(int i=0;i<old.Length;i++)map[old[i].ToArgb()]=current[i];return map;
        }
        public static void Recolor(Control root,Dictionary<int,Color> colors)
        {
            Color next;if(colors.TryGetValue(root.BackColor.ToArgb(),out next))root.BackColor=next;
            if(colors.TryGetValue(root.ForeColor.ToArgb(),out next))root.ForeColor=next;
            foreach(Control child in root.Controls)Recolor(child,colors);root.Invalidate();
            UiComboBox combo=root as UiComboBox;if(combo!=null)combo.UpdateCorners();
        }
        public static readonly Font Body=new Font("Segoe UI",9.5f),Small=new Font("Segoe UI",8.5f),
            Caption=new Font("Segoe UI",8,FontStyle.Bold),Section=new Font("Segoe UI Semibold",10.5f),
            Numbers=new Font("Consolas",10.5f);
        public static GraphicsPath Round(RectangleF bounds,float radius)
        {
            float d=Math.Min(radius*2,Math.Min(bounds.Width,bounds.Height));
            var path=new GraphicsPath();
            if(radius<=0){path.AddRectangle(bounds);return path;}
            path.AddArc(bounds.X,bounds.Y,d,d,180,90);path.AddArc(bounds.Right-d,bounds.Y,d,d,270,90);
            path.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);path.AddArc(bounds.X,bounds.Bottom-d,d,d,90,90);
            path.CloseFigure();return path;
        }
    }
    sealed class UiCard : Panel
    {
        public UiCard() { DoubleBuffered=true;BackColor=UiTheme.Card;Margin=Padding.Empty; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent==null?UiTheme.Background:Parent.BackColor);
            if(Width<2 || Height<2) return;
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=UiTheme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),UiTheme.CornerRadius))
            using(var fill=new SolidBrush(BackColor)) using(var border=new Pen(UiTheme.Border))
            {e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(border,path);}
        }
    }
    class UiButton : Button
    {
        bool hover;
        public bool Primary {get;set;}
        public UiButton()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            FlatStyle=FlatStyle.Flat;Font=UiTheme.Body;Cursor=Cursors.Hand;
            BackColor=UiTheme.Card;ForeColor=UiTheme.Text;Margin=new Padding(0,0,6,0);
        }
        protected override void OnMouseEnter(EventArgs e) {hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e) {hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            if(Width<2 || Height<2) return;
            e.Graphics.Clear(Parent==null?UiTheme.Surface:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            Color fill=Primary?UiTheme.Accent:hover?UiTheme.Hover:BackColor;
            using(var path=UiTheme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),Math.Min(6,UiTheme.CornerRadius)))
            using(var brush=new SolidBrush(Enabled?fill:UiTheme.Surface))
            using(var border=new Pen(!Enabled?UiTheme.Border:Focused?UiTheme.Accent:Primary?UiTheme.Accent:UiTheme.Border))
            {e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(border,path);}
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?(Primary?UiTheme.Contrast(fill):ForeColor):UiTheme.Muted,
                TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
            if(Focused) ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5),Primary?UiTheme.Background:UiTheme.Accent,fill);
        }
    }
    sealed class UiIconButton : UiButton
    {
        public string Glyph {get;set;}
        public UiIconButton(){Size=new Size(36,32);Text="";}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            float x=Width/2f,y=Height/2f;Color color=Primary?UiTheme.Contrast(UiTheme.Accent):UiTheme.Text;
            using(Pen pen=new Pen(color,1.5f))
            {
                pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
                if(Glyph=="gear")
                {
                    PointF[] points=new PointF[32];for(int i=0;i<32;i++){double a=i*Math.PI/16;double r=(i%4==0 || i%4==3)?9:7;points[i]=new PointF(x+(float)(Math.Cos(a)*r),y+(float)(Math.Sin(a)*r));}
                    g.DrawPolygon(pen,points);g.DrawEllipse(pen,x-3,y-3,6,6);
                }
                else if(Glyph=="close") {g.DrawLine(pen,x-5,y-5,x+5,y+5);g.DrawLine(pen,x+5,y-5,x-5,y+5);}
                else if(Glyph=="quests")
                {g.DrawLine(pen,x-4,y-8,x-4,y+8);g.DrawLines(pen,new[]{new PointF(x-4,y-8),new PointF(x+7,y-6),new PointF(x+4,y-2),new PointF(x+7,y+2),new PointF(x-4,y)});}
                else
                {g.DrawLines(pen,new[]{new PointF(x-3,y-7),new PointF(x-8,y-7),new PointF(x-8,y-2)});g.DrawLines(pen,new[]{new PointF(x+3,y-7),new PointF(x+8,y-7),new PointF(x+8,y-2)});g.DrawLines(pen,new[]{new PointF(x-8,y+2),new PointF(x-8,y+7),new PointF(x-3,y+7)});g.DrawLines(pen,new[]{new PointF(x+8,y+2),new PointF(x+8,y+7),new PointF(x+3,y+7)});}
            }
        }
    }
    sealed class UiToggle : CheckBox
    {
        public UiToggle()
        {
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
            Appearance=Appearance.Button;AutoSize=false;Font=UiTheme.Body;Height=32;Cursor=Cursors.Hand;
            Margin=new Padding(0,0,6,0);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if(Width<2 || Height<2) return;
            e.Graphics.Clear(Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=UiTheme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),Math.Min(6,UiTheme.CornerRadius)))
            using(var fill=new SolidBrush(Checked?UiTheme.Active:UiTheme.Surface))
            using(var pen=new Pen(Checked || Focused?UiTheme.Accent:UiTheme.Border))
            {e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}
            int d=Math.Max(11,Font.Height-4),x=10,y=(Height-d)/2;
            using(var pen=new Pen(Checked?UiTheme.Accent:UiTheme.Muted,1.3f))
            {
                e.Graphics.DrawRectangle(pen,x,y,d,d);
                if(Checked) e.Graphics.DrawLines(pen,new[]{new Point(x+2,y+d/2),new Point(x+d/2,y+d-2),new Point(x+d-1,y+2)});
            }
            TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(x+d+7,0,Width-x-d-9,Height),Checked?UiTheme.Accent:UiTheme.Muted,
                TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
            if(Focused) ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4),UiTheme.Accent,UiTheme.Surface);
        }
    }
    sealed class UiComboBox : ComboBox
    {
        [StructLayout(LayoutKind.Sequential)]struct NativeRect {public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)]struct ComboInfo {public int Size;public NativeRect Item,Button;public int State;public IntPtr Combo,Edit,List;}
        [DllImport("user32.dll")]static extern bool GetComboBoxInfo(IntPtr handle,ref ComboInfo info);
        [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr handle,out NativeRect rect);
        [DllImport("gdi32.dll")]static extern IntPtr CreateRoundRectRgn(int left,int top,int right,int bottom,int width,int height);
        [DllImport("user32.dll")]static extern int SetWindowRgn(IntPtr handle,IntPtr region,bool redraw);
        [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr value);
        public UiComboBox()
        {
            DropDownStyle=ComboBoxStyle.DropDownList;DrawMode=DrawMode.OwnerDrawFixed;FlatStyle=FlatStyle.Flat;
            BackColor=UiTheme.Field;ForeColor=UiTheme.Text;Font=UiTheme.Body;ItemHeight=24;
        }
        public void UpdateCorners()
        {
            if(Width<2 || Height<2)return;
            using(GraphicsPath shape=UiTheme.Round(new RectangleF(0,0,Width,Height),UiTheme.CornerRadius))Region=new Region(shape);
            if(IsHandleCreated && DroppedDown)RoundList();
        }
        protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);UpdateCorners();}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);UpdateCorners();}
        protected override void OnDropDown(EventArgs e)
        {base.OnDropDown(e);if(IsHandleCreated)BeginInvoke((MethodInvoker)delegate{if(!IsDisposed && DroppedDown)RoundList();});}
        void RoundList()
        {
            ComboInfo info=new ComboInfo {Size=Marshal.SizeOf(typeof(ComboInfo))};NativeRect rect;
            if(!GetComboBoxInfo(Handle,ref info) || info.List==IntPtr.Zero || !GetWindowRect(info.List,out rect))return;
            if(UiTheme.CornerRadius==0){SetWindowRgn(info.List,IntPtr.Zero,true);return;}
            IntPtr region=CreateRoundRectRgn(0,0,rect.Right-rect.Left+1,rect.Bottom-rect.Top+1,UiTheme.CornerRadius*2,UiTheme.CornerRadius*2);
            if(region!=IntPtr.Zero && SetWindowRgn(info.List,region,true)==0)DeleteObject(region);
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            bool selected=(e.State&DrawItemState.Selected)!=0;
            using(var brush=new SolidBrush(selected?UiTheme.Active:BackColor)) e.Graphics.FillRectangle(brush,e.Bounds);
            string text=UiText.T(e.Index>=0 && e.Index<Items.Count?Items[e.Index].ToString():Text);
            TextRenderer.DrawText(e.Graphics,text,Font,new Rectangle(e.Bounds.X+9,e.Bounds.Y,e.Bounds.Width-14,e.Bounds.Height),ForeColor,
                TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            bool print=m.Msg==0x0317 || m.Msg==0x0318;
            if((m.Msg!=0x000F && !print) || Width<2 || Height<2 || (print && m.WParam==IntPtr.Zero)) return;
            using(Graphics g=print?Graphics.FromHdc(m.WParam):Graphics.FromHwnd(Handle))
            using(var fill=new SolidBrush(BackColor)) using(var pen=new Pen(Focused?UiTheme.Accent:UiTheme.Border))
            using(var arrow=new Pen(UiTheme.Muted,1.5f))
            {
                int x=Width-Height/2;
                using(var parentFill=new SolidBrush(Parent==null?UiTheme.Background:Parent.BackColor))g.FillRectangle(parentFill,ClientRectangle);
                g.SmoothingMode=SmoothingMode.AntiAlias;
                using(var shape=UiTheme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),UiTheme.CornerRadius)){g.FillPath(fill,shape);g.DrawPath(pen,shape);}
                string text=UiText.T(SelectedIndex>=0?Items[SelectedIndex].ToString():Text);
                TextRenderer.DrawText(g,text,Font,new Rectangle(9,1,Math.Max(1,Width-Height-12),Height-2),ForeColor,
                    TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
                g.SmoothingMode=SmoothingMode.AntiAlias;
                g.DrawLines(arrow,new[]{new Point(x-4,Height/2-2),new Point(x,Height/2+2),new Point(x+4,Height/2-2)});
            }
        }
    }
}
