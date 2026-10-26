// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using EftPosition;

static class AutoNavigationTests
{
    static int checks;
    static MapDefinition[] maps;
    static string source;
    static void Check(bool ok,string message) {checks++;if(!ok) throw new Exception(message);}
    static MapDefinition Map(string id) {foreach(MapDefinition m in maps) if(m.Id==id) return m;throw new Exception(id);}
    static AutoNavigator Nav(string id) {return new AutoNavigator(Map(id),source);}
    static Position P(double x,double y,double z) {return new Position("fixture.png",DateTime.UtcNow,x,y,z,0,true,0);}
    static void MainTests()
    {
        var n=Nav("customs");
        var d=n.Update(P(300,1,220),"base");Check(!d.IsInterior && d.FocusBounds==null,"outdoors does not invent building");
        d=n.Update(P(200,1,150),"base");Check(d.IsInterior && d.Changed,"enter Dorms at ground level");
        Check(d.FocusBounds.Contains(165,125,0)&&d.FocusBounds.Contains(243,190,0),"Dorms full footprint");
        string context=d.ContextKey;
        d=n.Update(P(201,1.1,151),"base");Check(!d.Changed&&d.ContextKey==context,"no repeated view snapping while walking indoors");
        d=n.Update(P(200,2.8,150),"base");Check(d.LayerId=="base","upward stairs hysteresis");
        d=n.Update(P(200,3.0,150),"base");Check(d.LayerId=="Second_Floor"&&d.Changed,"Dorms second floor");
        d=n.Update(P(200,5.8,150),"Second_Floor");Check(d.LayerId=="Second_Floor","overlapping band waits until threshold margin");
        d=n.Update(P(200,6.0,150),"Second_Floor");Check(d.LayerId=="Third_Floor","overlap selects higher floor before broad older band maximum");
        d=n.Update(P(200,5.6,150),"Third_Floor");Check(d.LayerId=="Third_Floor","descending height jitter does not flap");
        d=n.Update(P(200,5.4,150),"Third_Floor");Check(d.LayerId=="Second_Floor","descend to second floor");
        d=n.Update(P(245,1,192),"Second_Floor");Check(d.LayerId=="base"&&!d.IsInterior&&d.ResetView,"leave building restores wide view");
        d=n.Update(P(245,1,192),"base");Check(!d.Changed&&!d.ResetView,"outdoor state stable");
        n.Reset();d=n.Update(P(205,0,150),"base");Check(d.Changed&&d.IsInterior,"reset new session");
        d=n.Update(P(208,-1,-149),"base");Check(d.LayerId=="Underground_Level"&&d.IsInterior,"Customs ZB013 underground selected");
        d=n.Update(P(209,-1,-150),"Underground_Level");Check(!d.Changed,"basement no repeated autofocus");

        n=Nav("factory");d=n.Update(P(0,0,0),"base");Check(d.IsInterior&&d.LayerId=="base","Factory indoor base focus");
        d=n.Update(P(0,3.1,0),"base");Check(d.LayerId=="base","Factory upward margin");
        d=n.Update(P(0,3.3,0),"base");Check(d.LayerId=="Second_Floor","Factory second floor");
        d=n.Update(P(0,-1.4,0),"Second_Floor");Check(d.LayerId=="Basement","Factory tunnels");
        d=n.Update(P(0,-.9,0),"Basement");Check(d.LayerId=="Basement","Factory basement boundary jitter");
        d=n.Update(P(0,-.7,0),"Basement");Check(d.LayerId=="base","Factory exit tunnels");

        n=Nav("the-lab");d=n.Update(P(-200,-2,-300),"base");Check(d.LayerId!="base"&&d.LayerAvailable,"Labs Technical global height");
        d=n.Update(P(-200,4,-300),d.LayerId);Check(d.LayerId!="base"&&d.LayerAvailable&&d.SourceLevelName=="Second Level","Labs bounded upper level");
        n=Nav("icebreaker");d=n.Update(P(0,20,0),"base");Check(d.LayerId!="base"&&d.SourceLevelName=="Helipad","Icebreaker numeric remote layer matched by URL");
        d=n.Update(P(0,23,0),d.LayerId);Check(d.SourceLevelName=="Gym/Canteen"&&d.Changed,"Icebreaker next deck");
        n=Nav("interchange");d=n.Update(P(0,27,0),"base");Check(d.LayerId=="First_Floor"&&d.IsInterior,"Interchange mall floor inside polygon");
        d=n.Update(P(300,27,0),"First_Floor");Check(d.LayerId=="base"&&!d.IsInterior,"same Y outside mall stays outdoors");
        n=Nav("reserve");d=n.Update(P(-100,-9,0),"base");Check(d.LayerId=="Bunkers"&&d.IsInterior,"Reserve bunker height and footprint");
        foreach(string id in new[]{"lighthouse","woods","terminal"})
        {n=Nav(id);d=n.Update(P(0,100,0),"base");Check(!d.IsInterior&&d.LayerId=="base","no invented building metadata "+id);}
        n=Nav("the-labyrinth");d=n.Update(P(0,0,0),"base");Check(d.IsInterior&&d.FocusBounds!=null,"Labyrinth single indoor level focus");
        n=Nav("customs");d=n.Update(P(99999,5,99999),"Third_Floor");Check(d.LayerId=="base"&&!d.IsInterior&&d.ContextKey=="outside-map","outside map never auto-selects floor");
        MapDefinition original=Map("customs");
        MapDefinition unavailable=new MapDefinition {Id=original.Id,Name=original.Name,Layers=new[]{original.Layers[0]}};
        n=new AutoNavigator(unavailable,source);d=n.Update(P(200,6,150),"base");
        Check(!d.LayerAvailable&&d.SourceLevelName=="3rd Floor"&&d.LayerId=="base"&&d.FocusBounds!=null,"missing source floor is explicit and still focuses known building");
        foreach(MapDefinition m in maps) {n=new AutoNavigator(m,source);n.Update(P(0,0,0),"base");checks++;}
    }
    static int Main(string[] args)
    {
        try
        {
            string assets=Path.GetFullPath(args.Length>0?args[0]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","assets"));
            maps=MapCatalog.Load(assets);source=Path.Combine(assets,"maps.upstream.json");
            MainTests();Console.WriteLine("PASS: "+checks+" automatic navigation checks.");return 0;
        }
        catch(Exception ex) {Console.WriteLine(ex);return 1;}
    }
}
