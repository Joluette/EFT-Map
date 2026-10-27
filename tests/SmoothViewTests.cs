// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace EftPosition
{
    static class SmoothViewTests
    {
        static int checks;
        static void Check(bool ok,string message) {checks++;if(!ok)throw new Exception(message);}
        static void Near(double a,double b,double tolerance,string message) {Check(Math.Abs(a-b)<=tolerance,message+" ("+a+" vs "+b+")");}
        static float Field(MapCanvas map,string name) {return (float)typeof(MapCanvas).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(map);}
        static void Mouse(MapCanvas map,string name,MouseButtons button,int x,int y,int delta)
        {typeof(MapCanvas).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(map,new object[]{new MouseEventArgs(button,0,x,y,delta)});}
        static void Pump(int milliseconds)
        {var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(2);}}
        static void Finish(MapCanvas map)
        {
            var clock=Stopwatch.StartNew();
            while(map.IsAnimating && clock.ElapsedMilliseconds<3000){Application.DoEvents();Thread.Sleep(2);}
            Check(!map.IsAnimating,"zoom completes without a running idle animation");
        }
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                string assets=Path.GetFullPath(args[0]);MapDefinition selected=MapCatalog.Load(assets)[0];
                using(Form form=new Form {ClientSize=new Size(1100,720)})
                using(MapCanvas map=new MapCanvas {Dock=DockStyle.Fill})
                {
                    form.Controls.Add(map);form.Show();
                    map.LoadMap(selected,selected.Layers[0],Path.Combine(assets,selected.Layers[0].VectorImage));
                    Pump(100);map.FitMap();float fit=Field(map,"scale");
                    const int x=310,y=270;
                    double worldX=(x-Field(map,"offsetX"))/fit,worldY=(y-Field(map,"offsetY"))/fit;
                    bool intermediate=false,anchorX=true,anchorY=true;
                    PaintEventHandler observe=delegate
                    {
                        float actual=Field(map,"scale");
                        if(actual>fit+.00001f && actual<fit*1.2f-.00001f) intermediate=true;
                        anchorX&=Math.Abs((x-Field(map,"offsetX"))/actual-worldX)<.001;
                        anchorY&=Math.Abs((y-Field(map,"offsetY"))/actual-worldY)<.001;
                    };
                    map.Paint+=observe;
                    Mouse(map,"OnMouseWheel",MouseButtons.None,x,y,120);
                    Check(map.IsAnimating,"wheel starts smooth zoom");
                    Finish(map);map.Paint-=observe;
                    Check(intermediate,"wheel paints intermediate zoom levels");
                    Check(anchorX,"cursor anchor X is stable in painted animation frames");
                    Check(anchorY,"cursor anchor Y is stable in painted animation frames");
                    Near(Field(map,"scale"),fit*1.2,.0001,"wheel reaches exact target scale");
                    Near((x-Field(map,"offsetX"))/Field(map,"scale"),worldX,.001,"cursor anchor X is stable after animation");
                    Near((y-Field(map,"offsetY"))/Field(map,"scale"),worldY,.001,"cursor anchor Y is stable after animation");

                    map.FitMap();
                    Mouse(map,"OnMouseWheel",MouseButtons.None,x,y,30);Mouse(map,"OnMouseWheel",MouseButtons.None,x,y,30);
                    Finish(map);Near(Field(map,"scale"),fit*Math.Sqrt(1.2),.0001,"partial wheel deltas accumulate proportionally");
                    map.FitMap();
                    Mouse(map,"OnMouseWheel",MouseButtons.None,x,y,120);Mouse(map,"OnMouseWheel",MouseButtons.None,x,y,120);
                    Mouse(map,"OnMouseWheel",MouseButtons.None,x,y,-120);
                    Finish(map);Near(Field(map,"scale"),fit*1.2,.0001,"fast opposite wheel input updates pending target");
                    map.FitMap();Mouse(map,"OnMouseWheel",MouseButtons.None,x,y,240);
                    Finish(map);Near(Field(map,"scale"),fit*1.44,.0001,"multi-notch wheel input is preserved");
                    Mouse(map,"OnMouseWheel",MouseButtons.None,x,y,0);Check(!map.IsAnimating,"zero wheel delta does not move view");

                    map.SmoothZoom(2);map.FitMap();Pump(220);
                    Check(!map.IsAnimating,"fit cancels an outstanding animation");Near(Field(map,"scale"),fit,.0001,"fit is not overwritten by a late animation frame");
                    map.SmoothZoom(2);
                    Mouse(map,"OnMouseDown",MouseButtons.Left,200,220,0);
                    Check(!map.IsAnimating,"drag takes control immediately from zoom animation");
                    float oldX=Field(map,"offsetX"),oldY=Field(map,"offsetY"),oldScale=Field(map,"scale");
                    Mouse(map,"OnMouseMove",MouseButtons.Left,320,265,0);Mouse(map,"OnMouseUp",MouseButtons.Left,320,265,0);
                    Pump(220);Near(Field(map,"offsetX"),oldX+120,.001,"drag tracks pointer X exactly");
                    Near(Field(map,"offsetY"),oldY+45,.001,"drag tracks pointer Y exactly");
                    Near(Field(map,"scale"),oldScale,.0001,"drag has no delayed zoom jump");
                    Mouse(map,"OnMouseDown",MouseButtons.Left,320,265,0);
                    Mouse(map,"OnMouseWheel",MouseButtons.None,320,265,120);
                    Check(!map.IsAnimating,"wheel while dragging does not fight pointer movement");
                    oldX=Field(map,"offsetX");oldY=Field(map,"offsetY");
                    Mouse(map,"OnMouseMove",MouseButtons.Left,330,285,0);Mouse(map,"OnMouseUp",MouseButtons.Left,330,285,0);
                    Pump(220);Near(Field(map,"offsetX"),oldX+10,.001,"drag X remains stable after simultaneous wheel");
                    Near(Field(map,"offsetY"),oldY+20,.001,"drag Y remains stable after simultaneous wheel");
                    map.SmoothZoom(2);map.CenterPosition(); // No position: the wheel can still finish normally.
                    Finish(map);
                    map.SmoothZoom(2);map.ClearMap();Pump(220);
                    Check(!map.IsAnimating && map.Layer==null,"map changes cancel pending animation safely");
                    map.LoadMap(selected,selected.Layers[0],Path.Combine(assets,selected.Layers[0].VectorImage));
                    Check(map.IsVector,"vector map remains usable after cancelled animation and reload");
                    form.Close();
                }
                Console.WriteLine("PASS: "+checks+" smooth interaction checks; intermediate frames, cursor anchor, wheel deltas, cancellation and drag.");return 0;
            }
            catch(Exception ex) {Console.WriteLine(ex);return 1;}
        }
    }
}
