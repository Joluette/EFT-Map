// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace EftPosition
{
    static class AppearanceTests
    {
        static int checks;
        static void Check(bool test,string message){checks++;if(!test)throw new Exception(message);}
        static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(Control nested in All(c))yield return nested;}}
        static T Find<T>(Control root,string name)where T:Control{return All(root).OfType<T>().Single(c=>c.AccessibleName==name);}
        static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
        static void Wait(Func<bool> test,string message){var clock=Stopwatch.StartNew();while(!test() && clock.ElapsedMilliseconds<15000)Pump(10);Check(test(),message);}
        static void Save(Form f,string path){using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(path);}}
        static void Key(Form f,Keys keys){object[] args={Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),keys};typeof(MainForm).GetMethod("ProcessCmdKey",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(f,args);Application.DoEvents();}
        [StructLayout(LayoutKind.Sequential)]struct Rect {public int L,T,R,B;}
        [StructLayout(LayoutKind.Sequential)]struct Info {public int Size;public Rect Item,Button;public int State;public IntPtr Combo,Edit,List;}
        [DllImport("user32.dll")]static extern bool GetComboBoxInfo(IntPtr control,ref Info info);
        [DllImport("user32.dll")]static extern int GetWindowRgn(IntPtr control,IntPtr region);
        [DllImport("gdi32.dll")]static extern IntPtr CreateRectRgn(int l,int t,int r,int b);
        [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr region);
        static bool PopupHasRoundCorner(ComboBox combo)
        {
            Info info=new Info{Size=Marshal.SizeOf(typeof(Info))};Check(GetComboBoxInfo(combo.Handle,ref info),"native list is available");IntPtr region=CreateRectRgn(0,0,0,0);
            try{if(GetWindowRgn(info.List,region)==0)return false;using(Region shape=Region.FromHrgn(region))return !shape.IsVisible(0,0) && shape.IsVisible(20,20);}
            finally{DeleteObject(region);}
        }
        [STAThread]static int Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string root=Path.GetFullPath(args[0]),reports=Path.GetFullPath(args[1]);Check(!Directory.Exists(root),"fresh test folder");Directory.CreateDirectory(root);Directory.CreateDirectory(reports);
                string cache=Path.Combine(root,"cache"),prefs=Path.Combine(cache,"app-preferences.json");
                string red=Path.Combine(root,"first.png"),blue=Path.Combine(root,"second.png");
                using(var b=new Bitmap(20,20)){using(Graphics g=Graphics.FromImage(b))g.Clear(Color.Red);b.Save(red);using(Graphics g=Graphics.FromImage(b))g.Clear(Color.Blue);b.Save(blue);}
                var legacy=Path.Combine(root,"legacy.json");File.WriteAllText(legacy,"{\"ScreenshotFolder\":\"screenshots\",\"DeleteScreenshots\":false}");
                AppPreferences migrated=AppPreferences.Load(legacy);Check(migrated.Theme=="tarkov" && migrated.RoundedCorners && migrated.WindowOpacity==100 && !migrated.DeleteScreenshots,"old settings migrate without losing screenshot choice");
                using(var f=new MainForm(null,null,0,false,false,null,cache,false,false,"ground-zero"))
                {
                    f.Show();MapCanvas map=All(f).OfType<MapCanvas>().Single();Wait(()=>map.Layer!=null,"initial map loads");
                    Check(map.Width>=f.ClientSize.Width-2,"no permanent sidebar takes map width");Check(map.Height>f.ClientSize.Height*.75,"compact controls leave map most of the window");
                    Check(map.VisibleMarkerCount==6,"extracts survive new shell");Check(Math.Abs(f.Opacity-1)<.001,"default window is opaque");Check(map.BackgroundError==null,"default Tarkov backdrop loads");
                    Save(f,Path.Combine(reports,"default.png"));
                    f.Size=f.MinimumSize;Pump(100);Check(map.Width>900 && map.Height>400,"map remains usable at minimum size");Save(f,Path.Combine(reports,"minimum.png"));f.ClientSize=new Size(1250,760);Pump(80);
                    Find<Button>(f,"Filtry mapy i questy").PerformClick();Check(All(f).OfType<MarkerPanel>().Single().Visible,"quest drawer opens");Check(map.Width<f.ClientSize.Width-250,"drawer reserves space only while open");
                    Find<Button>(f,"Zamknij panel questów").PerformClick();Check(map.Width>=f.ClientSize.Width-2,"closing drawer returns map width");
                    Find<Button>(f,"Ustawienia").PerformClick();SettingsForm settings=(SettingsForm)f.SettingsWindow;Check(settings!=null && settings.Visible,"gear opens settings");Check(Math.Abs(settings.Opacity-1)<.001,"settings stay opaque");Save(settings,Path.Combine(reports,"settings-appearance.png"));
                    Check(!All(f).OfType<TextBox>().Any(c=>c.AccessibleName=="Ścieżka folderu zrzutów"),"folder field has moved out of the main workspace");
                    ComboBox backgrounds=Find<ComboBox>(settings,"Tło aplikacji");
                    Check(backgrounds.Items.Count==4 && backgrounds.Items[3].ToString()=="Tarkov × Cyberpunk (HD)","Cyberpunk HD is an additional built-in choice");
                    Check(backgrounds.Items[1].ToString()=="Własny obraz" && backgrounds.Items[2].ToString()=="Jednolity kolor","existing background choice indexes retain their meaning");
                    Color originalPixel;
                    using(var b=new Bitmap(map.Width,map.Height)){map.DrawToBitmap(b,map.ClientRectangle);originalPixel=b.GetPixel(20,50);}
                    backgrounds.SelectedIndex=3;Check(AppPreferences.Load(prefs).BackgroundMode=="cyberpunk","Cyberpunk choice survives preference validation and save");
                    Check(map.BackgroundError==null,"Cyberpunk asset loads without fallback");
                    using(var b=new Bitmap(map.Width,map.Height)){map.DrawToBitmap(b,map.ClientRectangle);Check(b.GetPixel(20,50).B>originalPixel.B+20,"rendered backdrop actually changes to the neon image");}
                    using(var image=MapBackdrop.ReadImage(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","tarkov-cyberpunk-background.jpg")))Check(image.Width>=1600 && image.Height>=900,"bundled Cyberpunk backdrop is high resolution");
                    Save(f,Path.Combine(reports,"cyberpunk-background.png"));Save(settings,Path.Combine(reports,"cyberpunk-choice.png"));
                    backgrounds.SelectedIndex=0;
                    using(var b=new Bitmap(map.Width,map.Height)){map.DrawToBitmap(b,map.ClientRectangle);Check(b.GetPixel(20,50).ToArgb()==originalPixel.ToArgb(),"original Tarkov background remains available and restores correctly");}
                    ComboBox theme=Find<ComboBox>(settings,"Motyw kolorystyczny");theme.SelectedIndex=2;Check(f.BackColor==Color.FromArgb(19,23,20),"theme changes the actual window");
                    CheckBox round=Find<CheckBox>(settings,"Zaokrąglone krawędzie");round.Checked=false;
                    ComboBox menu=Find<ComboBox>(f,"Wybór mapy");Check(menu.Region.IsVisible(0,0),"square option changes selector region");round.Checked=true;Check(!menu.Region.IsVisible(0,0),"round option clips selector corners");
                    settings.ImportBackground(red);AppPreferences loaded=AppPreferences.Load(prefs);Check(loaded.BackgroundMode=="custom" && File.Exists(loaded.BackgroundFile),"chosen background copied to local cache and saved");
                    using(var lockTest=new FileStream(red,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(lockTest.Length>0,"import does not lock original image");
                    long revision=loaded.BackgroundRevision;settings.ImportBackground(blue);loaded=AppPreferences.Load(prefs);Check(loaded.BackgroundRevision>revision,"second image invalidates cached background even at the same path");
                    Find<TrackBar>(settings,"Przyciemnienie tła").Value=0;Find<TrackBar>(settings,"Krycie mapy").Value=65;
                    Check(map.VisibleMarkerCount==6,"changing map opacity does not hide markers");
                    Find<Button>(settings,"Ustawienia okna i mapy").PerformClick();Find<TrackBar>(settings,"Przezroczystość okna").Value=50;
                    Check(Math.Abs(f.Opacity-1)<.001,"fullscreen-only transparency does not affect windowed mode");
                    f.SetFullScreen(true);Pump(80);Check(Math.Abs(f.Opacity-.5)<.001 && map.MapOnly,"fullscreen applies requested opacity");Check(Math.Abs(settings.Opacity-1)<.001,"settings remain readable above translucent fullscreen map");
                    Check(Find<Button>(f,"Ustawienia na pełnym ekranie").Visible,"settings remain reachable in fullscreen");Save(settings,Path.Combine(reports,"settings-window.png"));
                    f.SetFullScreen(false);Check(Math.Abs(f.Opacity-1)<.001,"leaving fullscreen restores opaque window");
                    Find<CheckBox>(settings,"Przezroczystość tylko na pełnym ekranie").Checked=false;Check(Math.Abs(f.Opacity-.5)<.001,"opacity can apply in windowed mode too");
                    Key(f,Keys.F10);Check(Math.Abs(f.Opacity-1)<.001 && Find<TrackBar>(settings,"Przezroczystość okna").Value==0,"F10 restores visibility and updates settings");
                    int height=map.Height;Find<CheckBox>(settings,"Pokaż pasek pozycji").Checked=false;Check(map.Height>=height+40,"hiding footer returns its space to map");
                    Find<TrackBar>(settings,"Rozmiar znaczników").Value=140;Find<CheckBox>(settings,"Pokaż pasek pozycji").Checked=true;
                    settings.ShowCapturePage();Check(Find<TextBox>(settings,"Ścieżka folderu zrzutów").Visible,"capture settings are usable in their own page");Save(settings,Path.Combine(reports,"settings-capture.png"));
                    settings.Close();Check(f.SettingsWindow==null,"closing settings detaches capture controls safely");
                    f.Activate();menu.Focus();menu.DroppedDown=true;Pump(100);Check(PopupHasRoundCorner(menu),"expanded native dropdown has rounded corners");menu.DroppedDown=false;
                    f.ShowSettings();settings=(SettingsForm)f.SettingsWindow;Find<CheckBox>(settings,"Zaokrąglone krawędzie").Checked=false;settings.Close();f.Activate();menu.Focus();menu.DroppedDown=true;Pump(100);Check(!PopupHasRoundCorner(menu),"square mode removes expanded dropdown clipping");menu.DroppedDown=false;
                    f.ShowSettings();settings=(SettingsForm)f.SettingsWindow;Find<ComboBox>(settings,"Tło aplikacji").SelectedIndex=3;settings.Close();
                    Save(f,Path.Combine(reports,"customized.png"));Check(map.AppearanceCacheBytes<(long)map.Width*map.Height*8+1024,"background and map overlay use at most two viewport buffers");f.Close();
                }
                AppPreferences saved=AppPreferences.Load(prefs);Check(saved.Theme=="forest" && saved.MapOpacity==65 && saved.MarkerSize==140 && !saved.RoundedCorners && saved.BackgroundMode=="cyberpunk","customization and Cyberpunk background survive closing the application");
                using(var reopened=new MainForm(null,null,0,false,false,null,cache,false,false,"ground-zero"))
                {
                    reopened.Show();MapCanvas map=All(reopened).OfType<MapCanvas>().Single();Wait(()=>map.Layer!=null,"reopen loads map");Check(reopened.BackColor==Color.FromArgb(19,23,20),"reopen restores palette");reopened.ShowSettings();var settings=(SettingsForm)reopened.SettingsWindow;
                    Check(Find<ComboBox>(settings,"Tło aplikacji").SelectedIndex==3 && map.BackgroundError==null,"reopened window restores the Cyberpunk option and asset");
                    Check(Find<TrackBar>(settings,"Krycie mapy").Value==65,"reopen restores slider values");Find<Button>(settings,"Przywróć wygląd domyślny").PerformClick();saved=AppPreferences.Load(prefs);Check(saved.Theme=="tarkov" && saved.BackgroundMode=="tarkov" && saved.RoundedCorners && saved.WindowOpacity==100,"reset restores visual defaults");settings.Close();reopened.Close();
                }
                Console.WriteLine("PASS: "+checks+" appearance checks: compact shell, settings, backgrounds, opacity, fullscreen recovery, rounded dropdowns and saved choices.");return 0;
            }
            catch(Exception ex){Console.WriteLine(ex);return 1;}
        }
    }
}
