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
    static class MapIconTests
    {
        static int checks;
        static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
        static IEnumerable<Control> All(Control c){foreach(Control child in c.Controls){yield return child;foreach(Control nested in All(child))yield return nested;}}
        static T Find<T>(Control c,string name)where T:Control{return All(c).OfType<T>().Single(n=>n.AccessibleName==name);}
        static void Pump(int ms){var t=Stopwatch.StartNew();while(t.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(2);}}
        static void Wait(Func<bool> ready,string text){var t=Stopwatch.StartNew();while(!ready()&&t.ElapsedMilliseconds<15000)Pump(5);Check(ready(),text);}
        static Bitmap Render(Control c){var image=new Bitmap(c.Width,c.Height);c.DrawToBitmap(image,c.ClientRectangle);return image;}
        static bool Same(Bitmap a,Bitmap b){for(int y=0;y<a.Height;y+=2)for(int x=0;x<a.Width;x+=2)if(a.GetPixel(x,y)!=b.GetPixel(x,y))return false;return true;}
        static string Tip(MapCanvas c,double x,double z){return c.MarkerTooltipAt(Point.Round(c.MarkerScreenPoint(x,z)));}
        [STAThread]static int Main(string[] args)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string assets=Path.GetFullPath(args[0]),root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);UiText.Language="en";
                Check(new AppPreferences().Language=="en","fresh installation defaults to English");
                var invalid=new AppPreferences {Language="unknown"};invalid.Normalize();Check(invalid.Language=="en","invalid language falls back to English");
                var catalog=LootCatalog.Load(Path.Combine(assets,"loot.json"));Check(catalog.Version==2&&catalog.Error==null,"v2 catalog loads");
                Check(catalog.Types.Count(t=>t.Group=="container")==27,"all 27 source container types can be selected independently");
                Check(catalog.Types.Count(t=>t.Group=="loose")==14,"14 source handbook categories, including injectors");
                Check(catalog.Types.Count(t=>t.Group=="hazard")==3&&catalog.Types.Count(t=>t.Group=="utility")==2,"hazards and utilities are distinct types");
                Check(catalog.Types.All(t=>!String.IsNullOrEmpty(t.Id)&&!String.IsNullOrEmpty(t.Icon)&&!t.Name.EndsWith(" Name")),"stable filter IDs and resolved English labels");
                Check(catalog.Items.Any(i=>i.Name.Contains("SJ6")&&catalog.Types[i.Type].Id=="loose:injectors"),"SJ6 injector is classified from the source handbook");
                Check(catalog.Maps.SelectMany(m=>m.Points).Any(p=>p.Items!=null&&p.Items.Select(i=>catalog.Items[i].Type).Distinct().Count()>1),"mixed-category loose points retain all items");
                Check(catalog.Maps.SelectMany(m=>m.Points).Where(p=>catalog.Types[p.Type].Id=="utility:locked-room").All(p=>!String.IsNullOrEmpty(p.KeyName)),"every imported room includes its required key");
                string legacyCache=Path.Combine(root,"legacy-cache");Directory.CreateDirectory(legacyCache);File.WriteAllText(Path.Combine(legacyCache,"loot-preferences.json"),"{\"Enabled\":false,\"Categories\":64,\"GroupNearby\":false}");
                using(var legacyPanel=new LootPanel(assets,legacyCache))
                {
                    Check(!legacyPanel.Preferences.Enabled&&!legacyPanel.Preferences.GroupNearby,"migration retains master and grouping switches");
                    Check(legacyPanel.Catalog.Types.All(t=>legacyPanel.Preferences.Allows(t)==(t.Category==6)),"legacy broad category mask expands into equivalent independent filter IDs");
                    var medical=All(legacyPanel).OfType<LootFilterRow>().Single(r=>r.Type.Id=="container:medcase");medical.Checked=true;
                    Check(legacyPanel.Preferences.Allows(medical.Type)&&!legacyPanel.Preferences.Allows(legacyPanel.Catalog.Types.Single(t=>t.Id=="container:medbag")),"enabling a migrated leaf does not enable its old sibling category");
                    Check(legacyPanel.Save(),"migrated filters save atomically");
                }
                string png=Path.Combine(root,"fixture.png");using(var b=new Bitmap(512,512))using(var g=Graphics.FromImage(b)){g.Clear(Color.FromArgb(40,64,61));b.Save(png);}
                var types=new[]{
                    new LootType {Id="container:safe",Name="Safe",LocalName="Sejf",Icon="safe",Section="container",Category=6},
                    new LootType {Id="container:bank-safe",Name="Bank safe",LocalName="Sejf bankowy",Icon="bank-safe",Section="container",Category=6},
                    new LootType {Id="loose:electronics",Name="Electronics",LocalName="Elektronika",Icon="electronics",Section="loose",Category=0},
                    new LootType {Id="loose:injectors",Name="Injectors",LocalName="Wstrzykiwacze",Icon="injectors",Section="loose",Category=0},
                    new LootType {Id="utility:locked-room",Name="Locked rooms",LocalName="Zamknięte pomieszczenia",Icon="lock",Section="utility",Category=8},
                    new LootType {Id="utility:stationary-weapon",Name="Stationary weapons",LocalName="Broń stacjonarna",Icon="stationary",Section="utility",Category=8},
                    new LootType {Id="hazard:minefield",Name="Mines",LocalName="Miny",Icon="mine",Section="hazard",Category=8}};
                var points=new[]{new LootPoint {X=100,Z=100,Type=0,Modes=1},new LootPoint {X=200,Z=100,Type=1,Modes=1},
                    new LootPoint {X=150,Z=250,Type=2,Modes=1,Items=new[]{0,1}},new LootPoint {X=400,Z=100,Type=4,Modes=1,Key=2,NeedsPower=true},
                    new LootPoint {X=400,Z=200,Type=5,Modes=1,Detail="NSV Utyos"},
                    new LootPoint {X=1000,Z=1000,Type=6,Modes=1,Outline=new[]{new[]{350d,350d},new[]{500d,350d},new[]{500d,500d},new[]{350d,500d}}}};
                var fixture=new LootCatalog {Version=2,Types=types,Items=new[]{new LootItem {Id="gpu",Name="Graphics card",LocalName="Karta graficzna",Type=2},new LootItem {Id="sj6",Name="SJ6 injector",LocalName="Wstrzykiwacz SJ6",Type=3},new LootItem {Id="key",Name="Factory emergency exit key",LocalName="Klucz awaryjny",Type=3}},Maps=new[]{new LootMap {Id="fixture",Points=points}}};
                var definition=new MapDefinition {Id="fixture",Author="Test",Name="Fixture",Labels=new[]{new MapLabel {Text="First place",X=50,Z=40},new MapLabel {Text="Overlapping place",X=50,Z=40}},Layers=new[]{new MapLayer {Id="base",Width=512,Height=512,Affine=new[]{1d,0d,0d,1d,0d,0d}}}};
                var options=new LootPreferences {GroupNearby=false};
                using(var form=new Form {ClientSize=new Size(800,640)})using(var canvas=new MapCanvas {Dock=DockStyle.Fill,MapOnly=true})
                {
                    form.Controls.Add(canvas);form.Show();canvas.LoadMap(definition,definition.Layers[0],png);canvas.SetLoot(fixture,"fixture","regular",options);using(var b=Render(canvas))b.Save(Path.Combine(root,"fixture-all.png"));
                    Check(canvas.VisibleMapLabels==1,"overlapping labels do not paint on top of each other");
                    Check(canvas.VisibleLootIcons==5,"offscreen hazard centroid is culled but ordinary markers remain");
                    Check(Tip(canvas,425,425)=="Mines","hazard outline remains visible and hoverable when its centroid is outside the viewport");
                    Color shaded;Point areaPixel=Point.Round(canvas.MarkerScreenPoint(425,425));using(var b=Render(canvas))shaded=b.GetPixel(areaPixel.X,areaPixel.Y);
                    Check(Tip(canvas,400,100).Contains("Required key: Factory emergency exit key")&&Tip(canvas,400,100).Contains("Requires power"),"room tooltip identifies the real key and power requirement");
                    Check(Tip(canvas,400,200).Contains("NSV Utyos"),"stationary weapon retains its source name");
                    options.DisabledTypes=new[]{"container:safe"};canvas.SetLoot(fixture,"fixture","regular",options);using(var b=Render(canvas)){}Check(Tip(canvas,100,100)==""&&Tip(canvas,200,100)=="Bank safe","one container filter does not disable another in its old category");
                    options.DisabledTypes=new[]{"loose:electronics"};canvas.SetLoot(fixture,"fixture","regular",options);string tip=Tip(canvas,150,250);Check(tip.Contains("SJ6")&&!tip.Contains("Graphics card"),"mixed point stays visible for selected injector category, excludes hidden item names");
                    options.DisabledTypes=new[]{"loose:injectors"};canvas.SetLoot(fixture,"fixture","regular",options);tip=Tip(canvas,150,250);Check(tip.Contains("Graphics card")&&!tip.Contains("SJ6"),"opposite mixed-point selection is independent");
                    options.DisabledTypes=new[]{"loose:injectors","loose:electronics","hazard:minefield"};canvas.SetLoot(fixture,"fixture","regular",options);using(var b=Render(canvas))Check(b.GetPixel(areaPixel.X,areaPixel.Y)!=shaded,"hazard filter invalidates the cached map region, with no stale shading");Check(Tip(canvas,150,250)==""&&Tip(canvas,425,425)=="","disabled loose categories and hazard remove both artwork and hit targets");
                    options.DisabledTypes=new string[0];options.GroupNearby=true;canvas.SetLoot(fixture,"fixture","regular",options);using(var b=Render(canvas)){}int builds=canvas.LootViewBuildCount;using(var b=Render(canvas)){}Check(canvas.LootViewBuildCount==builds,"stationary frame reuses layout");
                    canvas.SetPosition(new Position("fixture",DateTime.UtcNow,150,0,250,0,false,0));using(var b=canvas.RenderMiniMap(new Size(260,260),100)){}using(var b=Render(canvas)){}Check(Tip(canvas,150,250).Contains("SJ6"),"main hover remains correct after minimap draw");
                    UiText.Language="pl";Check(Tip(canvas,400,100).Contains("Wymagany klucz: Klucz awaryjny"),"room detail switches language");UiText.Language="en";
                    string prefs=Path.Combine(root,"prefs.json");options.DisabledTypes=new[]{"container:bank-safe","loose:electronics"};Check(options.Save(prefs),"independent filters save");var loaded=LootPreferences.Load(prefs);Check(!loaded.Allows(types[1])&&loaded.Allows(types[0])&&!loaded.Allows(types[2])&&loaded.Allows(types[3]),"filter IDs survive round trip independently of category ordering");
                    form.Close();
                }
                var woods=MapCatalog.Load(assets).Single(m=>m.Id=="woods");var woodsOptions=new LootPreferences {Enabled=false};
                using(var form=new Form {ClientSize=new Size(900,620)})using(var canvas=new MapCanvas {Dock=DockStyle.Fill,MapOnly=true})
                {
                    form.Controls.Add(canvas);form.Show();canvas.LoadMap(woods,woods.Layers[0],Path.Combine(assets,woods.Layers[0].VectorImage));var hazard=catalog.ForMap("woods").First(p=>p.Outline!=null);
                    canvas.SetPosition(new Position("woods",DateTime.UtcNow,hazard.X,hazard.Y,hazard.Z,0,false,0));canvas.SetLoot(catalog,"woods","regular",woodsOptions);
                    using(var off=canvas.RenderMiniMap(new Size(260,260),100))
                    {
                        woodsOptions.Enabled=true;canvas.SetLoot(catalog,"woods","regular",woodsOptions);using(var b=Render(canvas)){}
                        using(var hidden=canvas.RenderMiniMap(new Size(260,260),100))Check(Same(off,hidden),"cached main-map hazards do not leak into a minimap whose icons are disabled");
                        woodsOptions.ShowOnMiniMap=true;canvas.SetLoot(catalog,"woods","regular",woodsOptions);using(var on=canvas.RenderMiniMap(new Size(260,260),100))Check(!Same(off,on),"minimap hazard regions and icons appear only with its separate switch");
                    }
                    using(var b=Render(canvas)){}var vector=(VectorMap)typeof(MapCanvas).GetField("vector",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(canvas);int renders=vector.RenderCount;
                    woodsOptions.DisabledTypes=new[]{"loose:injectors"};canvas.SetLoot(catalog,"woods","regular",woodsOptions);using(var b=Render(canvas)){}Check(vector.RenderCount==renders,"ordinary loot filter changes reuse the map and its hazard-region cache");
                    form.Close();
                }
                string cache=Path.Combine(root,"app-cache");new AppPreferences {Language="en",AutoMapEnabled=false,DeleteScreenshots=false,OverlayEnabled=false,Theme="night",BackgroundMode="plain",MapOpacity=100}.Save(Path.Combine(cache,"app-preferences.json"));
                string uiMap="woods";
                if(args.Length>2 && File.Exists(Path.Combine(args[2],"maps","the-lab-base.png"))){Directory.CreateDirectory(Path.Combine(cache,"maps"));File.Copy(Path.Combine(args[2],"maps","the-lab-base.png"),Path.Combine(cache,"maps","the-lab-base.png"),true);uiMap="the-lab";}
                using(var form=new MainForm(null,null,0,false,true,null,cache,false,false,uiMap))
                {
                    form.Show();var canvas=All(form).OfType<MapCanvas>().Single();Wait(()=>canvas.Layer!=null&&form.SettingsPreparationCount==1,"English app loads "+uiMap);
                    Find<Button>(form,"Map filters and quests").PerformClick();Find<Button>(form,"Show loot filters").PerformClick();var panel=All(form).OfType<LootPanel>().Single();
                    Check(All(panel).OfType<LootFilterRow>().Count()==46,"each container/category has its own keyboard-accessible checkbox");
                    Find<Button>(panel,"Hide all loot categories").PerformClick();Find<TextBox>(panel,"Search map icons").Text="SJ6";Pump(20);var injector=All(panel).OfType<LootFilterRow>().Single(r=>r.Type.Id=="loose:injectors");Check(injector.Visible&&All(panel).OfType<LootFilterRow>().Count(r=>r.Visible)==1,"search also finds categories by actual item name");
                    injector.Checked=true;canvas.FitMap();canvas.Refresh();Check(canvas.VisibleLootIcons>0,"injectors alone can be enabled from real app filters");
                    using(var b=Render(form))b.Save(Path.Combine(root,"lab-injectors-en.png"));
                    Find<TextBox>(panel,"Search map icons").Clear();Check(All(panel).OfType<LootFilterRow>().Count(r=>r.Checked)==1,"clearing search retains individual selections");
                    Find<CheckBox>(panel,"Toggle Loose loot").PerformClickForTest();Check(All(panel).OfType<LootFilterRow>().Where(r=>r.Type.Group=="loose").All(r=>r.Checked),"section master restores its own children");
                    UiText.Language="pl";panel.RefreshLanguage();Check(injector.Text=="Wstrzykiwacze"&&injector.Checked,"switching to Polish retains filters");UiText.Language="en";panel.RefreshLanguage();Check(injector.Text=="Injectors","English names restore without data reload");
                    Find<Button>(panel,"Show all loot categories").PerformClick();Find<Button>(panel,"Expand Containers").PerformClick();canvas.Refresh();using(var b=Render(form))b.Save(Path.Combine(root,"lab-loose-loot-en.png"));
                    Find<TextBox>(panel,"Search map icons").Text="locked";Check(All(panel).OfType<LootFilterRow>().Single(r=>r.Type.Id=="utility:locked-room").Visible,"keyed rooms are searchable separately from loose keys");
                    Find<TextBox>(panel,"Search map icons").Clear();form.SetFullScreen(true);canvas.Refresh();using(var b=Render(form))b.Save(Path.Combine(root,"lab-fullscreen-en.png"));Check(canvas.VisibleLootIcons>0,"icons work in F11 fullscreen");form.SetFullScreen(false);
                    form.Size=form.MinimumSize;Pump(30);Check(Find<TextBox>(panel,"Search map icons").Visible,"search and independent filters remain accessible at minimum size");using(var b=Render(form))b.Save(Path.Combine(root,"minimum-en.png"));form.Close();
                }
                using(var image=new Bitmap(840,((catalog.Types.Length+3)/4)*74+42))using(var g=Graphics.FromImage(image))using(var font=new Font("Segoe UI",9))
                {
                    g.Clear(Color.FromArgb(17,27,35));g.DrawString("EFT Map 3.6 · Map icon library",UiTheme.Section,Brushes.White,18,12);
                    for(int i=0;i<catalog.Types.Length;i++){int x=(i%4)*210+12,y=(i/4)*74+48;using(var icon=LootArtwork.Create(catalog.Types[i].Icon,32))g.DrawImageUnscaled(icon,x,y);g.DrawString(catalog.Types[i].Name,font,Brushes.White,new RectangleF(x+40,y+2,155,48));}image.Save(Path.Combine(root,"icon-library-en.png"));
                }
                Console.WriteLine("PASS: "+checks+" map icon release checks: data, individual filters, mixed loose loot, keyed rooms, hazards, search, English/Polish, cache and F11.");return 0;
            }
            catch(Exception ex){Console.WriteLine(ex);return 1;}
        }
        static void PerformClickForTest(this CheckBox box){typeof(CheckBox).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(box,new object[]{EventArgs.Empty});}
    }
}
