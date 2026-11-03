// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace EftPosition
{
    public sealed class LootPanel : Panel
    {
        readonly string preferencesFile;
        readonly CheckBox enabled,minimap,group,floors;
        readonly FlowLayoutPanel body;
        readonly TextBox search;
        readonly Label summary;
        readonly List<LootFilterRow> rows=new List<LootFilterRow>();
        readonly List<FilterGroup> groups=new List<FilterGroup>();
        readonly ToolTip tips=new ToolTip {InitialDelay=350,AutoPopDelay=10000};
        string map="customs",mode="regular";bool rebuilding;
        public LootCatalog Catalog {get;private set;}public LootPreferences Preferences {get;private set;}
        public event EventHandler Changed,BackRequested;
        sealed class FilterGroup
        {public string Id,Name;public Panel Header;public CheckBox Check;public Button Expand;public bool Open=true;public readonly List<LootFilterRow> Rows=new List<LootFilterRow>();}
        public LootPanel(string assets,string cache)
        {
            BackColor=UiTheme.Surface;AccessibleName=UiText.T("Filtry lootu");
            Catalog=LootCatalog.Load(Path.Combine(assets,"loot.json"));preferencesFile=Path.Combine(cache,"loot-preferences.json");Preferences=LootPreferences.Load(preferencesFile);
            // Expand old broad category choices into independent stable type IDs.
            var disabled=new HashSet<string>(Preferences.DisabledTypes??new string[0],StringComparer.Ordinal);
            foreach(var type in Catalog.Types)if((Preferences.Categories&(1<<type.Category))==0)disabled.Add(type.Id);
            Preferences.DisabledTypes=disabled.Where(id=>id!=null).ToArray();Preferences.Categories=511;
            var top=new Panel {Dock=DockStyle.Top,Height=194};
            top.Controls.Add(new Label {Text=UiText.T("Map icons"),Font=UiTheme.Section,AutoSize=true,Location=new Point(0,4)});
            var close=new UiIconButton {Glyph="close",Anchor=AnchorStyles.Top|AnchorStyles.Right,Location=new Point(226,0),AccessibleName=UiText.T("Zamknij panel lootu")};close.Click+=delegate{if(BackRequested!=null)BackRequested(this,EventArgs.Empty);};top.Controls.Add(close);
            enabled=Toggle("Pokaż loot",Preferences.Enabled,top,36);group=Toggle("Grupuj bliskie punkty",Preferences.GroupNearby,top,64);
            enabled.Text=UiText.T("Show map icons");
            minimap=Toggle("Loot na minimapie",Preferences.ShowOnMiniMap,top,92);floors=Toggle("Wszystkie piętra",Preferences.AllFloors,top,92);
            minimap.Text=UiText.T("Minimap icons");minimap.Width=122;minimap.Anchor=AnchorStyles.Top|AnchorStyles.Left;floors.Left=128;floors.Width=124;floors.Anchor=AnchorStyles.Top|AnchorStyles.Left;
            var searchBox=new Panel {Location=new Point(0,126),Size=new Size(252,29),BackColor=UiTheme.Field,Padding=new Padding(8,6,32,2)};
            search=new TextBox {Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,BackColor=UiTheme.Field,ForeColor=UiTheme.Text,Font=UiTheme.Body,AccessibleName=UiText.T("Search map icons")};searchBox.Controls.Add(search);
            var clear=new UiButton {Text="×",Dock=DockStyle.Right,Width=26,AccessibleName=UiText.T("Clear icon search")};clear.Click+=delegate{search.Clear();search.Focus();};searchBox.Padding=new Padding(8,3,0,2);searchBox.Controls.Add(clear);top.Controls.Add(searchBox);
            var placeholder=new Label {Text=UiText.T("Search icons or items…"),AutoSize=false,Location=new Point(7,6),Size=new Size(205,19),Font=UiTheme.Small,ForeColor=UiTheme.Muted,BackColor=UiTheme.Field};
            searchBox.Controls.Add(placeholder);placeholder.BringToFront();placeholder.Click+=delegate{search.Focus();};
            search.TextChanged+=delegate{placeholder.Visible=search.Text.Length==0 && !search.Focused;};search.GotFocus+=delegate{placeholder.Visible=false;};search.LostFocus+=delegate{placeholder.Visible=search.Text.Length==0;};
            tips.SetToolTip(search,UiText.T("Search containers, categories or possible items"));
            var all=new UiButton {Text=UiText.T("Wszystkie"),Location=new Point(0,162),Size=new Size(120,27),AccessibleName=UiText.T("Pokaż wszystkie kategorie lootu")};
            var none=new UiButton {Text=UiText.T("Żadne"),Location=new Point(128,162),Size=new Size(120,27),AccessibleName=UiText.T("Ukryj wszystkie kategorie lootu")};top.Controls.AddRange(new Control[]{all,none});
            body=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=Padding.Empty};
            summary=new Label {Dock=DockStyle.Bottom,Height=62,Font=UiTheme.Small,ForeColor=UiTheme.Muted,Padding=new Padding(0,7,0,0)};
            Controls.Add(body);Controls.Add(summary);Controls.Add(top);
            foreach(var section in new[]{new[]{"container","Containers"},new[]{"loose","Loose loot"},new[]{"hazard","Hazards"},new[]{"utility","Utilities"}})
            {
                var entry=new FilterGroup {Id=section[0],Name=section[1]};groups.Add(entry);
                entry.Header=new Panel {Width=246,Height=34,Margin=new Padding(0,7,0,2)};
                entry.Check=new LootSectionToggle {Text=UiText.T(section[1]),ThreeState=true,AutoCheck=false,Font=UiTheme.Section,ForeColor=UiTheme.Text,Location=new Point(0,4),Size=new Size(207,26),AccessibleName=UiText.T("Toggle "+section[1])};
                entry.Expand=new UiButton {Text="−",Size=new Size(26,26),Location=new Point(216,4),AccessibleName=UiText.T("Expand "+section[1])};entry.Header.Controls.AddRange(new Control[]{entry.Check,entry.Expand});body.Controls.Add(entry.Header);
                foreach(var type in Catalog.Types.Where(t=>t.Group==entry.Id).OrderBy(t=>t.Name,StringComparer.OrdinalIgnoreCase))
                {
                    var row=new LootFilterRow(type) {Checked=Preferences.Allows(type),Width=246,Height=31,Margin=new Padding(0,0,0,2),AccessibleName=type.Name};rows.Add(row);entry.Rows.Add(row);body.Controls.Add(row);
                    row.CheckedChanged+=delegate{if(rebuilding)return;Preferences.SetType(type,row.Checked);Notify();};
                    tips.SetToolTip(row,type.DisplayName);
                }
                entry.Check.Click+=delegate{SetRows(entry.Rows,entry.Check.CheckState!=CheckState.Checked);};
                entry.Expand.Click+=delegate{entry.Open=!entry.Open;ApplySearch();};
            }
            search.TextChanged+=delegate{ApplySearch();};
            EventHandler optionChanged=delegate{if(rebuilding)return;Preferences.Enabled=enabled.Checked;Preferences.GroupNearby=group.Checked;Preferences.ShowOnMiniMap=minimap.Checked;Preferences.AllFloors=floors.Checked;Notify();};
            foreach(var box in new[]{enabled,group,minimap,floors})box.CheckedChanged+=optionChanged;
            all.Click+=delegate{SetRows(rows,true);enabled.Checked=true;};none.Click+=delegate{SetRows(rows,false);};
            Resize+=delegate{int width=Math.Max(1,body.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-2);foreach(var row in rows)row.Width=width;foreach(var entry in groups){entry.Header.Width=width;entry.Expand.Left=Math.Max(0,width-28);entry.Check.Width=Math.Max(1,width-34);}close.Left=Math.Max(0,top.ClientSize.Width-36);enabled.Width=group.Width=searchBox.Width=Math.Max(1,top.ClientSize.Width-2);minimap.Width=Math.Max(1,(top.ClientSize.Width-8)/2);floors.Left=minimap.Width+8;floors.Width=minimap.Width;};
            enabled.Enabled=Catalog.Error==null;UpdateCounts();UpdateSummary();
        }
        CheckBox Toggle(string text,bool value,Control parent,int y)
        {var c=new UiToggle {Text=UiText.T(text),AccessibleName=UiText.T(text),Checked=value,Location=new Point(0,y),Size=new Size(252,26)};parent.Controls.Add(c);return c;}
        void SetRows(IEnumerable<LootFilterRow> targets,bool value)
        {rebuilding=true;foreach(var row in targets){row.Checked=value;Preferences.SetType(row.Type,value);}rebuilding=false;Notify();}
        void Notify(){UpdateSummary();if(Changed!=null)Changed(this,EventArgs.Empty);}
        public void SetMap(string id,string gameMode){map=id;mode=gameMode;UpdateCounts();UpdateSummary();}
        void UpdateCounts()
        {
            var counts=new int[Catalog.Types.Length];int bit=LootCatalog.ModeBit(mode);
            foreach(var point in Catalog.ForMap(map))if((point.Modes&bit)!=0)
            {
                if(point.Items!=null)foreach(int t in point.Items.Select(i=>Catalog.Items[i].Type).Distinct())counts[t]++;
                else counts[point.Type]++;
            }
            foreach(var row in rows)row.Count=counts[Array.IndexOf(Catalog.Types,row.Type)];ApplySearch();
        }
        void ApplySearch()
        {
            body.SuspendLayout();string query=search.Text.Trim();
            foreach(var entry in groups)
            {
                foreach(var row in entry.Rows)
                {
                    bool match=query.Length==0 || Contains(row.Type.Name,query) || Contains(row.Type.LocalName,query) || Catalog.Items.Any(i=>i.Type==Array.IndexOf(Catalog.Types,row.Type) && (Contains(i.Name,query)||Contains(i.LocalName,query)));
                    row.Visible=match && (entry.Open || query.Length>0);
                }
                entry.Header.Visible=query.Length==0 || entry.Rows.Any(r=>Contains(r.Type.Name,query)||Contains(r.Type.LocalName,query)||Catalog.Items.Any(i=>i.Type==Array.IndexOf(Catalog.Types,r.Type)&&(Contains(i.Name,query)||Contains(i.LocalName,query))));
                entry.Expand.Text=entry.Open?"−":"+";
            }
            body.ResumeLayout();
        }
        static bool Contains(string text,string query){return (text??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;}
        void UpdateSummary()
        {
            foreach(var entry in groups){int count=entry.Rows.Count(r=>r.Checked);entry.Check.CheckState=count==0?CheckState.Unchecked:count==entry.Rows.Count?CheckState.Checked:CheckState.Indeterminate;}
            int bit=LootCatalog.ModeBit(mode),countPoints=Catalog.ForMap(map).Count(p=>(p.Modes&bit)!=0 && (p.Items==null?Preferences.Allows(Catalog.Types[p.Type]):p.Items.Any(i=>Preferences.Allows(Catalog.Types[Catalog.Items[i].Type]))));
            summary.Text=Catalog.Error??(UiText.Format("Map locations: {0}",Preferences.Enabled?countPoints:0)+"\n"+UiText.T("Possible locations · all floors · tarkov.dev")+"\n"+UiText.T("Hover an icon for details."));
        }
        public void RefreshLanguage(){UiText.Apply(this,tips);foreach(var row in rows){row.Text=row.Type.DisplayName;row.AccessibleName=row.Type.DisplayName;tips.SetToolTip(row,row.Type.DisplayName);}UpdateSummary();ApplySearch();}
        public void FocusSearch(){search.Focus();search.SelectAll();}
        public bool Save(){return Preferences.Save(preferencesFile);}
        protected override void Dispose(bool disposing){if(disposing)tips.Dispose();base.Dispose(disposing);}
    }
    internal sealed class LootSectionToggle : CheckBox
    {
        public LootSectionToggle(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;}
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.Clear(Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            var rect=new RectangleF(6,6,14,14);bool selected=CheckState!=CheckState.Unchecked;
            using(var path=UiTheme.Round(rect,3))using(var bg=new SolidBrush(selected?UiTheme.Accent:UiTheme.Field))using(var edge=new Pen(selected?UiTheme.Accent:UiTheme.Muted)){g.FillPath(bg,path);g.DrawPath(edge,path);}
            if(selected)using(var p=new Pen(UiTheme.Contrast(UiTheme.Accent),1.7f)){p.StartCap=p.EndCap=LineCap.Round;if(CheckState==CheckState.Indeterminate)g.DrawLine(p,9,13,17,13);else g.DrawLines(p,new[]{new PointF(9,13),new PointF(12,16),new PointF(17,10)});}
            TextRenderer.DrawText(g,Text,Font,new Rectangle(29,0,Math.Max(1,Width-29),Height),UiTheme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,ClientRectangle,UiTheme.Muted,BackColor);
        }
    }
    internal sealed class LootFilterRow : CheckBox
    {
        readonly Bitmap image;
        public readonly LootType Type;
        int count;
        public int Count {get{return count;}set{count=value;Invalidate();}}
        public LootFilterRow(LootType type)
        {Type=type;Text=type.DisplayName;image=LootArtwork.Create(type.Icon,24);Font=UiTheme.Body;Cursor=Cursors.Hand;AutoSize=false;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.Clear(Parent==null?UiTheme.Surface:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            if(ClientRectangle.Contains(PointToClient(MousePosition)))using(var bg=new SolidBrush(UiTheme.Hover))using(var path=UiTheme.Round(new RectangleF(0,0,Width-1,Height-1),5))g.FillPath(bg,path);
            var box=new Rectangle(6,9,13,13);
            using(var path=UiTheme.Round(box,3))using(var fill=new SolidBrush(Checked?UiTheme.Accent:UiTheme.Field))using(var edge=new Pen(Checked?UiTheme.Accent:UiTheme.Muted,1)){g.FillPath(fill,path);g.DrawPath(edge,path);}
            if(Checked)using(var pen=new Pen(UiTheme.Contrast(UiTheme.Accent),1.7f)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLines(pen,new[]{new PointF(9,15),new PointF(12,18),new PointF(17,12)});}
            g.DrawImageUnscaled(image,25,3);
            TextRenderer.DrawText(g,Text,Font,new Rectangle(55,0,Math.Max(1,Width-89),Height),count>0?UiTheme.Text:UiTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g,count.ToString(),UiTheme.Small,new Rectangle(Math.Max(0,Width-33),0,30,Height),UiTheme.Muted,TextFormatFlags.Right|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
            if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(2,2,Math.Max(1,Width-4),Math.Max(1,Height-4)),UiTheme.Muted,BackColor);
        }
        protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);Invalidate();}
        protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);Invalidate();}
        protected override void Dispose(bool disposing){if(disposing)image.Dispose();base.Dispose(disposing);}
    }
}
