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
    static class OverlayTests
    {
        static int checks;
        static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(Control n in All(c))yield return n;}}
        static T Find<T>(Control root,string name)where T:Control{return All(root).OfType<T>().Single(c=>c.AccessibleName==name);}
        static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
        static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(5);}}
        static void Wait(Func<bool> condition,string message){var clock=Stopwatch.StartNew();while(!condition() && clock.ElapsedMilliseconds<15000)Pump(10);Check(condition(),message);}
        static void Save(Control form,string file){using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(file);}}
        static void Mouse(Control target,string method,MouseEventArgs e){typeof(MiniMapOverlay).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,new object[]{e});}
        [StructLayout(LayoutKind.Sequential)]struct NativePoint{public int X,Y;public NativePoint(Point p){X=p.X;Y=p.Y;}}
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(NativePoint point);
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        static void Preferences(string root)
        {
            string legacy=Path.Combine(root,"legacy.json");File.WriteAllText(legacy,"{\"Language\":\"en\",\"Theme\":\"night\",\"MapOpacity\":69,\"MarkerSize\":80,\"DeleteScreenshots\":false}");
            AppPreferences p=AppPreferences.Load(legacy);
            Check(!p.OverlayEnabled && p.OverlaySize==320 && p.OverlayZoom==180 && p.OverlayOpacity==90,"older preferences receive conservative overlay defaults");
            Check(p.Language=="en" && p.Theme=="night" && p.MapOpacity==69 && p.MarkerSize==80 && !p.DeleteScreenshots,"migration retains existing choices");
            p.OverlayEnabled=true;p.OverlaySize=9999;p.OverlayOpacity=-5;p.OverlayZoom=0;p.OverlayCorner="bad";p.OverlayMonitor=new string('x',201);p.Normalize();
            Check(p.OverlaySize==520 && p.OverlayOpacity==30 && p.OverlayZoom==100 && p.OverlayCorner=="top-right" && p.OverlayMonitor=="","overlay limits reject malformed configuration");
            p.OverlaySize=240;p.OverlayOpacity=50;p.OverlayZoom=250;p.OverlayCorner="bottom-left";p.OverlayX=-600;p.OverlayY=120;p.OverlayCustomPosition=true;p.ResetAppearance();
            Check(p.OverlayEnabled && p.OverlaySize==240 && p.OverlayZoom==250 && p.OverlayCustomPosition && p.OverlayX==-600,"appearance reset preserves minimap choices");
            string file=Path.Combine(root,"roundtrip.json");Check(p.Save(file),"overlay preferences save");var q=AppPreferences.Load(file);
            Check(q.OverlayEnabled && q.OverlayX==-600 && q.OverlayY==120 && q.OverlayCorner=="bottom-left" && q.OverlayZoom==250,"overlay configuration survives serialization");
            foreach(Rectangle screen in new[]{new Rectangle(0,0,1920,1080),new Rectangle(-1920,0,1920,1080),new Rectangle(0,-1080,1920,1080),new Rectangle(0,0,360,240)})
                foreach(string corner in new[]{"top-left","top-right","bottom-left","bottom-right"})
                {
                    Rectangle rect=MiniMapOverlay.Placement(screen,new Size(320,402),corner,false,0,0);
                    Check(screen.Contains(rect),"corner is visible on screens including negative coordinates");
                    Rectangle custom=MiniMapOverlay.Placement(screen,new Size(520,602),corner,true,100000,-100000);
                    Check(screen.Contains(custom),"custom position clamps after display change");
                }
        }
        static void Raster(string root)
        {
            string image=Path.Combine(root,"base.png");using(var b=new Bitmap(320,320)){using(var g=Graphics.FromImage(b))g.Clear(Color.CornflowerBlue);b.Save(image);}
            var layer=new MapLayer {Id="base",Name="Base",Width=320,Height=320,Affine=new double[]{1,0,0,1,0,0},Remote=true,TileTemplate="https://assets.tarkov.dev/maps/test/{z}/{x}/{y}.png",TileSize=256,TileZoom=1,TileLeft=0,TileTop=0,MaxTileZoom=3};
            var map=new MapDefinition {Id="test",Name="Test",Layers=new[]{layer}};
            using(var canvas=new MapCanvas {Size=new Size(640,640)})
            {
                canvas.LoadMap(map,layer,image,Path.Combine(root,"tiles"));canvas.SetPosition(new Position("sample",DateTime.Now,160,0,160,90,true,1));
                canvas.SetMarkers("test",new[]{new MapMarker{Name="Exit",Kind=MapMarkerKind.Pmc,X=170,Z=155}});
                var tiles=Field<TileMap>(canvas,"tiles");float scale=Field<float>(canvas,"scale");
                for(int i=0;i<12;i++)using(var preview=canvas.RenderMiniMap(new Size(304,304),100+i*20))
                {Color center=preview.GetPixel(152,152);Check(center.G>150,"player is centered over raster at every minimap zoom");}
                Check(tiles.PendingCount==0 && tiles.DownloadCount==0,"minimap does not enqueue or download competing tiles");
                Check(Field<float>(canvas,"scale")==scale,"minimap does not change the main raster camera");
                canvas.ClearMap();using(var preview=canvas.RenderMiniMap(new Size(304,304),180))Check(!canvas.MiniMapHasPosition,"cleared map cannot display a stale position");
            }
        }
        [STAThread]static int Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                bool desktop=args.Length<3 || args[2]!="--noninteractive";
                string root=Path.GetFullPath(args[0]),reports=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);Directory.CreateDirectory(reports);Preferences(root);Raster(root);
                string cache=Path.Combine(root,"cache"),shots=Path.Combine(root,"screenshots");Directory.CreateDirectory(shots);
                var prefs=new AppPreferences {ScreenshotFolder=shots,DeleteScreenshots=false,Language="pl",Theme="night",BackgroundMode="plain",MapOpacity=69,MarkerSize=80,OverlayEnabled=true};
                string file=Path.Combine(cache,"app-preferences.json");prefs.Save(file);
                string shot=Path.Combine(shots,"2026-10-28[12-34]_200.00, 2.30, 150.00_0.0, 0.70711, 0.0, 0.70711_16.60 (1).png");File.WriteAllBytes(shot,new byte[]{1,2,3});
                bool toggleRegistered=false,editRegistered=false;
                using(var f=new MainForm(null,null,0,false,false,null,cache,false,false,"ground-zero"))
                using(var game=new Form {Text="Borderless test surface",FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Bounds=Screen.PrimaryScreen.Bounds,BackColor=Color.FromArgb(46,42,35)})
                {
                    f.Show();var source=All(f).OfType<MapCanvas>().Single();Wait(()=>source.Layer!=null && source.Position!=null,"map and synthetic screenshot load");
                    var overlay=f.MiniMap;Wait(()=>overlay.Visible,"enabled minimap appears");
                    Check(!overlay.ShowInTaskbar && overlay.Owner==null,"overlay has no taskbar button and is not owned by the main form");
                    int style=GetWindowLong(overlay.Handle,-20);
                    Check((style&0x080800a8)==0x080800a8,"native window is layered, topmost, tool window, transparent to input and nonactivating");
                    Check(SendMessage(overlay.Handle,0x84,IntPtr.Zero,IntPtr.Zero).ToInt32()==-1,"locked minimap returns HTTRANSPARENT");
                    Check(SendMessage(overlay.Handle,0x21,IntPtr.Zero,IntPtr.Zero).ToInt32()==3,"minimap refuses keyboard activation");
                    game.Show();game.Activate();Pump(80);IntPtr foreground=GetForegroundWindow();
                    f.ToggleOverlay();Check(!overlay.Visible,"show/hide action hides minimap");f.ToggleOverlay();Pump(80);
                    if(desktop)Check(GetForegroundWindow()==foreground,"showing overlay leaves the current foreground window unchanged");
                    else Console.WriteLine("SKIP foreground presentation assertion: noninteractive CI desktop. Native no-activate styles and messages are still checked.");
                    Point hit=overlay.PointToScreen(new Point(overlay.Width/2,overlay.Height/2));
                    if(desktop)Check(WindowFromPoint(new NativePoint(hit))!=overlay.Handle,"Windows hit testing reaches the window behind the locked overlay");
                    else Console.WriteLine("SKIP desktop point hit test: noninteractive CI desktop. HTTRANSPARENT is still checked.");
                    game.Hide();f.Activate();
                    Save(overlay,Path.Combine(reports,"minimap-pl.png"));int initialPaints=overlay.SnapshotCount;long bytes=overlay.CacheBytes;
                    for(int i=0;i<10;i++)using(var b=new Bitmap(overlay.Width,overlay.Height))overlay.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));
                    Check(overlay.SnapshotCount==initialPaints,"repeated window paints reuse the small map snapshot");
                    Check(bytes>0 && bytes<320L*320*4,"minimap caches less than one 320-square bitmap");
                    float scale=Field<float>(source,"scale"),x=Field<float>(source,"offsetX"),y=Field<float>(source,"offsetY");var vector=Field<VectorMap>(source,"vector");int renderCount=vector.RenderCount;
                    for(int i=0;i<6;i++)using(var b=source.RenderMiniMap(new Size(280,280),100+i*50))Check(b.Width==280,"shared SVG preview renders");
                    Check(vector.RenderCount==renderCount && scale==Field<float>(source,"scale") && x==Field<float>(source,"offsetX") && y==Field<float>(source,"offsetY"),"SVG minimap leaves the main viewport cache and camera unchanged");
                    f.WindowState=FormWindowState.Minimized;Pump(80);Check(overlay.Visible,"ownerless overlay stays visible while the main app is minimized");f.WindowState=FormWindowState.Normal;
                    f.ToggleOverlayEditing();Check(overlay.IsEditing && (GetWindowLong(overlay.Handle,-20)&0x20)==0 && Math.Abs(overlay.Opacity-1)<.001,"positioning mode accepts mouse interaction and restores full visibility");
                    hit=overlay.PointToScreen(new Point(overlay.Width/2,overlay.Height/2));if(desktop)Check(WindowFromPoint(new NativePoint(hit))==overlay.Handle,"positioning mode receives Windows hit tests");
                    else Console.WriteLine("SKIP positioning desktop point hit test: noninteractive CI desktop. Input styles and drag/resize events are still checked.");
                    Rectangle before=overlay.Bounds;
                    Mouse(overlay,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,70,20,0));Mouse(overlay,"OnMouseMove",new MouseEventArgs(MouseButtons.Left,0,30,50,0));Mouse(overlay,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,30,50,0));
                    var saved=AppPreferences.Load(file);Check(saved.OverlayCustomPosition && saved.OverlayX==overlay.Left && saved.OverlayY==overlay.Top && overlay.Bounds!=before,"dragging stores custom screen coordinates");
                    int oldWidth=overlay.Width;int mouseX=overlay.Width-6,mouseY=overlay.Height-6;
                    Mouse(overlay,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,mouseX,mouseY,0));Mouse(overlay,"OnMouseMove",new MouseEventArgs(MouseButtons.Left,0,mouseX+40,mouseY+40,0));Mouse(overlay,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,mouseX+40,mouseY+40,0));
                    Check(overlay.Width>oldWidth && AppPreferences.Load(file).OverlaySize>320,"resize handle updates and persists minimap size");Save(overlay,Path.Combine(reports,"minimap-positioning.png"));
                    Mouse(overlay,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,overlay.Width-16,15,0));
                    Check(!overlay.IsEditing && (GetWindowLong(overlay.Handle,-20)&0x20)!=0,"checkmark locks minimap back into click-through mode");
                    var hotkeys=Field<OverlayHotkeys>(f,"overlayHotkeys");toggleRegistered=hotkeys.ToggleAvailable;editRegistered=hotkeys.EditAvailable;
                    using(var conflict=new OverlayHotkeys(delegate{},delegate{}))Check(!conflict.ToggleAvailable && !conflict.EditAvailable && conflict.Status.Contains("zajęty"),"shortcut conflicts are reported without stealing an existing registration");
                    if(hotkeys.ToggleAvailable || hotkeys.BackupAvailable)SendMessage(hotkeys.Handle,0x312,new IntPtr(hotkeys.ToggleAvailable?0x4546:0x4548),IntPtr.Zero);
                    else {Console.WriteLine("SKIP native visibility chord: all visibility shortcuts are already registered; checking the settings action instead.");f.ToggleOverlay();}
                    Check(!overlay.Visible,"available visibility action hides overlay while main focus is irrelevant");
                    hotkeys.PollKeys();if(hotkeys.EditAvailable)SendMessage(hotkeys.Handle,0x312,new IntPtr(0x4547),IntPtr.Zero);
                    else {Console.WriteLine("SKIP native positioning chord: shortcut is already registered; checking the settings action instead.");f.ToggleOverlayEditing();}
                    Check(overlay.Visible && overlay.IsEditing,"positioning action can restore a hidden overlay");overlay.SetEditing(false);
                    f.ShowSettings();var settings=(SettingsForm)f.SettingsWindow;settings.ShowOverlayPage();
                    Check(Find<CheckBox>(settings,"Włącz minimapę").Checked,"settings reflect shortcut visibility changes");
                    Find<TrackBar>(settings,"Przybliżenie minimapy").Value=250;Find<TrackBar>(settings,"Przezroczystość minimapy").Value=35;
                    Check(AppPreferences.Load(file).OverlayZoom==250 && Math.Abs(overlay.Opacity-.65)<.001,"settings change minimap zoom and transparency immediately");
                    Find<ComboBox>(settings,"Położenie minimapy").SelectedIndex=0;saved=AppPreferences.Load(file);Check(!saved.OverlayCustomPosition && saved.OverlayCorner=="top-left","corner selector resets custom placement");
                    Save(settings,Path.Combine(reports,"settings-minimap-pl.png"));
                    Find<ComboBox>(settings,"Język / Language").SelectedIndex=1;
                    Check(Find<Button>(settings,"Minimap settings").Text=="Minimap" && Find<CheckBox>(settings,"Enable minimap").Text=="Show minimap above the game","minimap settings switch to English");
                    Check(overlay.AccessibleName=="In-game minimap","overlay language switches live");Save(overlay,Path.Combine(reports,"minimap-en.png"));Save(settings,Path.Combine(reports,"settings-minimap-en.png"));
                    Find<Button>(settings,"Adjust minimap placement").PerformClick();Check(overlay.IsEditing,"settings provide a shortcut-independent editing action");Find<Button>(settings,"Adjust minimap placement").PerformClick();Check(!overlay.IsEditing,"settings action locks the minimap");
                    Position original=source.Position;source.SetPosition(null);overlay.Pulse(null);Save(overlay,Path.Combine(reports,"no-position.png"));Check(!source.MiniMapHasPosition,"no screenshot gives an explicit empty minimap state");
                    source.SetPosition(new Position("outside",DateTime.Now,99999,0,99999,0,true,2));overlay.Pulse(DateTime.UtcNow);Check(!source.MiniMapHasPosition,"wrong map does not show a misleading centered position");source.SetPosition(original);
                    saved=AppPreferences.Load(file);Check(saved.Theme=="night" && saved.MapOpacity==69 && saved.MarkerSize==80 && !saved.DeleteScreenshots && saved.ScreenshotFolder==shots,"minimap changes preserve prior appearance and screenshot choices");
                    settings.Close();Check(File.Exists(shot),"isolated test respects the deletion opt-out");
                    f.Close();Check(overlay.IsDisposed,"closing main application disposes minimap");Check(hotkeys.Handle==IntPtr.Zero,"closing main application releases the hotkey window");
                }
                using(var again=new OverlayHotkeys(delegate{},delegate{}))Check(again.ToggleAvailable==toggleRegistered && again.EditAvailable==editRegistered,"registered shortcuts are released for the next process");
                using(var reopened=new MainForm(null,null,0,false,true,null,cache,false,false,"ground-zero"))
                {reopened.Show();Wait(()=>reopened.MiniMap.Visible,"restart restores enabled minimap");Check(Math.Abs(reopened.MiniMap.Opacity-.65)<.001,"restart restores overlay transparency");reopened.Close();}
                string cleanCache=Path.Combine(root,"cleanup-cache"),cleanShots=Path.Combine(root,"cleanup-shots");Directory.CreateDirectory(cleanShots);
                new AppPreferences {ScreenshotFolder=cleanShots,DeleteScreenshots=true,OverlayEnabled=true}.Save(Path.Combine(cleanCache,"app-preferences.json"));
                using(var cleanup=new MainForm(null,null,0,false,false,null,cleanCache,false,false,"ground-zero"))
                {
                    cleanup.Show();MapCanvas canvas=All(cleanup).OfType<MapCanvas>().Single();Wait(()=>canvas.Layer!=null,"cleanup test map loads");
                    cleanup.WindowState=FormWindowState.Minimized;Pump(60);int beforePaint=cleanup.MiniMap.SnapshotCount;
                    string first=Path.Combine(cleanShots,"2026-10-28[12-34]_200.00, 2.30, 150.00_0.0, 0.70711, 0.0, 0.70711_16.60 (1).png");File.WriteAllBytes(first,new byte[]{1,2,3});
                    Wait(()=>canvas.Position!=null && !File.Exists(first),"visible minimap acknowledges and cleans a screenshot while main is minimized");
                    Check(cleanup.MiniMap.SnapshotCount>beforePaint,"minimap completed a fresh repaint before screenshot acknowledgement");
                    cleanup.ToggleOverlay();
                    string second=Path.Combine(cleanShots,"2026-10-28[12-34]_201.00, 2.30, 151.00_0.0, 0.70711, 0.0, 0.70711_16.60 (2).png");File.WriteAllBytes(second,new byte[]{1,2,3});
                    Wait(()=>canvas.Position!=null && canvas.Position.Counter==2,"hidden views still receive the latest position");Pump(400);
                    Check(File.Exists(second),"screenshot remains when neither main map nor minimap is visible");
                    cleanup.ToggleOverlay();Wait(()=>!File.Exists(second),"showing the minimap displays and then acknowledges the retained screenshot");cleanup.Close();
                }
                Console.WriteLine("PASS: "+checks+" overlay checks: shared rendering, settings, placement, Windows styles, click-through, activation, hotkeys, minimizing, persistence and disposal.");return 0;
            }
            catch(Exception ex){Console.WriteLine(ex);return 1;}
        }
    }
}
