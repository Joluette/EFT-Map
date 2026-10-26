// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace EftPosition
{
    static class UITests
    {
        static int checks;
        static void Check(bool ok,string message) { checks++; if(!ok) throw new Exception(message); }
        static IEnumerable<Control> Descend(Control c)
        { foreach(Control child in c.Controls) { yield return child; foreach(Control nested in Descend(child)) yield return nested; } }
        static Button Button(MainForm f,string text)
        { foreach(Control c in Descend(f)) { Button b=c as Button; if(b!=null && b.Text==text) return b; } throw new Exception("Missing button "+text); }
        static float Field(MapCanvas c,string name)
        { return (float)typeof(MapCanvas).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c); }
        static void Mouse(MapCanvas c,string method,MouseEventArgs args)
        { typeof(MapCanvas).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{args}); }
        static void WaitFor(Func<bool> predicate,string message)
        {
            DateTime until=DateTime.UtcNow.AddSeconds(15);
            while(!predicate() && DateTime.UtcNow<until) { Application.DoEvents(); Thread.Sleep(10); }
            Check(predicate(),message);
        }
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            string fixtures=Path.GetFullPath(args[0]), preview=Path.GetFullPath(args[1]);
            Directory.CreateDirectory(fixtures);
            using(File.Create(Path.Combine(fixtures,"2026-10-26[12-34]_200.00, 2.30, 150.00_0.0, 0.70711, 0.0, 0.70711 (0).png"))) { }
            Exception failure=null;
            string cache=args.Length>2?Path.GetFullPath(args[2]):Path.Combine(fixtures,"cache");
            using(MainForm form=new MainForm(fixtures,null,0,false,true,null,cache))
            using(System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer())
            {
                DateTime start=DateTime.UtcNow;
                timer.Interval=100;
                timer.Tick+=delegate
                {
                    try
                    {
                        MapCanvas map=null;
                        foreach(Control c in Descend(form)) if(c is MapCanvas) map=(MapCanvas)c;
                        if(map==null || map.Position==null || map.Layer==null)
                        { if((DateTime.UtcNow-start).TotalSeconds>10) throw new Exception("No marker after opening folder"); return; }
                        timer.Stop();
                        foreach(Control c in Descend(form)) if(c is CheckBox && c.AccessibleName=="Automatyczne wnętrza") ((CheckBox)c).Checked=false;
                        map.FitMap();
                        Check(map.Position.X==200 && map.Position.Z==150,"marker receives watched position");
                        float initial=Field(map,"scale");
                        Button(form,"+").PerformClick(); Check(Field(map,"scale")>initial,"zoom-in button");
                        Button(form,"−").PerformClick(); Check(Math.Abs(Field(map,"scale")-initial)<.001,"zoom-out button");
                        float before=Field(map,"offsetX");
                        Mouse(map,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,250,250,0));
                        Mouse(map,"OnMouseMove",new MouseEventArgs(MouseButtons.Left,0,310,280,0));
                        Mouse(map,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,310,280,0));
                        Check(Math.Abs(Field(map,"offsetX")-before-60)<.01,"drag moves map by expected pixels");
                        float preWheel=Field(map,"scale");
                        Mouse(map,"OnMouseWheel",new MouseEventArgs(MouseButtons.None,0,300,250,120));
                        Check(Field(map,"scale")>preWheel,"wheel zoom");
                        Button(form,"Do znacznika").PerformClick();
                        MapPoint p=CustomsMap.Project(200,150);
                        Check(Math.Abs(Field(map,"offsetX")+p.X*Field(map,"scale")-map.Width/2.0)<1,"center X");
                        Check(Math.Abs(Field(map,"offsetY")+p.Y*Field(map,"scale")-map.Height/2.0)<1,"center Y");
                        Button(form,"Cała mapa").PerformClick(); Check(Math.Abs(Field(map,"scale")-initial)<.001,"fit map");
                        foreach(Control c in Descend(form))
                        {
                            if(c is CheckBox && c.AccessibleName=="Śledź pozycję") { ((CheckBox)c).Checked=true; Check(map.Follow,"follow toggle"); ((CheckBox)c).Checked=false; }
                            if(c is Label && c.Text=="DANE PRZYKŁADOWE") Check(c.Right<=c.Parent.ClientSize.Width,"demo badge stays inside header");
                        }
                        form.ClientSize=new Size(950,650); Application.DoEvents();
                        Check(map.Width>500 && map.Height>400,"minimum-size layout leaves map visible");
                        form.ClientSize=new Size(1250,760); Application.DoEvents();
                        ComboBox mapMenu=null,levels=null;
                        foreach(Control c in Descend(form)) if(c is ComboBox)
                        { if(c.AccessibleName=="Wybór mapy") mapMenu=(ComboBox)c; else if(c.AccessibleName=="Wybór piętra") levels=(ComboBox)c; }
                        Check(mapMenu!=null && mapMenu.Items.Count==13,"all 13 maps in menu");
                        int loaded=0;
                        foreach(MapDefinition selected in MapCatalog.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets")))
                        {
                            for(int i=0;i<mapMenu.Items.Count;i++) if(((MapDefinition)mapMenu.Items[i]).Id==selected.Id) { mapMenu.SelectedIndex=i; break; }
                            if(selected.Layers[0].Remote && !File.Exists(MapAssets.CachedPath(".",cache,selected.Layers[0])))
                            { Console.WriteLine("SKIP uncached online map in offline GUI test: "+selected.Id); continue; }
                            WaitFor(delegate { return map.Definition!=null && map.Definition.Id==selected.Id; },"load "+selected.Id);
                            Check(levels.Items.Count==selected.Layers.Length,"level menu "+selected.Id);
                            Check(map.Position!=null && map.Position.X==200,"map switch preserves screenshot coordinates");
                            if(!selected.Layers[0].Remote)
                            {
                                for(int j=0;j<selected.Layers.Length;j++)
                                {
                                    if(selected.Layers[j].Remote && !File.Exists(MapAssets.CachedPath(".",cache,selected.Layers[j]))) continue;
                                    levels.SelectedIndex=j;
                                    string layerId=selected.Layers[j].Id;
                                    WaitFor(delegate { return map.Layer!=null && map.Layer.Id==layerId && map.Definition.Id==selected.Id; },"floor "+selected.Id+"/"+layerId);
                                }
                            }
                            loaded++;
                        }
                        Check(loaded>=10,"at least ten offline map graphics available");
                        // Rapid selection must finish on the last choice, even while a prior load is pending.
                        for(int i=0;i<25;i++) mapMenu.SelectedIndex=i%mapMenu.Items.Count;
                        for(int i=0;i<mapMenu.Items.Count;i++) if(((MapDefinition)mapMenu.Items[i]).Id=="customs") { mapMenu.SelectedIndex=i; break; }
                        WaitFor(delegate { return map.Definition!=null && map.Definition.Id=="customs"; },"rapid changes finish on last map");
                        using(Bitmap image=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new Rectangle(0,0,form.Width,form.Height)); image.Save(preview); }
                        Button(form,"Zatrzymaj").PerformClick(); Check(map.Position!=null,"stop keeps last marker");
                        Button(form,"Obserwuj").PerformClick();
                        Check(map.Position==null,"new watcher clears previous marker until fresh scan");
                        Console.WriteLine("PASS: "+checks+" GUI checks; real WinForms message loop and control events, no injected OS input.");
                        form.Close();
                    }
                    catch(Exception ex) { failure=ex; timer.Stop(); form.Close(); }
                };
                timer.Start(); Application.Run(form);
            }
            if(failure!=null) { Console.WriteLine(failure); return 1; }
            return 0;
        }
    }
}
