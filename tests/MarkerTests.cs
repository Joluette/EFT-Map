// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace EftPosition
{
    static class MarkerTests
    {
        static int checks;
        static Exception failure;
        static void Check(bool ok,string message) {checks++;if(!ok)throw new Exception(message);}
        static void Near(double a,double b,string message) {Check(Math.Abs(a-b)<.02,message);}
        static IEnumerable<Control> Descend(Control c)
        {foreach(Control child in c.Controls){yield return child;foreach(Control nested in Descend(child))yield return nested;}}
        static T Find<T>(Control root,string name) where T:Control
        {return Descend(root).OfType<T>().Single(c=>c.AccessibleName==name);}
        static void Wait(Func<bool> condition,string message)
        {var clock=Stopwatch.StartNew();while(!condition() && clock.ElapsedMilliseconds<15000){Application.DoEvents();Thread.Sleep(5);}Check(condition(),message);}
        static void Save(Form form,string path)
        {using(Bitmap b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(path);}}
        static void Layout(Control root)
        {
            foreach(Control c in Descend(root)) if(c.Visible && (c is Button || c is ComboBox || c is TextBox || c is CheckBox || c is CheckedListBox))
            {
                Rectangle screen=c.RectangleToScreen(c.ClientRectangle);
                Check(c.Width>0 && c.Height>0,"filter control has size: "+c.AccessibleName);
                for(Control ancestor=c.Parent;ancestor!=null;ancestor=ancestor.Parent)
                    Check(ancestor.RectangleToScreen(ancestor.ClientRectangle).Contains(screen),"filter control not clipped: "+c.AccessibleName);
            }
        }
        static void DataChecks(string assets,string fixtures)
        {
            MarkerCatalog catalog=MarkerCatalog.Load(Path.Combine(assets,"markers.json"));
            Check(catalog.Error==null && catalog.Maps.Length==13,"all thirteen map catalogs load");
            foreach(MarkerMap map in catalog.Maps)
            {
                Check(map.Modes.Length==3,"all data modes: "+map.Id);
                foreach(MarkerMode mode in map.Modes)
                {
                    Check(mode.Quests.Select(q=>q.Id).Distinct().Count()==mode.Quests.Length,"unique quest IDs within map/mode");
                    foreach(MapQuest q in mode.Quests)
                    {
                        Check(q.Points.Length>0 && !q.Name.EndsWith(" name"),"quest has locations and translated name");
                        Check(catalog.Search(map.Id,mode.Id,q.Name).Any(found=>found.Id==q.Id),"quest searchable within its own map/mode");
                        foreach(MarkerPoint p in q.Points)Check(!Double.IsNaN(p.X+p.Z) && Math.Abs(p.X)<100000 && Math.Abs(p.Z)<100000,"finite quest coordinates");
                    }
                }
            }
            MarkerMode ground=catalog.ForMap("ground-zero","regular");
            Check(ground.Quests.Length>=5 && ground.Quests.Length<100,"Ground Zero is scoped, not the whole task database");
            Check(catalog.Search("ground-zero","regular","Background Check").Length==0,"Customs quest is not returned on Ground Zero");
            Check(catalog.Search("customs","regular","Background Check").Length==1,"Background Check is available on Customs");
            Check(catalog.Search("ground-zero","regular","RATOWANIE KRETA").Single().Name=="Saving the Mole","Polish names and case are searchable");
            Check(catalog.Search("ground-zero","regular","zrodlo informacji").Single().Name=="Information Source","Polish diacritics are optional");
            Check(catalog.Search("ground-zero","regular","MOLE SAVING").Single().Name=="Saving the Mole","multiple search words work in either order");
            foreach(string mode in new[]{"regular","pve","pvp-season"})
            {
                Check(catalog.Search("ground-zero",mode,"Gunsmith").Length==0,"quests without map locations are not offered");
                Check(catalog.ForMap("terminal",mode).Quests.Length==0 && catalog.ForMap("terminal",mode).Extracts.Length==0,"missing Terminal data is not invented");
                Check(catalog.ForMap("icebreaker",mode).Extracts.Any(e=>e.Name.Length>0),"Icebreaker helipad near map edge is retained");
            }
            ExtractMarker emercom=ground.Extracts.Single(e=>e.Name=="Emercom Checkpoint");
            Near(emercom.X,151.63,"verified Ground Zero Emercom X");Near(emercom.Z,-97.46,"verified Ground Zero Emercom Z");
            MapQuest first=ground.Quests.Single(q=>q.Name=="First in Line");
            Check(first.Points.Any(p=>Math.Abs(p.X-156.2)<.02 && Math.Abs(p.Z+83.59)<.02),"verified First in Line medical zone");
            MapQuest rubber=ground.Quests.Single(q=>q.Name=="Burning Rubber");
            Check(rubber.Points.Any(p=>Math.Abs(p.X+19.51)<.02 && Math.Abs(p.Z-114.94)<.02),"named extraction objective resolves to Police Cordon");
            MarkerSelection selection=new MarkerSelection();
            Check(selection.Visible(catalog,"ground-zero").All(p=>p.Kind!=MapMarkerKind.Quest),"quests start unselected");
            selection.Preferences.ShowPmc=false;Check(selection.Visible(catalog,"ground-zero").Length<ground.Extracts.Length,"PMC filter removes PMC-only exits");
            selection.Preferences.ShowScav=false;Check(selection.Visible(catalog,"ground-zero").Length==0,"both extraction filters can hide exits");
            selection.ForMap("ground-zero").Add(first.Id);selection.ForMap("ground-zero").Add(rubber.Id);
            Check(selection.Visible(catalog,"ground-zero").Length==first.Points.Length+rubber.Points.Length,"multiple quests display all their locations");
            Check(selection.Visible(catalog,"customs").Length==0,"selection does not leak to another map");
            selection.Preferences.Mode="pve";Check(selection.Visible(catalog,"ground-zero").Length==0,"selection is separate per game mode");
            selection.Preferences.Mode="regular";Check(selection.ForMap("ground-zero").Count==2,"returning restores previous selection");
            selection.Preferences.ShowQuests=false;Check(selection.Visible(catalog,"ground-zero").Length==0,"quest layer can be hidden without deleting selection");
            selection.Preferences.ShowQuests=true;
            string file=Path.Combine(fixtures,"preferences.json");Check(selection.Save(file),"preferences save atomically");
            MarkerSelection restored=MarkerSelection.Load(file);Check(restored.ForMap("ground-zero").SetEquals(selection.ForMap("ground-zero")),"selection survives restart");
            Check(!restored.Preferences.ShowPmc && !restored.Preferences.ShowScav,"filters survive restart");
            File.WriteAllText(file,"broken JSON");Check(MarkerSelection.Load(file).Preferences.Mode=="regular","corrupt preferences fall back safely");
            Check(MarkerCatalog.Load(Path.Combine(fixtures,"absent.json")).Error!=null,"missing marker data does not prevent map use");
        }
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                string assets=Path.GetFullPath(args[0]),fixtures=Path.GetFullPath(args[1]),reports=Path.GetFullPath(args[2]);
                Directory.CreateDirectory(fixtures);Directory.CreateDirectory(reports);DataChecks(assets,fixtures);
                string cache=Path.Combine(fixtures,"cache");
                using(MainForm form=new MainForm(null,null,0,false,true,null,cache,false,false,"ground-zero"))
                using(var driver=new System.Windows.Forms.Timer {Interval=100})
                {
                    var start=Stopwatch.StartNew();
                    driver.Tick+=delegate
                    {
                        try
                        {
                            MapCanvas canvas=Descend(form).OfType<MapCanvas>().Single();
                            if(canvas.Layer==null){if(start.ElapsedMilliseconds>15000)throw new Exception("Ground Zero load timed out");return;}
                            driver.Stop();
                            Find<Button>(form,"Filtry mapy i questy").PerformClick();
                            MarkerPanel panel=Descend(form).OfType<MarkerPanel>().Single();
                            Check(panel.Visible && panel.MapId=="ground-zero","filter panel follows selected map");
                            TextBox search=Find<TextBox>(panel,"Szukaj questu na wybranej mapie");
                            CheckedListBox list=Find<CheckedListBox>(panel,"Questy bieżącej mapy");
                            search.Text="mole";Check(list.Items.Count==1 && list.Items[0].ToString()=="Saving the Mole","UI search is map-scoped");
                            list.SetItemChecked(0,true);string moleId=((MapQuest)list.Items[0]).Id;
                            search.Text="shooting";Check(list.Items.Count==1,"second quest can be searched independently");list.SetItemChecked(0,true);
                            Check(panel.Selection.ForMap("ground-zero").Count==2,"search does not erase hidden checked quests");
                            search.Text="zzzzzzzz";Check(list.Items.Count==0 && panel.Selection.ForMap("ground-zero").Count==2,"zero results preserve selection");search.Clear();
                            Check(list.CheckedItems.Count==2,"clearing search restores checked rows");
                            Check(canvas.VisibleMarkerCount>panel.Catalog.ForMap("ground-zero","regular").Extracts.Length,"quest selection reaches canvas");
                            CheckBox questLayer=Find<CheckBox>(panel,"Lokalizacje wybranych questów"),pmc=Find<CheckBox>(panel,"Ekstrakty PMC"),scav=Find<CheckBox>(panel,"Ekstrakty Scav");
                            questLayer.Checked=false;Check(panel.VisibleMarkers.All(m=>m.Kind!=MapMarkerKind.Quest),"UI quest toggle hides quest pins");questLayer.Checked=true;
                            pmc.Checked=false;scav.Checked=false;Check(panel.VisibleMarkers.All(m=>m.Kind==MapMarkerKind.Quest),"UI extraction filters hide all exits");
                            pmc.Checked=true;scav.Checked=true;Check(list.CheckedItems.Count==2,"filters do not erase quest choices");
                            canvas.FitMap();
                            using(Bitmap b=new Bitmap(canvas.Width,canvas.Height)){canvas.DrawToBitmap(b,canvas.ClientRectangle);}
                            MapQuest mole=panel.Catalog.ForMap("ground-zero","regular").Quests.Single(q=>q.Id==moleId);
                            PointF p=canvas.MarkerScreenPoint(mole.Points[0].X,mole.Points[0].Z);
                            string tooltip=canvas.MarkerTooltipAt(Point.Round(p));
                            Check(tooltip.Split('\n').Contains("Saving the Mole"),"map hover shows the selected quest name");
                            string[] valid=panel.VisibleMarkers.Select(m=>m.Name).ToArray();
                            Check(tooltip.Split('\n').All(name=>valid.Contains(name)),"tooltip contains names only, no quest instructions or extra fields");
                            form.Size=form.MinimumSize;Application.DoEvents();canvas.FitMap();Layout(panel);Save(form,Path.Combine(reports,"markers-minimum.png"));
                            form.ClientSize=new Size(1250,760);Application.DoEvents();canvas.FitMap();Layout(panel);Save(form,Path.Combine(reports,"markers-ground-zero.png"));
                            ComboBox mapMenu=Find<ComboBox>(form,"Wybór mapy");
                            for(int i=0;i<mapMenu.Items.Count;i++)if(((MapDefinition)mapMenu.Items[i]).Id=="customs")mapMenu.SelectedIndex=i;
                            Wait(()=>canvas.Definition!=null && canvas.Definition.Id=="customs","switch map with filters open");
                            Check(panel.MapId=="customs" && list.CheckedItems.Count==0,"other map gets an independent selection");
                            search.Text="Background Check";Check(list.Items.Count==1,"Customs quest search available after map switch");list.SetItemChecked(0,true);search.Clear();
                            Find<Button>(panel,"Odznacz questy tej mapy").PerformClick();Check(list.CheckedItems.Count==0,"clear button only clears current map");
                            search.Text="Background Check";Find<Button>(panel,"Zaznacz widoczne questy").PerformClick();Check(list.CheckedItems.Count==1,"select results acts on the current search");search.Clear();
                            Save(form,Path.Combine(reports,"markers-customs.png"));
                            for(int i=0;i<mapMenu.Items.Count;i++)if(((MapDefinition)mapMenu.Items[i]).Id=="ground-zero")mapMenu.SelectedIndex=i;
                            Wait(()=>canvas.Definition!=null && canvas.Definition.Id=="ground-zero","return to Ground Zero");
                            Check(list.CheckedItems.Count==2,"returning restores map-specific choices");
                            ComboBox mode=Find<ComboBox>(panel,"Tryb danych znaczników");mode.SelectedIndex=1;Check(list.CheckedItems.Count==0,"UI mode change separates selections");mode.SelectedIndex=0;Check(list.CheckedItems.Count==2,"UI mode return restores selections");
                            for(int i=0;i<mapMenu.Items.Count;i++)if(((MapDefinition)mapMenu.Items[i]).Id=="terminal")mapMenu.SelectedIndex=i;
                            Wait(()=>canvas.Definition!=null && canvas.Definition.Id=="terminal","Terminal still loads with absent POI data");
                            Check(list.Items.Count==0 && canvas.VisibleMarkerCount==0,"Terminal has an explicit empty catalog, no invented points");Save(form,Path.Combine(reports,"markers-terminal.png"));
                            for(int i=0;i<mapMenu.Items.Count;i++)if(((MapDefinition)mapMenu.Items[i]).Id=="ground-zero")mapMenu.SelectedIndex=i;
                            Wait(()=>canvas.Definition!=null && canvas.Definition.Id=="ground-zero","return after empty catalog");
                            form.SetFullScreen(true);Application.DoEvents();Check(!panel.Visible,"fullscreen hides filter controls");
                            using(Bitmap b=new Bitmap(canvas.Width,canvas.Height))canvas.DrawToBitmap(b,canvas.ClientRectangle);
                            Check(canvas.VisibleMarkerCount>0,"markers remain active in fullscreen");Save(form,Path.Combine(reports,"markers-fullscreen.png"));
                            form.SetFullScreen(false);Find<Button>(panel,"Powrót do pozycji").PerformClick();Check(!panel.Visible,"return button restores position pane");
                            form.Close();
                        }
                        catch(Exception ex){failure=ex;driver.Stop();Console.WriteLine(ex);form.Close();}
                    };
                    driver.Start();Application.Run(form);
                }
                if(failure==null)
                {
                    MarkerSelection persisted=MarkerSelection.Load(Path.Combine(cache,"marker-preferences.json"));
                    Check(persisted.ForMap("ground-zero").Count==2 && persisted.ForMap("customs").Count==1,"real window close saves choices for both maps");
                    Console.WriteLine("PASS: "+checks+" marker checks; map-scoped search, multiple selection, filters, saved choices, hover names and fullscreen.");
                }
                return failure==null?0:1;
            }
            catch(Exception ex){Console.WriteLine(ex);return 1;}
        }
    }
}
