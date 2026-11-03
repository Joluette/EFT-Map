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
    static class UiInteractionTests
    {
        static int checks;
        static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,Fields).GetValue(target);}
        static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(Control n in All(c))yield return n;}}
        static T Find<T>(Control root,string name)where T:Control{return All(root).OfType<T>().Single(c=>c.AccessibleName==name);}
        static void Pump(int ms){var t=Stopwatch.StartNew();while(t.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(2);}}
        static void Wait(Func<bool> ready,string name){var t=Stopwatch.StartNew();while(!ready() && t.ElapsedMilliseconds<15000)Pump(5);Check(ready(),name);}
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")]static extern bool GetLayeredWindowAttributes(IntPtr window,out uint key,out byte alpha,out uint flags);
        [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll")]static extern IntPtr ChildWindowFromPointEx(IntPtr parent,Point point,uint flags);
        static void Alpha(Form form,double value,string name)
        {
            uint key,flags;byte alpha;bool layered=GetLayeredWindowAttributes(form.Handle,out key,out alpha,out flags);
            if(!layered && (GetWindowLong(form.Handle,-20)&0x80000)==0)alpha=255;
            Check(Math.Abs(alpha-value*255)<=1 && Math.Abs(form.Opacity-value)<.001,name+"; native alpha="+alpha+", property="+form.Opacity);
        }
        static void Wheel(Control target,int delta){SendMessage(target.Handle,0x20A,(IntPtr)((long)(ushort)(short)delta<<16),IntPtr.Zero);}
        static bool SettingsCoverHost(SettingsForm settings,Panel host)
        {
            Rectangle visible=Rectangle.Intersect(host.Bounds,host.Parent.ClientRectangle);
            if(visible.Width<=host.Padding.Horizontal || visible.Height==0)return true;
            visible.X+=host.Padding.Left;visible.Width-=host.Padding.Horizontal;
            Rectangle content=settings.Bounds;content.Offset(host.Location);
            return content.Contains(visible);
        }
        static bool SettingsInFront(Panel host)
        {
            Rectangle visible=Rectangle.Intersect(host.Bounds,host.Parent.ClientRectangle);
            return visible.Width<=host.Padding.Horizontal || visible.Height==0 ||
                ChildWindowFromPointEx(host.Parent.Handle,new Point(visible.Left+visible.Width/2,visible.Top+visible.Height/2),1)==host.Handle;
        }
        [STAThread]static int Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);UiText.Language="pl";
            try
            {
                string root=Path.GetFullPath(args[0]),reports=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);Directory.CreateDirectory(reports);
                string cache=Path.Combine(root,"cache"),file=Path.Combine(cache,"app-preferences.json");
                using(var canvas=new MapCanvas())using(var fresh=new MiniMapOverlay(canvas,new AppPreferences {Language="pl",OverlayOpacity=100}))
                {GC.KeepAlive(fresh.Handle);Alpha(fresh,1,"fresh minimap initializes native opacity at 100%");}
                new AppPreferences {Language="pl",WindowOpacity=55,FullscreenOpacityOnly=false,OverlayOpacity=65,OverlayEnabled=true,AutoMapEnabled=false,DeleteScreenshots=false,BackgroundMode="plain"}.Save(file);
                using(var form=new MainForm(null,null,0,false,false,null,cache,false,false,"ground-zero"))
                {
                    var keys=Field<OverlayHotkeys>(form,"overlayHotkeys");keys.Dispose();typeof(MainForm).GetField("overlayHotkeys",Fields).SetValue(form,null);
                    form.Show();var map=Field<MapCanvas>(form,"canvas");Wait(()=>map.Layer!=null && form.SettingsPreparationCount==1,"map and settings prepare at startup");Pump(25);
                    Alpha(form,.55,"window applies opacity before opening settings");
                    int fullMapWidth=map.Width;var clock=Stopwatch.StartNew();form.ShowSettings();long open=clock.ElapsedMilliseconds;var settings=(SettingsForm)form.SettingsWindow;
                    var host=Field<Panel>(form,"settingsHost");
                    Check(SettingsCoverHost(settings,host),"the first opening frame exposes no empty settings background");
                    Check(map.Width==fullMapWidth,"the map remains under the entering panel before the first animation tick");
                    Check(form.IsSettingsAnimating,"opening starts an asynchronous transition");
                    bool covered=true,inFront=true;var left=new List<int>();while(form.IsSettingsAnimating && clock.ElapsedMilliseconds<1500){left.Add(host.Left);covered&=SettingsCoverHost(settings,host);inFront&=SettingsInFront(host);Pump(10);}
                    Check(covered,"every sampled opening frame contains settings rather than an empty placeholder");
                    Check(inFront,"native window order places the entering settings in front of the map");
                    Check(left.Distinct().Count()>2,"slide transition renders intermediate positions");
                    Check(settings.Left==host.Padding.Left && host.Right==host.Parent.ClientSize.Width && map.Right==host.Left,"opening ends with the panel beside the map");
                    Console.WriteLine("Prepared settings open: "+open+" ms; animation samples="+left.Count+", distinct positions="+left.Distinct().Count());
                    Alpha(form,.55,"opening settings does not force opacity to 100%");
                    settings.ShowWindowPage();var transparency=Find<TrackBar>(settings,"Przezroczystość okna");long scene=map.SceneRevision;transparency.Value=60;
                    Alpha(form,.4,"window slider updates native window transparency immediately");
                    Check(map.SceneRevision==scene,"window opacity does not rebuild the map scene");
                    Find<CheckBox>(settings,"Przezroczystość tylko na pełnym ekranie").Checked=true;transparency.Value=50;
                    Alpha(form,.5,"fullscreen-only setting allows a live adjustment preview");
                    settings.Close();Check(form.SettingsWindow==null && form.IsSettingsAnimating,"closing starts its own transition");
                    Check(map.Width==fullMapWidth && SettingsCoverHost(settings,host),"closing immediately restores the map behind the departing settings");
                    covered=true;inFront=true;clock.Restart();while(form.IsSettingsAnimating && clock.ElapsedMilliseconds<1500){covered&=SettingsCoverHost(settings,host);inFront&=SettingsInFront(host);Pump(10);}
                    Check(!form.IsSettingsAnimating && covered && !host.Visible && map.Width==fullMapWidth,"closing leaves no empty settings background");
                    Check(inFront,"native window order keeps departing settings in front of the expanded map");
                    Alpha(form,1,"after preview, fullscreen-only mode restores the opaque normal window");
                    Check(!settings.IsDisposed,"closing retains the prepared settings controls");
                    form.SetFullScreen(true);Alpha(form,.5,"fullscreen applies the saved window opacity");form.SetFullScreen(false);
                    form.ShowSettings();Wait(()=>!form.IsSettingsAnimating,"reopening animation completes");Check(Object.ReferenceEquals(settings,form.SettingsWindow) && form.SettingsPreparationCount==1,"reopening reuses one settings panel");
                    settings.ShowWindowPage();Find<CheckBox>(settings,"Przezroczystość tylko na pełnym ekranie").Checked=false;
                    var page=Field<FlowLayoutPanel>(settings,"windowPage");page.AutoScrollPosition=Point.Empty;transparency.Focus();int before=transparency.Value;
                    Wheel(transparency,-120);Wheel(transparency,120);Check(transparency.Value==before,"focused slider ignores native mouse-wheel value changes");
                    page.AutoScrollPosition=Point.Empty;Wheel(transparency,-120);Check(page.AutoScrollPosition.Y<0,"wheel over a slider scrolls the settings page");
                    string beforeWheel=File.ReadAllText(file);
                    foreach(var slider in All(settings).OfType<TrackBar>()){int value=slider.Value;Wheel(slider,120);Wheel(slider,-120);Check(slider.Value==value,"wheel preserves slider: "+slider.AccessibleName);}
                    foreach(var combo in All(settings).OfType<ComboBox>()){int value=combo.SelectedIndex;Wheel(combo,120);Wheel(combo,-120);Check(combo.SelectedIndex==value,"wheel preserves selector: "+combo.AccessibleName);}
                    settings.ShowOverlayPage();var corner=Find<ComboBox>(settings,"Położenie minimapy");int selected=corner.SelectedIndex;corner.DroppedDown=true;Pump(20);Wheel(corner,-120);Check(corner.SelectedIndex==selected,"wheel inside an open selector does not commit a different setting");corner.DroppedDown=false;
                    Check(File.ReadAllText(file)==beforeWheel,"scrolling does not write changed settings to disk");
                    var opacity=Find<TrackBar>(settings,"Przezroczystość minimapy");
                    foreach(bool editing in new[]{false,true,false})
                    {
                        form.MiniMap.SetEditing(editing);
                        foreach(int value in new[]{0,70,35,0,60}){opacity.Value=value;Alpha(form.MiniMap,(100-value)/100.0,"minimap native opacity updates; editing="+editing+", transparency="+value);}
                        Check(((GetWindowLong(form.MiniMap.Handle,-20)&0x20)==0)==editing,"opacity changes preserve click-through/editing style");
                    }
                    form.ToggleOverlay();opacity.Value=45;form.ToggleOverlay();Alpha(form.MiniMap,.55,"hidden minimap uses the new opacity after showing again");
                    var saved=AppPreferences.Load(file);Check(saved.WindowOpacity==50 && saved.OverlayOpacity==55,"both opacity values persist to the private cache");
                    for(int i=0;i<8;i++){settings.Close();Pump(10);form.ShowSettings();Pump(10);}Wait(()=>!form.IsSettingsAnimating,"rapid toggle sequence completes");Check(form.SettingsPreparationCount==1 && form.SettingsWindow==settings,"rapid toggles reuse the panel and end in a stable state");
                    settings.Close();Pump(25);form.ClientSize=new Size(1000,680);Check(SettingsCoverHost(settings,host),"resizing during closing keeps the panel background covered");
                    form.ShowSettings();Pump(25);form.ClientSize=new Size(1250,760);Check(SettingsCoverHost(settings,host),"resizing during reopening keeps the panel background covered");Wait(()=>!form.IsSettingsAnimating,"resized transition completes");
                    Check(map.Right==host.Left && host.Right==host.Parent.ClientSize.Width,"resized settings settle beside the map");
                    using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(reports,"settings-opacity.png"));}
                    form.Close();Check(form.IsDisposed && settings.IsDisposed,"closing the app disposes the cached panel and transition timer");
                }
                Console.WriteLine("PASS: "+checks+" interaction checks: native opacity, wheel protection, cached settings, animation and lifetime.");return 0;
            }
            catch(Exception error){Console.WriteLine(error);return 1;}
        }
    }
}
