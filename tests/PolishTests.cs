// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace EftPosition
{
    static class PolishTests
    {
        static int checks;
        static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
        static Position P(double y){return new Position("",DateTime.MinValue,200,y,150,0,false,0);}
        static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
        static void Wait(Func<bool> condition,string text)
        {var clock=Stopwatch.StartNew();while(!condition() && clock.ElapsedMilliseconds<15000)Pump(10);Check(condition(),text);}
        static IEnumerable<Control> Controls(Control parent)
        {foreach(Control c in parent.Controls){yield return c;foreach(Control nested in Controls(c))yield return nested;}}
        static T Find<T>(Control parent,string name) where T:Control
        {MainForm f=parent as MainForm;if(f!=null && (name=="Ścieżka folderu zrzutów" || name=="Usuwaj zrzut po odczycie")){f.ShowSettings();((SettingsForm)f.SettingsWindow).ShowCapturePage();return Find<T>(f.SettingsWindow,name);}return Controls(parent).OfType<T>().Single(c=>c.AccessibleName==name);}
        static Snapshot Write(string folder,int counter)
        {
            string name="2026-10-27[12-34]_200.00, 2.30, 150.00_0.0, 0.70711, 0.0, 0.70711_16.60 ("+counter+").png";
            string file=Path.Combine(folder,name);File.WriteAllBytes(file,new byte[]{1,2,3});
            Position p;Check(ScreenshotParser.TryParse(name,out p),"fixture filename parses");
            var f=new FileInfo(file);return new Snapshot(p,f.LastWriteTimeUtc,f.CreationTimeUtc,DateTime.UtcNow);
        }
        static void Save(Form f,string path){using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(path);}}
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string root=Path.GetFullPath(args[0]);Check(!Directory.Exists(root),"use a fresh isolated fixture folder");Directory.CreateDirectory(root);
                string assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets"),reports=Path.GetFullPath(args[1]);Directory.CreateDirectory(reports);
                string folder=Path.Combine(root,"screenshots");Directory.CreateDirectory(folder);
                Snapshot first=Write(folder,1);string firstPath=Path.Combine(folder,first.Position.FileName);
                string unrelated=Path.Combine(folder,"normal-screenshot.png");File.WriteAllText(unrelated,"keep");
                using(var cleaner=new ScreenshotCleaner(folder))
                {
                    Pump(700);Check(File.Exists(firstPath),"recognized but unacknowledged screenshot is retained");
                    using(var locked=new FileStream(firstPath,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                    {cleaner.ConfirmDisplayed(first);Pump(900);Check(File.Exists(firstPath) && cleaner.Deleted==0,"active writer prevents deletion");}
                    Wait(()=>!File.Exists(firstPath),"retry deletes acknowledged screenshot after writer closes");
                    Check(cleaner.Deleted==1,"one confirmed file deleted");Check(File.Exists(unrelated),"unrecognized PNG untouched");
                    Snapshot changed=Write(folder,2);string changedPath=Path.Combine(folder,changed.Position.FileName);
                    File.SetLastWriteTimeUtc(changedPath,changed.FileTimeUtc.AddMinutes(1));cleaner.ConfirmDisplayed(changed);Pump(900);
                    Check(File.Exists(changedPath) && cleaner.Failed==1,"changed file identity is retained");
                    Snapshot backlog=Write(folder,3);Pump(700);Check(File.Exists(Path.Combine(folder,backlog.Position.FileName)),"backlog is not bulk removed");
                    bool removed;var bad=new Snapshot(new Position("../normal-screenshot.png",DateTime.Now,1,1,1,0,false,0),DateTime.UtcNow,DateTime.UtcNow,DateTime.UtcNow);
                    Check(ScreenshotCleaner.TryDelete(folder,bad,out removed)==-1 && !removed,"path traversal rejected");
                    var normal=new Snapshot(new Position("normal-screenshot.png",DateTime.Now,1,1,1,0,false,0),DateTime.UtcNow,DateTime.UtcNow,DateTime.UtcNow);
                    Check(ScreenshotCleaner.TryDelete(folder,normal,out removed)==-1 && File.Exists(unrelated),"unparsed PNG cannot be deleted");
                }
                string pref=Path.Combine(root,"settings","app-preferences.json");var defaults=AppPreferences.Load(pref);
                Check(defaults.DeleteScreenshots && defaults.ScreenshotFolder=="","cleanup default is enabled");
                defaults.ScreenshotFolder=folder;defaults.DeleteScreenshots=false;Check(defaults.Save(pref),"first settings save");
                defaults.ScreenshotFolder=folder+"-nowy";Check(defaults.Save(pref),"atomic settings replacement");
                var reloaded=AppPreferences.Load(pref);Check(reloaded.ScreenshotFolder==folder+"-nowy" && !reloaded.DeleteScreenshots,"folder and cleanup choice survive reload");
                File.WriteAllText(pref,"{bad");Check(AppPreferences.Load(pref).ScreenshotFolder=="","corrupted settings recover");
                MapDefinition customs=MapCatalog.Load(assets).Single(m=>m.Id=="customs");var levels=new AutoNavigator(customs,Path.Combine(assets,"maps.upstream.json"));
                Check(levels.LevelAt(200,0,150)=="base" && levels.LevelAt(200,3.3,150)=="Second_Floor" && levels.LevelAt(200,6.2,150)=="Third_Floor","three Dorms floors resolve independently");
                levels.Update(P(3.3),"base");levels.LevelAt(200,6.2,150);Check(levels.Update(P(3.3),"Second_Floor").LayerId=="Second_Floor","quest classification does not move navigator state");
                var points=new[]{new MapMarker{Name="Ground",X=200,Y=0,Z=150,Kind=MapMarkerKind.Quest},new MapMarker{Name="Upstairs",X=200,Y=3.3,Z=150,Kind=MapMarkerKind.Quest},new MapMarker{Name="Unknown",X=200,Z=150,Kind=MapMarkerKind.Quest},new MapMarker{Name="Extract",X=200,Y=6.2,Z=150,Kind=MapMarkerKind.Pmc}};
                using(var canvas=new MapCanvas())
                {
                    canvas.SetMarkerLevels(levels);canvas.SetMarkers("customs",points);canvas.SetPosition(P(0));
                    Check(!canvas.IsMarkerDimmed(points[0]) && canvas.IsMarkerDimmed(points[1]),"only quest on another floor is dimmed at ground level");
                    Check(!canvas.IsMarkerDimmed(points[2]) && !canvas.IsMarkerDimmed(points[3]),"unknown height and extracts remain legible");
                    canvas.SetPosition(P(3.3));Check(canvas.IsMarkerDimmed(points[0]) && !canvas.IsMarkerDimmed(points[1]),"new screenshot reverses floor dimming");
                }
                var catalog=MarkerCatalog.Load(Path.Combine(assets,"markers.json"));var selected=new MarkerSelection();
                MapQuest mole=catalog.ForMap("ground-zero","regular").Quests.Single(q=>q.Name=="Saving the Mole");selected.ForMap("ground-zero").Add(mole.Id);
                Check(selected.Visible(catalog,"ground-zero").Where(p=>p.Kind==MapMarkerKind.Quest).All(p=>p.Y.HasValue),"source heights reach rendered quest markers");
                string live=Path.Combine(root,"live");Directory.CreateDirectory(live);string cache=Path.Combine(root,"ui-cache");
                Snapshot liveShot=Write(live,10);string liveFile=Path.Combine(live,liveShot.Position.FileName);File.WriteAllText(Path.Combine(live,"unrecognized.png"),"keep");
                using(var form=new MainForm(live,null,0,false,false,null,cache,false,false,"customs"))
                {
                    form.Show();MapCanvas canvas=Controls(form).OfType<MapCanvas>().Single();
                    Wait(()=>canvas.Definition!=null && canvas.Position!=null,"real window displays position");
                    Wait(()=>!File.Exists(liveFile),"real window deletes its displayed screenshot");
                    Check(canvas.Position.X==200 && canvas.Position.Z==150,"position survives file deletion");
                    Check(File.Exists(Path.Combine(live,"unrecognized.png")),"UI keeps unrelated PNG");
                    Check(AppPreferences.Load(Path.Combine(cache,"app-preferences.json")).ScreenshotFolder==live,"folder saved immediately on observing");
                    Save(form,Path.Combine(reports,"position.png"));
                    form.ClientSize=new Size(934,611);Pump(100);Check(!Controls(form).OfType<Panel>().Any(p=>p.Dock==DockStyle.Left && p.VerticalScroll.Visible),"new folder options fit minimum window");
                    Save(form,Path.Combine(reports,"minimum.png"));form.Close();
                }
                using(var reopened=new MainForm(null,null,0,false,false,null,cache,false,false,"customs"))
                {
                    reopened.Show();Check(Find<TextBox>(reopened,"Ścieżka folderu zrzutów").Text==live,"window restores folder without command line");
                    Snapshot next=Write(live,11);Wait(()=>!File.Exists(Path.Combine(live,next.Position.FileName)),"reopened app automatically watches restored folder");
                    Find<CheckBox>(reopened,"Usuwaj zrzut po odczycie").Checked=false;Snapshot keep=Write(live,12);Pump(1200);
                    Check(File.Exists(Path.Combine(live,keep.Position.FileName)),"visible switch disables deletion");reopened.Close();
                }
                using(var again=new MainForm(null,null,0,false,false,null,cache,false,false,"customs"))
                {again.Show();Check(!Find<CheckBox>(again,"Usuwaj zrzut po odczycie").Checked,"cleanup switch survives restart");again.Close();}
                string missingCache=Path.Combine(root,"missing-cache");new AppPreferences {ScreenshotFolder=Path.Combine(root,"missing")}.Save(Path.Combine(missingCache,"app-preferences.json"));
                using(var missing=new MainForm(null,null,0,false,false,null,missingCache))
                {missing.Show();Pump(100);Check(Find<TextBox>(missing,"Ścieżka folderu zrzutów").Text.EndsWith("missing"),"unavailable folder is retained without startup dialog");missing.Close();}
                using(var icon=new Icon(Path.Combine(assets,"app.ico"),new Size(32,32)))Check(icon.Width==32,"application icon loads at taskbar size");
                byte[] ico=File.ReadAllBytes(Path.Combine(assets,"app.ico"));Check(BitConverter.ToUInt16(ico,4)==9 && ico[6+8*16]==0,"ICO contains nine sizes including encoded 256 px frame");
                using(var startup=new MainForm(null,null,0,false,false,null,Path.Combine(root,"startup-cache"),false,false,"ground-zero"))
                {
                    startup.Show();var canvas=Controls(startup).OfType<MapCanvas>().Single();
                    Wait(()=>canvas.Definition!=null && canvas.VisibleMarkerCount==6,"six Ground Zero extracts appear before opening filters");
                    using(var b=new Bitmap(canvas.Width,canvas.Height))canvas.DrawToBitmap(b,canvas.ClientRectangle);
                    ExtractMarker exit=catalog.ForMap("ground-zero","regular").Extracts.First();
                    Check(canvas.MarkerTooltipAt(Point.Round(canvas.MarkerScreenPoint(exit.X,exit.Z))).Contains(exit.Name),"startup extracts are rendered and hoverable");
                    Save(startup,Path.Combine(reports,"startup-extracts.png"));startup.Close();
                }
                Console.WriteLine("PASS: "+checks+" checks: persistent folder, exclusive-handle cleanup, retries, opt-out, floor dimming and icon.");return 0;
            }
            catch(Exception e){Console.WriteLine(e);return 1;}
        }
    }
}
