// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EftPosition
{
    static class UiTheme
    {
        public static readonly Color Background=Color.FromArgb(13,20,26),Surface=Color.FromArgb(20,30,38),
            Card=Color.FromArgb(25,37,46),Field=Color.FromArgb(16,26,34),Border=Color.FromArgb(45,62,74),
            Text=Color.FromArgb(234,241,245),Muted=Color.FromArgb(154,173,187),Accent=Color.FromArgb(81,224,195),
            Active=Color.FromArgb(28,67,65),Hover=Color.FromArgb(36,53,65);
        public static readonly Font Body=new Font("Segoe UI",9.5f),Small=new Font("Segoe UI",8.5f),
            Caption=new Font("Segoe UI",8,FontStyle.Bold),Section=new Font("Segoe UI Semibold",10.5f),
            Numbers=new Font("Consolas",10.5f);
        public static GraphicsPath Round(RectangleF bounds,float radius)
        {
            float d=Math.Min(radius*2,Math.Min(bounds.Width,bounds.Height));
            var path=new GraphicsPath();
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
            using(var path=UiTheme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),8))
            using(var fill=new SolidBrush(BackColor)) using(var border=new Pen(UiTheme.Border))
            {e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(border,path);}
        }
    }
    sealed class UiButton : Button
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
            using(var path=UiTheme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),6))
            using(var brush=new SolidBrush(Enabled?fill:UiTheme.Surface))
            using(var border=new Pen(!Enabled?UiTheme.Border:Focused?UiTheme.Accent:Primary?UiTheme.Accent:UiTheme.Border))
            {e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(border,path);}
            TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?(Primary?UiTheme.Background:ForeColor):UiTheme.Muted,
                TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
            if(Focused) ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5),Primary?UiTheme.Background:UiTheme.Accent,fill);
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
            using(var path=UiTheme.Round(new RectangleF(.5f,.5f,Width-1,Height-1),6))
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
        public UiComboBox()
        {
            DropDownStyle=ComboBoxStyle.DropDownList;DrawMode=DrawMode.OwnerDrawFixed;FlatStyle=FlatStyle.Flat;
            BackColor=UiTheme.Field;ForeColor=UiTheme.Text;Font=UiTheme.Body;ItemHeight=24;
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            bool selected=(e.State&DrawItemState.Selected)!=0;
            using(var brush=new SolidBrush(selected?UiTheme.Active:BackColor)) e.Graphics.FillRectangle(brush,e.Bounds);
            string text=e.Index>=0 && e.Index<Items.Count?Items[e.Index].ToString():Text;
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
                g.FillRectangle(fill,ClientRectangle);g.DrawRectangle(pen,0,0,Width-1,Height-1);
                string text=SelectedIndex>=0?Items[SelectedIndex].ToString():Text;
                TextRenderer.DrawText(g,text,Font,new Rectangle(9,1,Math.Max(1,Width-Height-12),Height-2),ForeColor,
                    TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
                g.SmoothingMode=SmoothingMode.AntiAlias;
                g.DrawLines(arrow,new[]{new Point(x-4,Height/2-2),new Point(x,Height/2+2),new Point(x+4,Height/2-2)});
            }
        }
    }
}
