// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace EftPosition
{
    static class MapDetectionTests
    {
        static int checks;
        static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        static void Pump(int ms){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();System.Threading.Thread.Sleep(10);}}
        static void Wait(Func<bool> condition,string name){var watch=Stopwatch.StartNew();while(!condition() && watch.ElapsedMilliseconds<12000)Pump(20);Check(condition(),name);}
        static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
        static string Line(string id,DateTime time,bool scene=false)
        {return time.ToString("yyyy-MM-dd HH:mm:ss.fff",System.Globalization.CultureInfo.InvariantCulture)+"|1.2.5.0.48146|Info|application|"+(scene?"scene preset path:maps/customs_preset.bundle rcid:"+id+".scenespreset.asset":"TRACE-NetworkGameCreate profileStatus: test, Location: "+id+", RaidMode: Online")+"\n";}
        const string InterchangeScene="2026-10-28 13:40:51.319|1.2.5.0.48146|Info|application|scene preset path:maps/shopping_mall.bundle rcid:Shopping_Mall.ScenesPreset.asset";
        static string CurrentInterchangeScene()
        {return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff",System.Globalization.CultureInfo.InvariantCulture)+InterchangeScene.Substring(23)+"\n";}
        static void Discover(GameMapMonitor m){typeof(GameMapMonitor).GetField("nextDiscovery",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(m,DateTime.MinValue);m.Poll();}
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        static void Parser()
        {
            var pairs=new[]{"bigmap|customs","factory4_day|factory","factory4_night|factory","Sandbox|ground-zero","Sandbox_high|ground-zero","Woods|woods","Shoreline|shoreline","Interchange|interchange","Lighthouse|lighthouse","RezervBase|reserve","TarkovStreets|streets-of-tarkov","laboratory|the-lab","labyrinth|the-labyrinth","Terminal|terminal","Icebreaker|icebreaker"};
            foreach(string pair in pairs)
            {string[] p=pair.Split('|');DateTime time;foreach(bool scene in new[]{false,true})Check(GameMapMonitor.ParseLine(Line(p[0],DateTime.Now,scene),out time)==p[1] && time.Kind==DateTimeKind.Utc,"canonical alias and log format: "+pair);}
            DateTime unused;
            foreach(string bad in new[]{Line("unknownmap",DateTime.Now),Line("hideout",DateTime.Now,true),Line("woods",DateTime.Now).Replace("|application|","|notifications|"),Line("woods",DateTime.Now).Replace("2026-","xxxx-"),"Location: woods","scene preset path:maps/woods.bundle rcid:woods.scenespreset.asset",new string('x',33000)})
                Check(GameMapMonitor.ParseLine(bad,out unused)==null,"ambiguous and unrelated text is not map detection");
        }
        static void ActualScenes()
        {
            // Scene identifiers observed in game logs differ from raid Location names.
            var entries=new[]{
                InterchangeScene+"|interchange",
                "2026-10-28 13:40:51.319|1.2.5.0.48146|Info|application|scene preset path:maps/factory_day_preset.bundle rcid:factory_day.scenespreset.asset|factory",
                "2026-10-28 13:40:51.319|1.2.5.0.48146|Info|application|scene preset path:maps/factory_night_preset.bundle rcid:factory_night.scenespreset.asset|factory",
                "2026-10-28 13:40:51.319|1.2.5.0.48146|Info|application|scene preset path:maps/rezerv_base_preset.bundle rcid:Rezerv_Base.scenespreset.asset|reserve",
                "2026-10-28 13:40:51.319|1.2.5.0.48146|Info|application|scene preset path:maps/city_preset.bundle rcid:city.scenespreset.asset|streets-of-tarkov",
                "2026-10-28 13:40:51.319|1.2.5.0.48146|Info|application|scene preset path:maps/customs_preset.bundle rcid:bigmap.scenespreset.asset|customs",
                "2026-10-28 13:40:51.319|1.2.5.0.48146|Info|application|scene preset path:maps/sandbox_high_preset.bundle rcid:sandbox_high.scenespreset.asset|ground-zero",
                "2026-10-28 13:40:51.319|1.2.5.0.48146|Info|application|scene preset path:maps/woods_preset.bundle rcid:woods.scenespreset.asset|woods",
                "2026-10-28 13:40:51.319|1.2.5.0.48146|Info|application|scene preset path:maps/shoreline_preset.bundle rcid:shoreline.scenespreset.asset|shoreline",
                "2026-10-28 13:40:51.319|1.2.5.0.48146|Info|application|scene preset path:maps/laboratory_preset.bundle rcid:laboratory.ScenesPreset.asset|the-lab"};
            foreach(string entry in entries)
            {int split=entry.LastIndexOf('|');DateTime time;Check(GameMapMonitor.ParseLine(entry.Substring(0,split),out time)==entry.Substring(split+1),"observed scene maps to canonical map: "+entry.Substring(split+1));}
        }
        static void Files(string root)
        {
            string logs=Path.Combine(root,"Logs"),a=Path.Combine(logs,"log_2026.10.27_10-00-00"),b=Path.Combine(logs,"log_2026.10.28_10-00-00");Directory.CreateDirectory(a);
            string file=Path.Combine(a,"2026 application_000.log");File.WriteAllText(file,Line("bigmap",DateTime.Now));
            using(var m=new GameMapMonitor(logs,false))
            {
                m.Poll();Check(m.Latest.MapId=="customs","initial current session is read");long revision=m.Latest.Revision;
                m.Poll();Check(m.Latest.Revision==revision,"idle polling does not repeat events");
                string line=Line("woods",DateTime.Now,true);File.AppendAllText(file,line.Substring(0,line.Length-10));m.Poll();Check(m.Latest.Revision==revision,"partial writes wait for completed line");
                File.AppendAllText(file,line.Substring(line.Length-10));m.Poll();Check(m.Latest.MapId=="woods" && m.Latest.Revision==revision+1,"completed partial line switches once");
                File.WriteAllText(file,Line("factory4_night",DateTime.Now));m.Poll();Check(m.Latest.MapId=="factory","truncated file restarts safely");
                string rotated=Path.Combine(a,"2026 application_001.log");File.WriteAllText(rotated,Line("Sandbox_high",DateTime.Now,true));Discover(m);Check(m.Latest.MapId=="ground-zero","rotated application log is followed");
                Directory.CreateDirectory(b);string next=Path.Combine(b,"2026 application_000.log");File.WriteAllText(next,Line("Shoreline",DateTime.Now));Discover(m);Check(m.Latest.MapId=="shoreline","new game session is selected");
                File.AppendAllText(file,Line("Interchange",DateTime.Now));Discover(m);Check(m.Latest.MapId=="shoreline","late write in old session does not steal selection");
                string earlier=Path.Combine(logs,"log_2026.10.28_9-00-00");Directory.CreateDirectory(earlier);File.WriteAllText(Path.Combine(earlier,"application.log"),Line("Woods",DateTime.Now));Discover(m);Check(m.Latest.MapId=="shoreline","single digit hours sort by actual session time");
                File.AppendAllText(next,Line("hideout",DateTime.Now,true));m.Poll();Check(m.Latest.MapId=="shoreline","unknown map does not guess from coordinates");
                Check(GameMapMonitor.ResolveFolder(root)==logs && GameMapMonitor.ResolveFolder(logs)==logs,"installation and Logs folder can both be selected");
                byte[] before=File.ReadAllBytes(next);m.Poll();Check(before.SequenceEqual(File.ReadAllBytes(next)),"log reader never changes input files");
                m.Dispose();File.AppendAllText(next,Line("woods",DateTime.Now));m.Poll();Check(m.Latest.MapId=="shoreline","disposed monitor stops processing");
            }
            using(var missing=new GameMapMonitor(Path.Combine(root,"missing"),false)){missing.Poll();Check(missing.Latest==null && missing.Status=="missing","missing folder stays recoverable");}
            string huge=Path.Combine(root,"huge");Directory.CreateDirectory(huge);File.WriteAllText(Path.Combine(huge,"application.log"),new string('x',1100000)+"\n"+Line("Woods",DateTime.Now));
            using(var m=new GameMapMonitor(huge,false)){for(int i=0;i<7;i++)m.Poll();Check(m.Latest!=null && m.Latest.MapId=="woods","large history is read with a bounded tail");}
        }
        static void Hotkeys()
        {
            bool m=false,ctrl=false,alt=false,shift=false,f8=false;int toggles=0,edits=0;
            using(var h=new OverlayHotkeys(delegate{toggles++;},delegate{edits++;},delegate(Keys k){return k==Keys.F24?m:k==Keys.F8?f8:k==Keys.ControlKey?ctrl:k==Keys.Menu?alt:k==Keys.ShiftKey?shift:false;},Keys.F24))
            {
                Check(h.ToggleAvailable && h.EditAvailable,"isolated Ctrl Alt F24 and edit chord registration");
                ctrl=alt=m=true;h.PollKeys();Check(toggles==1,"key state fallback handles Ctrl Alt M without WM_HOTKEY");
                SendMessage(h.Handle,0x312,new IntPtr(0x4546),IntPtr.Zero);for(int i=0;i<20;i++)h.PollKeys();Check(toggles==1,"native message and held polling cannot toggle twice");
                shift=true;h.PollKeys();Check(edits==0,"adding shift while M is held does not retrigger");m=false;h.PollKeys();m=true;h.PollKeys();Check(edits==1,"fresh edit chord is separate from show hide");
                m=ctrl=alt=shift=false;h.PollKeys();
                if(h.BackupAvailable){f8=true;h.PollKeys();Check(toggles==2,"F8 offers alternate visibility shortcut");SendMessage(h.Handle,0x312,new IntPtr(0x4548),IntPtr.Zero);Check(toggles==2,"F8 native and polling paths are deduplicated");f8=false;h.PollKeys();}
                else Console.WriteLine("SKIP two F8 checks: another running app already owns F8.");
                int beforeNative=toggles;SendMessage(h.Handle,0x312,new IntPtr(0x4546),IntPtr.Zero);m=true;ctrl=alt=true;h.PollKeys();Check(toggles==beforeNative+1,"native-first dispatch is also deduplicated");
                using(var conflict=new OverlayHotkeys(delegate{throw new Exception("conflict toggled");},delegate{},delegate(Keys k){return k==Keys.F24 || k==Keys.ControlKey || k==Keys.Menu;},Keys.F24))
                {Check(!conflict.ToggleAvailable && !conflict.BackupAvailable,"another app's registration is respected");conflict.PollKeys();}
                h.Dispose();h.Dispose();Check(h.Handle==IntPtr.Zero,"hotkey timers and native window dispose idempotently");
            }
        }
        static void Interface(string root,string reports)
        {
            string logs=Path.Combine(root,"ui-logs"),shots=Path.Combine(root,"ui-shots"),cache=Path.Combine(root,"ui-cache");Directory.CreateDirectory(logs);Directory.CreateDirectory(shots);
            string log=Path.Combine(logs,"application.log");File.WriteAllText(log,Line("Sandbox_high",DateTime.Now));
            var prefs=new AppPreferences {AutoMapEnabled=true,GameLogsFolder=logs,ScreenshotFolder=shots,DeleteScreenshots=true,OverlayEnabled=true};prefs.Save(Path.Combine(cache,"app-preferences.json"));
            string old=Path.Combine(shots,"2026-10-28[12-34]_200.00, 2.30, 150.00_0.0, 0.70711, 0.0, 0.70711_16.60 (1).png");File.WriteAllBytes(old,new byte[]{1,2,3});File.SetLastWriteTimeUtc(old,DateTime.UtcNow.AddDays(-1));
            using(var f=new MainForm(null,null,0,false,false,null,cache,false,false,"customs"))
            {
                f.Show();var canvas=Field<MapCanvas>(f,"canvas");Wait(()=>canvas.Definition!=null && canvas.Definition.Id=="ground-zero","log event changes actual main and minimap map");Pump(300);
                Check(canvas.Position==null && File.Exists(old),"previous raid's screenshot is not placed or deleted on new map");
                string fresh=Path.Combine(shots,"2026-10-28[12-34]_201.00, 2.30, 151.00_0.0, 0.70711, 0.0, 0.70711_16.60 (2).png");File.WriteAllBytes(fresh,new byte[]{1,2,3});
                Wait(()=>canvas.Position!=null && canvas.Position.Counter==2 && !File.Exists(fresh),"new screenshot updates detected map and preserves cleanup workflow");
                File.AppendAllText(log,Line("Woods",DateTime.Now));Wait(()=>canvas.Definition!=null && canvas.Definition.Id=="woods","second map switches without restarting");Check(canvas.Position==null,"old location is cleared on second raid");
                f.ShowSettings();var settings=(SettingsForm)f.SettingsWindow;settings.ShowWindowPage();
                var check=Field<CheckBox>(settings,"autoMap");Check(check.Checked,"automatic map selection is visible in settings");
                using(var image=new Bitmap(settings.Width,settings.Height)){settings.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(reports,"automatic-map-pl.png"));}
                check.Checked=false;File.AppendAllText(log,Line("bigmap",DateTime.Now));Pump(800);Check(canvas.Definition.Id=="woods","disabled detector leaves manual selection unchanged");
                Check(!AppPreferences.Load(Path.Combine(cache,"app-preferences.json")).AutoMapEnabled,"map selection preference persists");
                Field<ComboBox>(settings,"language").SelectedIndex=1;Check(check.Text=="Automatically switch maps from game logs","new settings are translated live");
                settings.Close();f.Close();
            }
            using(var demo=new MainForm(null,null,0,false,true,null,cache)){demo.Show();Check(Field<GameMapMonitor>(demo,"gameMapMonitor")==null,"demo does not read production or configured game logs");demo.Close();}
            UiText.Language="pl";
        }
        static void CustomsToInterchange(string root,string reports)
        {
            string logs=Path.Combine(root,"switch-logs"),a=Path.Combine(logs,"log_2026.10.28_13-00-00"),b=Path.Combine(logs,"log_2026.10.28_13-39-17");
            string cache=Path.Combine(root,"switch-cache"),shots=Path.Combine(root,"switch-shots");Directory.CreateDirectory(a);Directory.CreateDirectory(shots);
            File.WriteAllText(Path.Combine(a,"application_000.log"),Line("bigmap",DateTime.Now.AddMinutes(-1),true));
            new AppPreferences {AutoMapEnabled=true,GameLogsFolder=logs,ScreenshotFolder=shots,DeleteScreenshots=true,OverlayEnabled=true}.Save(Path.Combine(cache,"app-preferences.json"));
            string old=Path.Combine(shots,"2026-10-28[12-34]_200.00, 2.30, 150.00_0.0, 0.70711, 0.0, 0.70711_16.60 (1).png");File.WriteAllBytes(old,new byte[]{1,2,3});
            using(var f=new MainForm(null,null,0,false,false,null,cache,false,false,"customs"))
            {
                f.Show();var canvas=Field<MapCanvas>(f,"canvas");Wait(()=>canvas.Definition!=null && canvas.Definition.Id=="customs" && canvas.Position!=null,"regression begins on Customs with an accepted position");
                f.WindowState=FormWindowState.Minimized;
                Directory.CreateDirectory(b);string file=Path.Combine(b,"2026.10.28_13-39-17_1.2.5.0.48146 application_000.log");string line=CurrentInterchangeScene();int half=line.Length/2;
                File.WriteAllText(file,line.Substring(0,half));Pump(2300);Check(canvas.Definition.Id=="customs","partial new session event does not switch prematurely");
                File.AppendAllText(file,line.Substring(half));Wait(()=>canvas.Definition!=null && canvas.Definition.Id=="interchange","actual Shopping_Mall scene switches Customs to Interchange in new session while minimized");
                Check(canvas.Position==null && !canvas.MiniMapHasPosition,"previous Customs position is cleared from both maps");
                var layer=MapCatalog.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets")).Single(m=>m.Id=="interchange").Layers[0];var center=layer.Unproject(layer.Width/2.0,layer.Height/2.0);
                string name=String.Format(System.Globalization.CultureInfo.InvariantCulture,"2026-10-28[12-34]_{0:F2}, 27.00, {1:F2}_0.0, 0.70711, 0.0, 0.70711_16.60 (2).png",center.X,center.Y);
                string fresh=Path.Combine(shots,name);File.WriteAllBytes(fresh,new byte[]{1,2,3});
                Wait(()=>canvas.Position!=null && canvas.Position.Counter==2 && canvas.MiniMapHasPosition && !File.Exists(fresh),"new screenshot appears on Interchange minimap and cleanup still works");
                using(var image=new Bitmap(f.MiniMap.Width,f.MiniMap.Height)){f.MiniMap.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(reports,"interchange-minimap.png"));}
                f.Close();
            }
            using(var restart=new MainForm(null,null,0,false,false,null,cache,false,false,"customs"))
            {restart.Show();var canvas=Field<MapCanvas>(restart,"canvas");Wait(()=>canvas.Definition!=null && canvas.Definition.Id=="interchange","restart reads existing actual scene and overrides initial Customs without needing a screenshot");restart.Close();}
        }
        [STAThread]static int Main(string[] args)
        {
            try
            {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                bool mapOnly=args.Length>2 && args[2]=="--map-only";
                string root=Path.GetFullPath(args[0]),reports=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);Directory.CreateDirectory(reports);Parser();ActualScenes();Files(root);if(!mapOnly)Hotkeys();Interface(root,reports);CustomsToInterchange(root,reports);
                Console.WriteLine("PASS: "+checks+" map detection"+(mapOnly?"":" and shortcut fallback")+" checks.");return 0;
            }
            catch(Exception e){Console.WriteLine(e);return 1;}
        }
    }
}
