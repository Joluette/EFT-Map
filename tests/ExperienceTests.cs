// SPDX-License-Identifier: GPL-3.0-only
// Integration tests use the real WinForms loop and synthetic files only.
// No system keyboard/mouse input is injected and EFT is never started.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace EftPosition
{
    static class ExperienceTests
    {
        static int checks,counter;
        static Exception failure;
        static MainForm form;
        static MapCanvas canvas;
        static string fixtures,reports;

        static void Check(bool ok,string message)
        { checks++;if(!ok) throw new Exception("FAIL: "+message); }
        static void Near(double actual,double expected,double tolerance,string message)
        { Check(Math.Abs(actual-expected)<=tolerance,message+" (actual="+actual+", expected="+expected+")"); }
        static IEnumerable<Control> Descendants(Control c)
        {
            foreach(Control child in c.Controls)
            { yield return child;foreach(Control inner in Descendants(child)) yield return inner; }
        }
        static float Field(string name)
        {
            FieldInfo field=typeof(MapCanvas).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance);
            if(field==null) throw new Exception("Missing canvas field: "+name);
            return (float)field.GetValue(canvas);
        }
        static void WaitFor(Func<bool> test,string message,int milliseconds)
        {
            DateTime end=DateTime.UtcNow.AddMilliseconds(milliseconds);
            while(!test() && DateTime.UtcNow<end)
            {
                if(failure!=null) throw new Exception("An unhandled GUI exception occurred.",failure);
                if(form.IsDisposed) throw new Exception("Window closed while waiting for: "+message);
                Application.DoEvents();Thread.Sleep(10);
            }
            Check(test(),message);
        }
        static void WaitFor(Func<bool> test,string message) {WaitFor(test,message,20000);}
        static string WritePosition(double x,double y,double z)
        {
            int index=++counter;
            string name=String.Format(CultureInfo.InvariantCulture,
                "2026-10-26[12-34]_{0:F2}, {1:F2}, {2:F2}_0.00000, 0.70711, 0.00000, 0.70711 ({3}).png",x,y,z,index);
            string path=Path.Combine(fixtures,name);
            // A deliberately empty body proves position updates do not decode PNGs.
            using(File.Create(path)) { }
            File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddMilliseconds(index));
            return name;
        }
        static void Update(double x,double y,double z,string layer)
        {
            string name=WritePosition(x,y,z);
            WaitFor(delegate {
                return canvas.Position!=null && canvas.Position.FileName==name && canvas.Layer!=null && canvas.Layer.Id==layer;
            },"new screenshot reaches layer "+layer);
            // Let the Tick which applies pending focus complete and paint once.
            Application.DoEvents();
        }
        static MapPoint CenterWorld()
        { return canvas.Layer.Unproject((canvas.Width/2.0-Field("offsetX"))/Field("scale"),(canvas.Height/2.0-Field("offsetY"))/Field("scale")); }
        static void Render(string filename)
        {
            string path=Path.Combine(reports,filename);
            using(Bitmap image=new Bitmap(form.Width,form.Height,PixelFormat.Format32bppArgb))
            {
                form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));
                image.Save(path,ImageFormat.Png);
                // Inspect the actual map pixels, excluding all window chrome and
                // a generous circle around the marker. A file-size assertion alone
                // could pass for an empty green map surrounded by detailed UI.
                Point clientScreen=form.PointToScreen(Point.Empty);
                Point mapScreen=canvas.PointToScreen(Point.Empty);
                int left=mapScreen.X-form.Left,top=mapScreen.Y-form.Top;
                Rectangle region=Rectangle.Intersect(new Rectangle(left+12,top+12,Math.Max(1,canvas.Width-24),Math.Max(1,canvas.Height-54)),new Rectangle(0,0,image.Width,image.Height));
                MapPoint marker=canvas.Layer.Project(canvas.Position.X,canvas.Position.Z);
                double markerX=left+Field("offsetX")+marker.X*Field("scale"),markerY=top+Field("offsetY")+marker.Y*Field("scale");
                HashSet<int> colors=new HashSet<int>();int edges=0;
                for(int y=region.Top;y<region.Bottom;y+=3)
                {
                    int previous=0;bool hasPrevious=false;
                    for(int x=region.Left;x<region.Right;x+=3)
                    {
                        if((x-markerX)*(x-markerX)+(y-markerY)*(y-markerY)<64*64) {hasPrevious=false;continue;}
                        int pixel=image.GetPixel(x,y).ToArgb();colors.Add(pixel);
                        if(hasPrevious && pixel!=previous) edges++;
                        previous=pixel;hasPrevious=true;
                    }
                }
                Console.WriteLine(filename+": map detail colors="+colors.Count+", edge transitions="+edges);
                Check(colors.Count>8,"render contains real map detail beyond marker/UI: "+filename);
                Check(edges>100,"render contains interior lines and edges: "+filename);
            }
            Check(new FileInfo(path).Length>10000,"saved a nontrivial render: "+filename);
        }
        static bool Command(Keys key)
        {
            MethodInfo method=typeof(MainForm).GetMethod("ProcessCmdKey",BindingFlags.NonPublic|BindingFlags.Instance);
            if(method==null) throw new Exception("Missing ProcessCmdKey override.");
            Message message=Message.Create(form.Handle,0x0100,(IntPtr)(int)key,IntPtr.Zero);
            object[] values={message,key};
            return (bool)method.Invoke(form,values);
        }
        static CheckBox AutoBox()
        {
            foreach(Control c in Descendants(form))
                if(c is CheckBox && c.AccessibleName=="Automatyczne wnętrza") return (CheckBox)c;
            throw new Exception("Missing checkbox: Automatyczne wnętrza");
        }
        static void RunChecks()
        {
            Check(canvas.Definition.Id=="customs","default location is Customs");
            Check(canvas.IsVector,"Customs uses the vector renderer");
            Check(AutoBox().Checked,"automatic interior focus enabled by default");
            canvas.FitMap();float fit=Field("scale");
            Update(200,1,150,"base");
            Check(Field("scale")>fit*1.5,"entering Dorms automatically zooms beyond whole-map fit");
            MapPoint middle=CenterWorld();
            Near(middle.X,204,1,"Dorms footprint is horizontally centered");
            Near(middle.Y,157.5,1,"Dorms footprint is vertically centered");

            canvas.Zoom(1.13f);
            float manualScale=Field("scale"),manualX=Field("offsetX"),manualY=Field("offsetY");
            Update(201,1.1,151,"base");
            Near(Field("scale"),manualScale,.001,"same indoor context preserves manual zoom");
            Near(Field("offsetX"),manualX,.1,"same indoor context does not repeatedly snap X");
            Near(Field("offsetY"),manualY,.1,"same indoor context does not repeatedly snap Y");

            Update(200,3,150,"Second_Floor");
            Check(canvas.IsVector,"upper floor remains vector rendered");
            Check(Field("scale")>fit*1.5,"second floor remains focused on building");
            Update(200,6,150,"Third_Floor");
            Check(canvas.IsVector,"third floor remains vector rendered");
            Update(208,-1,-149,"Underground_Level");
            Check(Field("scale")>fit*2,"basement is automatically focused and enlarged");
            Update(300,1,220,"base");
            Near(Field("scale"),fit,Math.Max(.002,fit*.03),"leaving indoor context restores whole-map fit");

            AutoBox().Checked=false;
            Update(200,3,150,"base");
            Check(canvas.Layer.Id=="base","automatic mode can be disabled");
            AutoBox().Checked=true;
            Update(200,3.1,150,"Second_Floor");
            Check(canvas.Layer.Id=="Second_Floor","automatic floor selection resumes after re-enabling");

            // (200,150) is the courtyard between Dorms buildings. Move inside the
            // actual east building before judging high-zoom interior quality.
            Update(183,3.1,166,"Second_Floor");
            canvas.FocusArea(new WorldBounds(179,162,187,170));
            Check(Field("scale")>fit*10,"high zoom is available for a small indoor area");
            Check(canvas.IsVector,"high zoom uses vector geometry, not enlarged low-resolution PNG");
            Render("preview-v3-zoom.png");

            Rectangle savedBounds=form.Bounds;
            FormBorderStyle savedBorder=form.FormBorderStyle;
            FormWindowState savedState=form.WindowState;
            float savedScale=Field("scale");
            MapPoint savedCenter=CenterWorld();
            form.SetFullScreen(true);
            WaitFor(delegate{return form.IsFullScreen && canvas.MapOnly;},"fullscreen API activates map-only mode",5000);
            Check(form.FormBorderStyle==FormBorderStyle.None,"fullscreen has no title bar or window frame");
            Rectangle mapScreen=canvas.RectangleToScreen(canvas.ClientRectangle);
            Rectangle windowScreen=form.RectangleToScreen(form.ClientRectangle);
            Near(mapScreen.Left,windowScreen.Left,2,"fullscreen map reaches left edge");
            Near(mapScreen.Top,windowScreen.Top,2,"fullscreen map reaches top edge");
            Near(mapScreen.Width,windowScreen.Width,2,"fullscreen map fills width");
            Near(mapScreen.Height,windowScreen.Height,2,"fullscreen map fills height");
            Rectangle monitor=Screen.FromControl(form).Bounds;
            Near(windowScreen.Width,monitor.Width,2,"fullscreen reaches monitor width");
            Near(windowScreen.Height,monitor.Height,2,"fullscreen reaches monitor height");
            foreach(Control control in Descendants(form))
                if(control is Button || control is TextBox || control is ComboBox || control is CheckBox || control is Label)
                    Check(!control.Visible,"fullscreen hides UI control: "+control.GetType().Name+" "+control.Text);
            Near(Field("scale"),savedScale,.001,"entering fullscreen preserves zoom");
            MapPoint fullCenter=CenterWorld();
            Near(fullCenter.X,savedCenter.X,.2,"entering fullscreen preserves world center X");
            Near(fullCenter.Y,savedCenter.Y,.2,"entering fullscreen preserves world center Z");
            Render("preview-v3-fullscreen.png");
            Check(Command(Keys.Escape),"Escape command is handled by the form");
            WaitFor(delegate{return !form.IsFullScreen && !canvas.MapOnly;},"Escape leaves fullscreen",5000);
            Check(form.FormBorderStyle==savedBorder,"window border restored");
            Check(form.WindowState==savedState,"window state restored");
            Near(form.Bounds.X,savedBounds.X,2,"window X restored");Near(form.Bounds.Y,savedBounds.Y,2,"window Y restored");
            Near(form.Bounds.Width,savedBounds.Width,2,"window width restored");Near(form.Bounds.Height,savedBounds.Height,2,"window height restored");
            Near(Field("scale"),savedScale,.001,"leaving fullscreen preserves zoom");
            MapPoint restoredCenter=CenterWorld();
            Near(restoredCenter.X,savedCenter.X,.2,"leaving fullscreen restores world center X");
            Near(restoredCenter.Y,savedCenter.Y,.2,"leaving fullscreen restores world center Z");
            Check(Command(Keys.F11),"F11 command enters fullscreen");
            WaitFor(delegate{return form.IsFullScreen;},"F11 fullscreen state",5000);
            Check(Command(Keys.F11),"second F11 command leaves fullscreen");
            WaitFor(delegate{return !form.IsFullScreen;},"second F11 restores window",5000);
            Check(AutoBox().Visible,"controls return after fullscreen");
            Update(200,1,150,"base");
            Check(Field("scale")>fit*1.5,"base-floor building is focused before watcher restart");
            foreach(Control c in Descendants(form)) if(c is Button && c.Text=="Zatrzymaj") ((Button)c).PerformClick();
            string outside=WritePosition(300,1,220);
            foreach(Control c in Descendants(form)) if(c is Button && c.Text=="Obserwuj") ((Button)c).PerformClick();
            WaitFor(delegate{return canvas.Position!=null && canvas.Position.FileName==outside && canvas.Layer!=null && canvas.Layer.Id=="base";},"watcher restart reads latest outdoor screenshot");
            Near(Field("scale"),fit,Math.Max(.002,fit*.03),"new watcher session clears stale indoor zoom");
            Check(Directory.GetFiles(fixtures,"*.png").Length==counter,"all synthetic screenshots are preserved");
        }

        [STAThread]
        static int Main(string[] args)
        {
            ThreadExceptionEventHandler handler=null;
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                string dataRoot=Path.GetFullPath(args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"EftPosition-experience"));
                fixtures=Path.Combine(dataRoot,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                reports=Path.GetFullPath(args.Length>1?args[1]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"reports"));
                Directory.CreateDirectory(fixtures);Directory.CreateDirectory(reports);
                string cache=args.Length>2?Path.GetFullPath(args[2]):Path.Combine(fixtures,"cache");
                form=new MainForm(fixtures,null,0,false,true,null,cache);
                handler=delegate(object sender,ThreadExceptionEventArgs e) { failure=e.Exception;Console.WriteLine("GUI exception: "+e.Exception);if(form!=null&&!form.IsDisposed) form.Close(); };
                Application.ThreadException+=handler;
                using(form)
                using(System.Windows.Forms.Timer driver=new System.Windows.Forms.Timer())
                {
                    DateTime begin=DateTime.UtcNow;driver.Interval=100;
                    driver.Tick+=delegate
                    {
                        try
                        {
                            if(canvas==null) foreach(Control control in Descendants(form)) if(control is MapCanvas) canvas=(MapCanvas)control;
                            if(canvas==null || canvas.Layer==null)
                            { if((DateTime.UtcNow-begin).TotalSeconds>30) throw new Exception("Map did not load before integration test timeout.");return; }
                            driver.Stop();RunChecks();
                            Console.WriteLine("PASS: "+checks+" v3 experience checks; automatic indoor focus, floors, vector zoom, fullscreen/Escape/F11.");
                            form.Close();
                        }
                        catch(Exception ex) {failure=ex;driver.Stop();Console.WriteLine(ex);form.Close();}
                    };
                    driver.Start();Application.Run(form);
                }
                return failure==null?0:1;
            }
            catch(Exception ex) {Console.WriteLine(ex);return 1;}
            finally {if(handler!=null) Application.ThreadException-=handler;}
        }
    }
}
