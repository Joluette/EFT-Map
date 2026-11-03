// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
namespace EftPosition
{
    static class LootTests
    {
        static int checks;
        static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static void Check(bool ok,string text){checks++;if(!ok)throw new Exception(text);}
        static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,Private).GetValue(target);}
        static IEnumerable<Control> All(Control c){foreach(Control child in c.Controls){yield return child;foreach(var next in All(child))yield return next;}}
        static T Find<T>(Control c,string name)where T:Control{return All(c).OfType<T>().Single(n=>n.AccessibleName==name);}
        static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(2);}}
        static void Wait(Func<bool> ready,string text){var clock=Stopwatch.StartNew();while(!ready() && clock.ElapsedMilliseconds<15000)Pump(5);Check(ready(),text);}
        static Bitmap Render(Control c){var b=new Bitmap(c.Width,c.Height);c.DrawToBitmap(b,c.ClientRectangle);return b;}
        static bool Different(Bitmap a,Bitmap b){for(int y=0;y<a.Height;y+=2)for(int x=0;x<a.Width;x+=2)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return true;return false;}
        [STAThread]static int Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            UiText.Language="pl";
            try
            {
                string assets=Path.GetFullPath(args[0]),root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
                var catalog=LootCatalog.Load(Path.Combine(assets,"loot.json"));Check(catalog.Error==null,"bundled loot catalog loads");Check(catalog.Maps.Length==13 && catalog.Maps.Sum(m=>m.Points.Length)==11519,"all 13 maps and audited point count");
                Check(catalog.Types.Length==46 && catalog.Types.Any(t=>t.Group=="loose"),"shared type dictionary includes loose loot");
                var definitions=MapCatalog.Load(assets);
                foreach(var map in catalog.Maps)
                {
                    var definition=definitions.Single(d=>d.Id==map.Id);var unique=new HashSet<string>();
                    foreach(var p in map.Points)
                    {
                        Check(unique.Add(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(p)),"no duplicate loot points: "+map.Id);
                        Check(definition.Layers.Any(l=>{MapPoint q=l.Project(p.X,p.Z);return q.X>=-64 && q.Y>=-64 && q.X<=l.Width+64 && q.Y<=l.Height+64;}) || p.Outline!=null,"loot or hazard outline projects inside calibrated map: "+map.Id);
                    }
                }
                var computer=catalog.ForMap("woods").First(p=>Math.Abs(p.X+610.343)<.001 && Math.Abs(p.Z-137.098)<.001);Check(catalog.Types[computer.Type].Name=="PC block","known source point retains the correct XYZ and container type");
                string bad=Path.Combine(root,"bad.json");File.WriteAllText(bad,"{oops");Check(LootCatalog.Load(bad).Error!=null,"malformed catalog fails without crashing");Check(LootCatalog.Load(Path.Combine(root,"missing.json")).Maps.Length==0,"missing optional dataset leaves other map features usable");
                File.WriteAllText(bad,"{\"Version\":1,\"Types\":[{\"Name\":\"x\",\"LocalName\":\"x\",\"Category\":99}],\"Maps\":[]}");Check(LootCatalog.Load(bad).Error!=null,"invalid categories are rejected");
                string prefs=Path.Combine(root,"loot-preferences.json");var options=new LootPreferences {Enabled=false,ShowOnMiniMap=true,GroupNearby=false,AllFloors=true,Categories=37};Check(options.Save(prefs),"separate loot preferences save atomically");var saved=LootPreferences.Load(prefs);Check(!saved.Enabled && saved.ShowOnMiniMap && !saved.GroupNearby && saved.AllFloors && saved.Categories==37,"loot preferences round trip");
                string png=Path.Combine(root,"fixture.png");using(var b=new Bitmap(512,512))using(Graphics g=Graphics.FromImage(b)){g.Clear(Color.Navy);b.Save(png);}
                var fixture=new MapDefinition {Id="fixture",Author="Test",Name="Fixture",Layers=new[]{new MapLayer {Id="base",Width=512,Height=512,Affine=new[]{1d,0d,0d,1d,0d,0d}}}};
                var points=new List<LootPoint>();for(int i=0;i<5;i++)points.Add(new LootPoint {X=100+i*.2,Y=0,Z=100+i*.2,Type=0,Modes=1});points.Add(new LootPoint {X=400,Z=400,Type=1,Modes=1});points.Add(new LootPoint {X=300,Z=100,Type=0,Modes=2});
                var data=new LootCatalog {Types=new[]{new LootType {Name="Loose loot",LocalName="Loot luzem",Category=0},new LootType {Name="Safe",LocalName="Sejf",Category=6}},Maps=new[]{new LootMap {Id="fixture",Points=points.ToArray()}}};
                options=new LootPreferences {ShowOnMiniMap=false};
                using(var form=new Form {ClientSize=new Size(640,420)})using(var canvas=new MapCanvas {Dock=DockStyle.Fill,MapOnly=true})
                {
                    form.Controls.Add(canvas);form.Show();canvas.LoadMap(fixture,fixture.Layers[0],png);canvas.SetLoot(data,"fixture","regular",options);using(var b=Render(canvas))b.Save(Path.Combine(root,"grouped.png"));
                    Check(canvas.VisibleLootIcons==2,"nearby points share one glyph and other game modes are excluded");int builds=canvas.LootViewBuildCount;using(var b=Render(canvas)){}Check(canvas.LootViewBuildCount==builds,"stationary redraw reuses layout and hit index");
                    var pixel=Point.Round(canvas.MarkerScreenPoint(100.4,100.4));Check(canvas.MarkerTooltipAt(pixel).Contains("5"),"cluster hover lists its actual count");
                    options.GroupNearby=false;canvas.SetLoot(data,"fixture","regular",options);using(var b=Render(canvas)){}Check(canvas.VisibleLootIcons==6,"grouping can be disabled");Check(canvas.MarkerTooltipAt(pixel)=="Loot luzem","individual hover shows the localized loot type");
                    canvas.SetMarkers("fixture",new[]{new MapMarker {Name="Quest priority",X=100.4,Z=100.4,Kind=MapMarkerKind.Quest}});Check(canvas.MarkerTooltipAt(pixel)=="Quest priority","quest hover retains priority over loot");canvas.SetMarkers("fixture",new MapMarker[0]);
                    UiText.Language="en";Check(canvas.MarkerTooltipAt(pixel)=="Loose loot","loot tooltip switches language without reloading data");UiText.Language="pl";
                    options.Categories=1<<6;canvas.SetLoot(data,"fixture","regular",options);using(var b=Render(canvas)){}Check(canvas.VisibleLootIcons==1,"category filtering removes other point types");
                    options.Categories=511;canvas.SetLoot(data,"fixture","pve",options);using(var b=Render(canvas)){}Check(canvas.VisibleLootIcons==1,"game-mode filtering selects the matching points");canvas.SetLoot(data,"fixture","regular",options);
                    options.Enabled=false;using(var b=Render(canvas))
                    {Check(canvas.VisibleLootIcons==0 && canvas.MarkerTooltipAt(pixel)=="","master switch removes both icons and hit targets");canvas.SetLoot(null,"fixture","regular",options);using(var original=Render(canvas))Check(!Different(b,original),"disabled loot is pixel-identical to having no loot layer");canvas.SetLoot(data,"fixture","regular",options);}options.Enabled=true;
                    canvas.SetPosition(new Position("fixture",DateTime.UtcNow,100,0,100,0,false,0));using(var off=canvas.RenderMiniMap(new Size(240,240),100)){options.ShowOnMiniMap=true;using(var on=canvas.RenderMiniMap(new Size(240,240),100))Check(Different(off,on),"minimap has an independent loot switch");}
                    canvas.ClearMap();using(var b=Render(canvas)){}Check(canvas.VisibleLootIcons==0,"changing maps removes old loot and hit targets");form.Close();
                }
                using(var form=new Form {ClientSize=new Size(950,620)})using(var canvas=new MapCanvas {Dock=DockStyle.Fill,MapOnly=true})
                {
                    var factory=definitions.Single(d=>d.Id=="factory");var layer=factory.Layers[0];var navigation=new AutoNavigator(factory,Path.Combine(assets,"maps.upstream.json"));
                    form.Controls.Add(canvas);form.Show();canvas.SetMarkerLevels(navigation);canvas.LoadMap(factory,layer,Path.Combine(assets,layer.VectorImage));
                    var floors=new LootPreferences {GroupNearby=false,AllFloors=true};canvas.SetLoot(catalog,"factory","regular",floors);using(var b=Render(canvas)){}int allFloors=canvas.VisibleLootIcons;
                    floors.AllFloors=false;canvas.SetLoot(catalog,"factory","regular",floors);using(var b=Render(canvas)){}Check(canvas.VisibleLootIcons>0 && canvas.VisibleLootIcons<allFloors,"Factory loot filters out other floors using calibrated height bands");
                    form.Close();
                }
                string cache=Path.Combine(root,"app-cache");new AppPreferences {Language="pl",AutoMapEnabled=false,DeleteScreenshots=false,OverlayEnabled=false,Theme="night",WindowOpacity=100,BackgroundMode="plain"}.Save(Path.Combine(cache,"app-preferences.json"));
                using(var form=new MainForm(null,null,0,false,true,null,cache,false,false,"woods"))
                {
                    form.Show();var canvas=Field<MapCanvas>(form,"canvas");Wait(()=>canvas.Layer!=null && form.SettingsPreparationCount==1,"real app loads Woods and prepared settings");
                    Find<Button>(form,"Filtry mapy i questy").PerformClick();Find<Button>(form,"Pokaż filtry lootu").PerformClick();canvas.FitMap();canvas.Refresh();Check(Find<CheckBox>(form,"Pokaż loot").Visible && canvas.VisibleLootIcons>0,"loot tab and real icons are enabled in the main app");
                    using(var b=Render(form))b.Save(Path.Combine(root,"woods-loot.png"));
                    Find<CheckBox>(form,"Pokaż loot").Checked=false;canvas.Refresh();Check(canvas.VisibleLootIcons==0,"UI master switch hides loot immediately");Find<CheckBox>(form,"Pokaż loot").Checked=true;
                    Find<Button>(form,"Ukryj wszystkie kategorie lootu").PerformClick();canvas.Refresh();Check(canvas.VisibleLootIcons==0,"None button clears all categories");Find<Button>(form,"Pokaż wszystkie kategorie lootu").PerformClick();canvas.Refresh();Check(canvas.VisibleLootIcons>0,"All button restores points");
                    form.SetFullScreen(true);canvas.FitMap();canvas.Refresh();Check(form.IsFullScreen && canvas.VisibleLootIcons>0,"loot remains usable in F11 fullscreen");using(var b=Render(form))b.Save(Path.Combine(root,"woods-fullscreen.png"));form.SetFullScreen(false);
                    form.Size=form.MinimumSize;Pump(20);Check(Find<CheckBox>(form,"Pokaż loot").Visible,"loot controls remain reachable at the minimum size");using(var b=Render(form))b.Save(Path.Combine(root,"loot-minimum.png"));
                    Find<Button>(form,"Pokaż questy i ekstrakty").PerformClick();Check(Find<TextBox>(form,"Szukaj questu na wybranej mapie").Visible,"existing quest controls remain accessible");form.Close();
                }
                Console.WriteLine("PASS: "+checks+" loot checks: real data, bounds, grouping, categories, hover, mode, minimap, UI, persistence and removal.");return 0;
            }
            catch(Exception error){Console.WriteLine(error);return 1;}
        }
    }
}
