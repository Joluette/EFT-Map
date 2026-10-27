// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace EftPosition
{
    static class CaptionTests
    {
        static int checks;
        static void Check(bool test,string message){checks++;if(!test)throw new Exception(message);}
        static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(var child in All(c))yield return child;}}
        static T Find<T>(Control root,string name)where T:Control{return All(root).OfType<T>().Single(c=>c.AccessibleName==name);}
        static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(3);}}
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")]static extern IntPtr GetSystemMenu(IntPtr window,bool revert);
        static int Hit(Form f,Point screen)
        {return SendMessage(f.Handle,0x84,IntPtr.Zero,(IntPtr)((screen.Y<<16)|(screen.X&0xffff))).ToInt32();}
        static int CaptionPixel(Control bar)
        {using(var image=new Bitmap(bar.Width,bar.Height)){bar.DrawToBitmap(image,bar.ClientRectangle);return image.GetPixel(bar.Width/2,2).ToArgb();}}
        static void SaveCaption(Control bar,string file)
        {using(var image=new Bitmap(bar.Width,bar.Height)){bar.DrawToBitmap(image,bar.ClientRectangle);image.Save(file);}}
        [STAThread]static int Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string root=Path.GetFullPath(args[0]),reports=Path.GetFullPath(args[1]);Check(!Directory.Exists(root),"fresh isolated preferences");Directory.CreateDirectory(reports);
                using(var f=new MainForm(null,null,0,false,false,null,root,false,false,"ground-zero"))
                {
                    f.Show();Pump(180);
                    var bar=Find<Control>(f,"Pasek tytułu okna");var map=All(f).OfType<MapCanvas>().Single();
                    Check(bar.Visible && bar.Top==0 && bar.Height>=30,"caption is visible at the top of the client area");
                    Point origin=f.PointToScreen(Point.Empty);
                    Check(origin.Y-f.Top<bar.Height/2,"native caption has been reclaimed, without a duplicate white strip");
                    Check(map.Top>=bar.Bottom || map.Parent.Top>=bar.Bottom || map.PointToScreen(Point.Empty).Y>=bar.PointToScreen(new Point(0,bar.Height)).Y,"caption reserves space above the map");
                    Check(Hit(f,bar.PointToScreen(new Point(200,bar.Height/2)))==2,"caption background uses native dragging/double-click hit testing");
                    Check(Hit(f,bar.PointToScreen(new Point(16,bar.Height/2)))==3,"icon retains native system-menu hit testing");
                    var maximize=Find<Button>(bar,"Maksymalizuj okno");var minimize=Find<Button>(bar,"Minimalizuj okno");
                    Point buttonPoint=maximize.PointToScreen(new Point(maximize.Width/2,maximize.Height/2));
                    int buttonHit=Hit(f,buttonPoint);
                    Check(buttonHit==1,"custom caption buttons receive client clicks (hit="+buttonHit+", button="+maximize.Bounds+", bar="+bar.Bounds+", local="+f.PointToClient(buttonPoint)+")");
                    Check(Hit(f,new Point(f.Left+1,f.Top+f.Height/2))==10,"left native sizing edge remains available");
                    Check(Hit(f,new Point(f.Left+f.Width/2,f.Top+1))==12,"top native sizing edge remains available");
                    var normal=f.Bounds;maximize.PerformClick();Pump(70);
                    Check(f.WindowState==FormWindowState.Maximized,"maximize caption button maximizes");
                    Rectangle area=Screen.FromControl(f).WorkingArea;
                    Check(area.Contains(f.PointToScreen(new Point(0,bar.Height))),"maximized caption remains in the monitor work area");
                    Check(Find<Button>(bar,"Przywróć rozmiar okna").Visible,"maximized window exposes restore action");
                    Find<Button>(bar,"Przywróć rozmiar okna").PerformClick();Pump(70);
                    Check(f.WindowState==FormWindowState.Normal && f.Bounds==normal,"restore returns the previous window bounds (before="+normal+", after="+f.Bounds+", restore="+f.RestoreBounds+")");
                    Point doubleClick=bar.PointToScreen(new Point(200,bar.Height/2));IntPtr location=(IntPtr)((doubleClick.Y<<16)|(doubleClick.X&0xffff));
                    SendMessage(f.Handle,0xA3,(IntPtr)2,location);Pump(50);Check(f.WindowState==FormWindowState.Maximized,"native caption double-click maximizes");
                    doubleClick=bar.PointToScreen(new Point(200,bar.Height/2));location=(IntPtr)((doubleClick.Y<<16)|(doubleClick.X&0xffff));
                    SendMessage(f.Handle,0xA3,(IntPtr)2,location);Pump(50);Check(f.WindowState==FormWindowState.Normal && f.Bounds==normal,"native caption double-click restores original bounds");
                    Check(GetSystemMenu(f.Handle,false)!=IntPtr.Zero,"native system menu remains available");
                    minimize.PerformClick();Pump(50);Check(f.WindowState==FormWindowState.Minimized,"minimize button minimizes");f.WindowState=FormWindowState.Normal;Pump(70);
                    f.ShowSettings();var settings=(SettingsForm)f.SettingsWindow;var settingsBar=Find<Control>(settings,"Pasek tytułu okna");
                    Check(settingsBar.Visible && All(settingsBar).OfType<Button>().Count(b=>b.Visible)==1,"settings caption has only its close button");
                    ComboBox theme=Find<ComboBox>(settings,"Motyw kolorystyczny");
                    var colors=new[]{Color.FromArgb(25,29,31),Color.FromArgb(20,30,38),Color.FromArgb(28,33,28)};
                    for(int i=0;i<3;i++)
                    {
                        theme.SelectedIndex=i;Pump(50);
                        Check(CaptionPixel(bar)==colors[i].ToArgb(),"main caption pixels follow theme "+i);
                        Check(CaptionPixel(settingsBar)==colors[i].ToArgb(),"settings caption pixels follow theme "+i);
                        SaveCaption(bar,Path.Combine(reports,"caption-theme-"+i+".png"));
                    }
                    Check(f.Text.Contains("Ground Zero"),"window title retains current map name");
                    f.SetFullScreen(true);Pump(70);Check(!bar.Visible && map.Height==f.ClientSize.Height,"F11 hides caption and returns its space to the map");
                    theme.SelectedIndex=1;Pump(50);f.SetFullScreen(false);Pump(70);
                    Check(bar.Visible && CaptionPixel(bar)==colors[1].ToArgb(),"returning from F11 restores the current theme");
                    Check(f.Bounds==normal,"F11 retains the window's original bounds");
                    Find<Button>(settingsBar,"Zamknij okno").PerformClick();Check(f.SettingsWindow==null,"settings caption closes settings normally");
                    Find<Button>(bar,"Zamknij okno").PerformClick();Check(f.IsDisposed,"main caption closes the application normally");
                }
                using(var f=new MainForm(null,null,0,false,false,null,root,false,false,"ground-zero"))
                {f.Show();Pump(120);Check(CaptionPixel(Find<Control>(f,"Pasek tytułu okna"))==Color.FromArgb(20,30,38).ToArgb(),"restart applies the saved theme to the first caption");f.Close();}
                Console.WriteLine("PASS: "+checks+" caption checks: theme pixels, settings, hit testing, resize edges, minimize/maximize/restore, fullscreen, close and restart.");return 0;
            }
            catch(Exception ex){Console.WriteLine(ex);return 1;}
        }
    }
}
