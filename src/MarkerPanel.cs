// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EftPosition
{
    public sealed class MarkerPanel : Panel
    {
        readonly MarkerCatalog catalog;
        readonly MarkerSelection selection;
        readonly string preferencesFile;
        readonly ComboBox mode;
        readonly TextBox search;
        readonly CheckedListBox quests;
        readonly CheckBox pmc,scav,showQuests;
        readonly Label summary,footer;
        readonly ToolTip tips=new ToolTip {InitialDelay=400,AutoPopDelay=10000};
        bool rebuilding;
        string mapId="customs",hovered="";
        public event EventHandler Changed,BackRequested;
        public string MapId {get{return mapId;}}
        public MarkerSelection Selection {get{return selection;}}
        public MarkerCatalog Catalog {get{return catalog;}}
        public MapMarker[] VisibleMarkers {get{return selection.Visible(catalog,mapId);}}
        public MarkerPanel(string assets,string cache)
        {
            BackColor=UiTheme.Background;AccessibleName="Filtry ekstraktów i questów";
            catalog=MarkerCatalog.Load(Path.Combine(assets,"markers.json"));
            preferencesFile=Path.Combine(cache,"marker-preferences.json");selection=MarkerSelection.Load(preferencesFile);
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Margin=Padding.Empty,Padding=Padding.Empty};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(float height in new[]{34,50,40,48})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            foreach(float height in new[]{36,38,42})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            Controls.Add(layout);
            var heading=new Panel {Dock=DockStyle.Fill,Margin=Padding.Empty};
            heading.Controls.Add(new Label {Text="Questy i ekstrakty",Font=UiTheme.Section,ForeColor=UiTheme.Text,AutoSize=true,Location=new Point(0,4)});
            var back=new UiButton {Text="Pozycja",Dock=DockStyle.Right,Width=68,AccessibleName="Powrót do pozycji"};
            back.Click+=delegate{if(BackRequested!=null)BackRequested(this,EventArgs.Empty);};heading.Controls.Add(back);layout.Controls.Add(heading,0,0);
            var modeField=new Panel {Dock=DockStyle.Fill,Margin=new Padding(0,0,0,6)};
            mode=new UiComboBox {Dock=DockStyle.Bottom,AccessibleName="Tryb danych znaczników"};mode.Items.AddRange(new object[]{"PvP","PvE","PvP sezonowy"});
            mode.SelectedIndex=selection.Preferences.Mode=="pve"?1:selection.Preferences.Mode=="pvp-season"?2:0;
            modeField.Controls.Add(mode);modeField.Controls.Add(new Label {Text="TRYB",Dock=DockStyle.Top,Height=16,Font=UiTheme.Caption,ForeColor=UiTheme.Muted});layout.Controls.Add(modeField,0,1);
            var filters=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};
            pmc=new UiToggle {Text="PMC",Width=76,Margin=new Padding(0,0,4,0),Checked=selection.Preferences.ShowPmc,AccessibleName="Ekstrakty PMC"};
            scav=new UiToggle {Text="Scav",Width=78,Margin=new Padding(0,0,4,0),Checked=selection.Preferences.ShowScav,AccessibleName="Ekstrakty Scav"};
            showQuests=new UiToggle {Text="Questy",Width=80,Margin=Padding.Empty,Checked=selection.Preferences.ShowQuests,AccessibleName="Lokalizacje wybranych questów"};
            filters.Controls.AddRange(new Control[]{pmc,scav,showQuests});layout.Controls.Add(filters,0,2);
            tips.SetToolTip(pmc,"Lokalizacje ekstraktów PMC i wspólnych. Dostępność w rajdzie sprawdź w grze.");
            tips.SetToolTip(scav,"Lokalizacje ekstraktów Scav i wspólnych. Dostępność w rajdzie sprawdź w grze.");
            tips.SetToolTip(showQuests,"Pokaż lokalizacje zaznaczonych poniżej questów.");
            var searchField=new Panel {Dock=DockStyle.Fill,Margin=new Padding(0,0,0,6)};
            var searchRow=new Panel {Dock=DockStyle.Bottom,Height=28};
            var clear=new UiButton {Text="×",Dock=DockStyle.Right,Width=28,AccessibleName="Wyczyść wyszukiwanie questów"};
            var input=new Panel {Dock=DockStyle.Fill,BackColor=UiTheme.Field,Padding=new Padding(6,5,6,2)};
            search=new TextBox {Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,BackColor=UiTheme.Field,ForeColor=UiTheme.Text,Font=UiTheme.Body,AccessibleName="Szukaj questu na wybranej mapie"};
            input.Controls.Add(search);searchRow.Controls.Add(input);searchRow.Controls.Add(clear);
            searchField.Controls.Add(searchRow);searchField.Controls.Add(new Label {Text="Szukaj questu na tej mapie",Dock=DockStyle.Top,Height=16,ForeColor=UiTheme.Muted,Font=UiTheme.Small});layout.Controls.Add(searchField,0,3);
            quests=new CheckedListBox {Dock=DockStyle.Fill,IntegralHeight=false,CheckOnClick=true,BorderStyle=BorderStyle.None,BackColor=UiTheme.Field,ForeColor=UiTheme.Text,Font=UiTheme.Body,AccessibleName="Questy bieżącej mapy",Margin=Padding.Empty};
            layout.Controls.Add(quests,0,4);
            summary=new Label {Dock=DockStyle.Fill,Font=UiTheme.Small,ForeColor=UiTheme.Muted,Margin=new Padding(0,4,0,0)};layout.Controls.Add(summary,0,5);
            var actions=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var select=new UiButton {Text="Zaznacz wyniki",Dock=DockStyle.Fill,AccessibleName="Zaznacz widoczne questy",Margin=new Padding(0,0,6,6)};
            var reset=new UiButton {Text="Odznacz mapę",Dock=DockStyle.Fill,AccessibleName="Odznacz questy tej mapy",Margin=new Padding(0,0,0,6)};
            actions.Controls.Add(select,0,0);actions.Controls.Add(reset,1,0);layout.Controls.Add(actions,0,6);
            footer=new Label {Dock=DockStyle.Fill,Font=UiTheme.Small,ForeColor=UiTheme.Muted,Margin=Padding.Empty};layout.Controls.Add(footer,0,7);
            clear.Click+=delegate{search.Clear();search.Focus();};search.TextChanged+=delegate{RefreshList();};
            mode.SelectedIndexChanged+=delegate{selection.Preferences.Mode=mode.SelectedIndex==1?"pve":mode.SelectedIndex==2?"pvp-season":"regular";RefreshList();RaiseChanged();};
            EventHandler filter=delegate {selection.Preferences.ShowPmc=pmc.Checked;selection.Preferences.ShowScav=scav.Checked;selection.Preferences.ShowQuests=showQuests.Checked;UpdateSummary();RaiseChanged();};
            pmc.CheckedChanged+=filter;scav.CheckedChanged+=filter;showQuests.CheckedChanged+=filter;
            quests.ItemCheck+=delegate(object sender,ItemCheckEventArgs e)
            {
                if(rebuilding)return;MapQuest quest=(MapQuest)quests.Items[e.Index];
                if(e.NewValue==CheckState.Checked)selection.ForMap(mapId).Add(quest.Id);else selection.ForMap(mapId).Remove(quest.Id);
                UpdateSummary();RaiseChanged();
            };
            select.Click+=delegate{foreach(MapQuest quest in quests.Items)selection.ForMap(mapId).Add(quest.Id);RefreshList();RaiseChanged();};
            reset.Click+=delegate{selection.ForMap(mapId).Clear();RefreshList();RaiseChanged();};
            quests.MouseMove+=delegate(object sender,MouseEventArgs e){int i=quests.IndexFromPoint(e.Location);string text=i>=0?((MapQuest)quests.Items[i]).Name:"";if(text!=hovered){hovered=text;tips.SetToolTip(quests,text);}};
            RefreshList();
        }
        public void SetMap(string id)
        {
            if(mapId==id){RaiseChanged();return;}mapId=id;
            rebuilding=true;search.Clear();rebuilding=false;
            RefreshList();RaiseChanged();
        }
        public void FocusSearch() {search.Focus();search.SelectAll();}
        void RaiseChanged() {if(Changed!=null)Changed(this,EventArgs.Empty);}
        void RefreshList()
        {
            if(rebuilding)return;rebuilding=true;quests.BeginUpdate();
            try
            {quests.Items.Clear();foreach(MapQuest q in catalog.Search(mapId,selection.Preferences.Mode,search.Text))quests.Items.Add(q,selection.ForMap(mapId).Contains(q.Id));}
            finally{quests.EndUpdate();rebuilding=false;}
            UpdateSummary();
        }
        void UpdateSummary()
        {
            var current=catalog.ForMap(mapId,selection.Preferences.Mode);
            int chosen=current.Quests.Count(q=>selection.ForMap(mapId).Contains(q.Id));
            int points=current.Quests.Where(q=>selection.ForMap(mapId).Contains(q.Id)).Sum(q=>q.Points.Length);
            summary.Text=current.Quests.Length==0?"Brak danych lokalizacji questów\ndla tej mapy.":quests.Items.Count==0?"Brak wyników na tej mapie.\nWybrane: "+chosen:"Wybrane: "+chosen+" · Punkty: "+points+"\nNa liście: "+quests.Items.Count+" z "+current.Quests.Length;
            string stamp=(catalog.FetchedUtc??"").Length>=10?catalog.FetchedUtc.Substring(0,10):"";
            footer.Text=catalog.Error??("⚑ Quest · ↪ PMC / Scav\nSzare questy: inne piętro · tarkov.dev");
        }
        public bool Save() {return selection.Save(preferencesFile);}
        protected override void Dispose(bool disposing) {if(disposing)tips.Dispose();base.Dispose(disposing);}
    }
}
