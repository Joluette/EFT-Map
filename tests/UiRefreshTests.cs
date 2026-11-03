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
    static class UiRefreshTests
    {
        static int checks;
        static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        static IEnumerable<Control> All(Control root){foreach(Control c in root.Controls){yield return c;foreach(Control n in All(c))yield return n;}}
        static T Find<T>(Control root,string name)where T:Control{return All(root).OfType<T>().Single(c=>c.AccessibleName==name);}
        static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
        static void Pump(int ms){var t=Stopwatch.StartNew();while(t.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(3);}}
        static void Wait(Func<bool> ready){var t=Stopwatch.StartNew();while(!ready() && t.ElapsedMilliseconds<15000)Pump(10);Check(ready(),"offline map loads");}
        static void Save(Control control,string file){using(var b=new Bitmap(control.Width,control.Height)){control.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(file);}}
        static void Key(MainForm form,Keys keys){object[] args={Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero),keys};typeof(MainForm).GetMethod("ProcessCmdKey",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,args);Pump(10);}
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        static IntPtr MousePoint(int x,int y){return (IntPtr)((y<<16)|(x&0xffff));}
        static Bitmap Row(UiComboBox combo,DrawItemState state)
        {
            var bitmap=new Bitmap(280,32);
            using(var g=Graphics.FromImage(bitmap))
                typeof(UiComboBox).GetMethod("OnDrawItem",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(combo,new object[]{new DrawItemEventArgs(g,combo.Font,new Rectangle(0,0,280,32),0,state)});
            return bitmap;
        }
        [STAThread]static int Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);UiText.Language="pl";
            try
            {
                string root=Path.GetFullPath(args[0]),reports=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);Directory.CreateDirectory(reports);
                string cache=Path.Combine(root,"cache"),prefsFile=Path.Combine(cache,"app-preferences.json");
                new AppPreferences {Language="pl",Theme="night",BackgroundMode="cyberpunk",BackgroundDim=0,MapOpacity=100,DeleteScreenshots=false,WindowOpacity=55,FullscreenOpacityOnly=false,OverlayEnabled=false}.Save(prefsFile);
                using(var form=new MainForm(null,null,0,false,true,null,cache,false,false,"ground-zero"))
                {
                    form.Show();var map=All(form).OfType<MapCanvas>().Single();Wait(()=>map.Layer!=null);
                    Check(Math.Abs(form.Opacity-.55)<.01,"saved window opacity is applied before opening settings");
                    int mapWidth=map.Width;form.ShowSettings();Pump(40);var settings=(SettingsForm)form.SettingsWindow;
                    Wait(()=>!form.IsSettingsAnimating);
                    Check(!settings.TopLevel && settings.Parent!=null && settings.TopLevelControl==form,"settings belong to the main window");
                    Check(form.OwnedForms.Length==0,"settings create no owned top-level window");
                    Check(map.Width<mapWidth && map.Width>350,"settings reserve space beside a usable map");
                    Check(Math.Abs(form.Opacity-.55)<.001 && Field<AppPreferences>(form,"preferences").WindowOpacity==55,"opening settings respects the selected window opacity");
                    Check(All(settings).OfType<UiSlider>().Count()==7,"all seven sliders use the modern control");
                    var slider=Find<TrackBar>(settings,"Krycie mapy");int y=slider.Height/2;
                    SendMessage(slider.Handle,0x201,(IntPtr)1,MousePoint(12,y));SendMessage(slider.Handle,0x202,IntPtr.Zero,MousePoint(12,y));
                    Check(slider.Value==slider.Minimum,"click reaches the minimum through native mouse messages");
                    SendMessage(slider.Handle,0x201,(IntPtr)1,MousePoint(slider.Width-12,y));SendMessage(slider.Handle,0x200,(IntPtr)1,MousePoint(slider.Width/2,y));SendMessage(slider.Handle,0x202,IntPtr.Zero,MousePoint(slider.Width/2,y));
                    Check(Math.Abs(slider.Value-(slider.Minimum+slider.Maximum)/2)<=1,"drag updates values throughout the track");
                    int before=slider.Value;SendMessage(slider.Handle,0x100,(IntPtr)Keys.Right,IntPtr.Zero);SendMessage(slider.Handle,0x101,(IntPtr)Keys.Right,IntPtr.Zero);
                    Check(slider.Value==before+slider.SmallChange,"keyboard right arrow changes the value once");
                    Check(Field<AppPreferences>(form,"preferences").MapOpacity==slider.Value,"slider still applies the preference immediately");
                    before=slider.Value;typeof(UiSlider).GetMethod("OnMouseWheel",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(slider,new object[]{new HandledMouseEventArgs(MouseButtons.None,0,100,y,120)});
                    Check(slider.Value==before,"focused slider ignores value changes from mouse wheel");
                    All(settings).OfType<Button>().Single(b=>b.Text=="Gotowe").Focus();var appearance=Field<FlowLayoutPanel>(settings,"appearance");appearance.AutoScrollPosition=Point.Empty;before=slider.Value;
                    typeof(UiSlider).GetMethod("OnMouseWheel",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(slider,new object[]{new HandledMouseEventArgs(MouseButtons.None,0,100,y,-120)});
                    Check(slider.Value==before && appearance.AutoScrollPosition.Y<0,"wheel over an unfocused slider scrolls settings without changing the value");
                    slider.Value=100;Field<FlowLayoutPanel>(settings,"appearance").AutoScrollPosition=Point.Empty;Save(form,Path.Combine(reports,"settings-integrated.png"));
                    form.Size=form.MinimumSize;Pump(30);
                    var done=All(settings).OfType<Button>().Single(b=>b.Text=="Gotowe");
                    Check(settings.RectangleToScreen(settings.ClientRectangle).Contains(done.RectangleToScreen(done.ClientRectangle)),"Done remains visible at the minimum window size");
                    Check(map.Width>350,"minimum window preserves map space");Save(form,Path.Combine(reports,"settings-minimum.png"));
                    form.ClientSize=new Size(1250,760);Pump(20);
                    settings.ShowWindowPage();Save(form,Path.Combine(reports,"settings-window.png"));
                    settings.ShowOverlayPage();Save(form,Path.Combine(reports,"settings-minimap.png"));
                    settings.ShowCapturePage();Check(Find<TextBox>(settings,"Ścieżka folderu zrzutów").Visible,"screenshot controls remain inside settings");
                    Key(form,Keys.Escape);Check(form.SettingsWindow==null && !form.IsDisposed,"Escape closes settings only");
                    Wait(()=>!form.IsSettingsAnimating);
                    Check(Math.Abs(form.Opacity-.55)<.01 && map.Width==form.ClientSize.Width,"closing settings restores opacity and map width");
                    for(int i=0;i<5;i++){form.ShowSettings();settings=(SettingsForm)form.SettingsWindow;settings.ShowCapturePage();Check(!Find<TextBox>(settings,"Ścieżka folderu zrzutów").IsDisposed,"capture controls survive reopen "+i);settings.Close();}
                    form.SetFullScreen(true);form.ShowSettings();Key(form,Keys.Escape);Check(form.IsFullScreen && form.SettingsWindow==null,"first Escape closes settings within fullscreen");Key(form,Keys.Escape);Check(!form.IsFullScreen,"second Escape exits fullscreen");
                    using(var combo=new UiComboBox {Width=280})
                    {
                        combo.Items.Add("Poziom bazowy");combo.SelectedIndex=0;
                        using(var plain=Row(combo,DrawItemState.Selected))using(var focused=Row(combo,DrawItemState.Selected|DrawItemState.Focus))
                        {
                            bool same=true;for(int px=0;px<plain.Width;px++)for(int py=0;py<plain.Height;py++)if(plain.GetPixel(px,py)!=focused.GetPixel(px,py))same=false;
                            Check(same,"selected option has no native dotted focus frame");focused.Save(Path.Combine(reports,"selected-option.png"));
                            Check(focused.GetPixel(0,0)==combo.BackColor && focused.GetPixel(140,16)!=combo.BackColor,"selected row has inset rounded highlighting");
                        }
                    }
                    form.ShowSettings();form.Close();Check(form.IsDisposed,"main window closes safely with settings open");
                }
                Check(AppPreferences.Load(prefsFile).WindowOpacity==55,"preview does not overwrite stored preferences");
                Console.WriteLine("PASS: "+checks+" UI refresh checks: embedded settings, lifetime, layout, modern slider input, keyboard navigation and focus rendering.");return 0;
            }
            catch(Exception error){Console.WriteLine(error);return 1;}
        }
    }
}
