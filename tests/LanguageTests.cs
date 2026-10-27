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
    static class LanguageTests
    {
        static int checks;
        static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        static IEnumerable<Control> All(Control root)
        {foreach(Control c in root.Controls){yield return c;foreach(Control nested in All(c))yield return nested;}}
        static T Find<T>(Control root,string name)where T:Control{return All(root).OfType<T>().Single(c=>c.AccessibleName==name);}
        static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
        static void Wait(Func<bool> condition,string message){var clock=Stopwatch.StartNew();while(!condition() && clock.ElapsedMilliseconds<15000)Pump(10);Check(condition(),message);}
        static void Save(Form form,string file){using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(file);}}
        static void Hover(MapCanvas canvas,Point point)
        {typeof(MapCanvas).GetMethod("OnMouseMove",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(canvas,new object[]{new MouseEventArgs(MouseButtons.None,0,point.X,point.Y,0)});}
        [STAThread]static int Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string root=Path.GetFullPath(args[0]),reports=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);Directory.CreateDirectory(reports);
                string cache=Path.Combine(root,"cache"),file=Path.Combine(cache,"app-preferences.json");
                File.WriteAllText(Path.Combine(root,"legacy.json"),"{\"ScreenshotFolder\":\"custom folder\",\"DeleteScreenshots\":false,\"Theme\":\"night\",\"MapOpacity\":40}");
                var old=AppPreferences.Load(Path.Combine(root,"legacy.json"));
                Check(old.Language=="pl" && old.ScreenshotFolder=="custom folder" && !old.DeleteScreenshots && old.Theme=="night" && old.MapOpacity==40,"migration retains preferences and defaults to Polish");
                old.Language="unknown";old.Normalize();Check(old.Language=="pl","unsupported language falls back to Polish");
                var prefs=new AppPreferences {Theme="night",MapOpacity=40,BackgroundDim=14,DeleteScreenshots=false};prefs.Save(file);
                using(var form=new MainForm(null,null,0,false,false,null,cache,false,false,"ground-zero"))
                {
                    form.Show();MapCanvas canvas=All(form).OfType<MapCanvas>().Single();Wait(()=>canvas.Layer!=null,"map loads");
                    Find<Button>(form,"Filtry mapy i questy").PerformClick();var panel=All(form).OfType<MarkerPanel>().Single();
                    ComboBox mode=Find<ComboBox>(panel,"Tryb danych znaczników");mode.SelectedIndex=2;
                    TextBox search=Find<TextBox>(panel,"Szukaj questu na wybranej mapie");search.Text="mole";
                    var list=Find<CheckedListBox>(panel,"Questy bieżącej mapy");Check(list.Items.Count==1,"quest search has a result");list.SetItemChecked(0,true);
                    string quest=((MapQuest)list.Items[0]).Id;
                    form.ShowSettings();SettingsForm settings=(SettingsForm)form.SettingsWindow;
                    ComboBox language=Find<ComboBox>(settings,"Język / Language");Check(language.SelectedIndex==0 && language.Items.Count==2,"settings offer Polish and English");
                    language.SelectedIndex=1;Pump(150);
                    Check(settings.Text=="Settings · EFT Position" && Find<Button>(settings,"Appearance settings").Text=="Appearance","settings switch immediately");
                    Check(Find<Button>(form,"Map filters and quests").Text=="Quests & filters","main shell switches immediately");
                    Check(Find<Button>(panel,"Select visible quests").Text=="Select results" && mode.Items[2].ToString()=="Seasonal PvP","quest panel and data modes switch");
                    Check(search.Text=="mole" && list.GetItemChecked(0) && panel.Selection.ForMap("ground-zero").Contains(quest) && mode.SelectedIndex==2 && panel.Selection.Preferences.Mode=="pvp-season","language switch retains search, checks and selected data mode");
                    Check(canvas.VisibleMarkerCount==panel.VisibleMarkers.Length,"language switch retains visible markers");
                    Check(UiText.T("Poziom bazowy")=="Base level","base map level has an English label");
                    var saved=AppPreferences.Load(file);Check(saved.Language=="en" && saved.Theme=="night" && saved.MapOpacity==40 && saved.BackgroundDim==14 && !saved.DeleteScreenshots,"language saves without changing existing preferences");
                    var tips=(ToolTip)typeof(MainForm).GetField("detailsTip",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
                    Check(tips.GetToolTip(Find<Button>(form,"Settings")).StartsWith("Appearance,"),"button tooltips switch");
                    Save(settings,Path.Combine(reports,"settings-en.png"));Save(form,Path.Combine(reports,"main-en.png"));
                    Find<Button>(settings,"Screenshot settings").PerformClick();
                    Check(Find<CheckBox>(settings,"Delete screenshot after reading").Text=="Delete screenshot after reading","capture settings switch");
                    Save(settings,Path.Combine(reports,"screenshots-en.png"));
                    Find<Button>(settings,"Window and map settings").PerformClick();Save(settings,Path.Combine(reports,"window-en.png"));
                    Find<Button>(settings,"Appearance settings").PerformClick();
                    language.SelectedIndex=0;Pump(100);
                    Check(Find<Button>(form,"Filtry mapy i questy").Text=="Questy i filtry" && settings.Text=="Ustawienia · EFT Position","switching back restores Polish");
                    Check(mode.Items[2].ToString()=="PvP sezonowy" && mode.SelectedIndex==2 && list.GetItemChecked(0),"switching back preserves quest mode and checks");
                    Save(settings,Path.Combine(reports,"settings-pl.png"));
                    language.SelectedIndex=1;Find<Button>(settings,"Reset appearance").PerformClick();
                    Check(language.SelectedIndex==1 && AppPreferences.Load(file).Language=="en","appearance reset preserves language");
                    settings.Close();form.ShowSettings();settings=(SettingsForm)form.SettingsWindow;
                    Check(settings.Text=="Settings · EFT Position" && Find<ComboBox>(settings,"Język / Language").SelectedIndex==1,"reopening settings keeps English");settings.Close();
                    // Rebuild hovered markers without a paint, including overlapping PMC and Scav exits.
                    MapPoint world=canvas.Layer.Unproject(canvas.Layer.Width/2.0,canvas.Layer.Height/2.0);
                    var extracts=new[]{new MapMarker{Name="PMC extraction",X=world.X,Z=world.Y,Kind=MapMarkerKind.Pmc},new MapMarker{Name="Scav extraction",X=world.X,Z=world.Y,Kind=MapMarkerKind.Scav}};
                    foreach(int size in new[]{80,100,150})
                    {
                        var appearance=new AppPreferences {MarkerSize=size};canvas.ApplyAppearance(appearance,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets"));canvas.SetMarkers("ground-zero",extracts);canvas.FitMap();
                        PointF p=canvas.MarkerScreenPoint(world.X,world.Y);float scale=size/100f;
                        foreach(Point hit in new[]{Point.Round(p),Point.Round(new PointF(p.X,p.Y-16*scale)),Point.Round(new PointF(p.X+10*scale,p.Y-16*scale))})
                            Check(canvas.MarkerTooltipAt(hit)=="PMC extraction\nScav extraction","both extract names cover the icon at size "+size);
                        canvas.Zoom(1.3f);p=canvas.MarkerScreenPoint(world.X,world.Y);
                        Check(canvas.MarkerTooltipAt(Point.Round(new PointF(p.X,p.Y-16*scale))).Contains("PMC extraction"),"hover follows current camera without repaint");
                        Check(canvas.MarkerTooltipAt(new Point(2,2))=="","empty map space has no tooltip");
                    }
                    bool popup=false;var tooltip=(ToolTip)typeof(MapCanvas).GetField("markerTip",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(canvas);tooltip.Popup+=delegate{popup=true;};
                    form.Activate();canvas.Focus();Pump(80);
                    PointF center=canvas.MarkerScreenPoint(world.X,world.Y);Hover(canvas,Point.Round(new PointF(center.X,center.Y-16)));Pump(450);
                    Check(popup,"hover timer opens the actual tooltip window; visible="+canvas.Visible+", handle="+canvas.IsHandleCreated+", text="+typeof(MapCanvas).GetField("markerTipText",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(canvas)+", timer="+((System.Windows.Forms.Timer)typeof(MapCanvas).GetField("markerHoverTimer",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(canvas)).Enabled);
                    Hover(canvas,new Point(2,2));canvas.SetMarkers("different-map",extracts);
                    Check(canvas.MarkerTooltipAt(Point.Round(center))=="","other maps do not expose stale names");
                    form.SetFullScreen(true);Check(Find<Button>(form,"Full screen settings").Visible,"full screen controls switch language");form.SetFullScreen(false);
                    form.Close();
                }
                using(var form=new MainForm(null,null,0,false,false,null,cache,false,false,"ground-zero"))
                {
                    form.Show();Wait(()=>All(form).OfType<MapCanvas>().Single().Layer!=null,"English restart loads");
                    Check(Find<Button>(form,"Map filters and quests").Text=="Quests & filters","restart restores English");
                    form.ShowSettings();var settings=(SettingsForm)form.SettingsWindow;
                    Find<ComboBox>(settings,"Język / Language").SelectedIndex=0;settings.Close();form.Close();
                    Check(AppPreferences.Load(file).Language=="pl","Polish selection survives closing");
                }
                Console.WriteLine("PASS: "+checks+" language and extract-hover checks.");return 0;
            }
            catch(Exception ex){Console.WriteLine(ex);return 1;}
        }
    }
}
