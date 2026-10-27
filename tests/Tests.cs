// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using EftPosition;

static class Tests
{
    static int assertions;
    static string root;
    static void Check(bool condition,string message)
    { assertions++; if(!condition) throw new Exception("FAIL: "+message); }
    static void Near(double actual,double expected,double tolerance,string message)
    { Check(Math.Abs(actual-expected)<=tolerance,message+" (actual "+actual+", expected "+expected+")"); }
    static string Name(double x,double y,double z,string q,int counter)
    { return String.Format(CultureInfo.InvariantCulture,"2026-10-26[12-34]_{0:F2}, {1:F2}, {2:F2}_{3} ({4}).png",x,y,z,q,counter); }
    static string Name(double x,int counter) { return Name(x,2.3,100,"0.0, 0.0, 0.0, 1.0",counter); }
    static Position Parse(string name)
    { Position p; Check(ScreenshotParser.TryParse(name,out p),"parse "+name); return p; }
    static void WaitUntil(Func<bool> predicate,string message,int ms)
    {
        DateTime end=DateTime.UtcNow.AddMilliseconds(ms);
        while(DateTime.UtcNow<end) { if(predicate()) { Check(true,message); return; } Thread.Sleep(10); }
        Check(false,message);
    }
    static void Write(string folder,string name,DateTime? time)
    {
        // Deliberately empty: the application must NOT decode the PNG contents.
        string p=Path.Combine(folder,name);
        using(File.Create(p)) { }
        if(time.HasValue) File.SetLastWriteTimeUtc(p,time.Value);
    }
    static void ParserTests()
    {
        Console.WriteLine("Parser: format, optional separators, culture, quaternion, invalid input");
        Position p=Parse(Name(-123.45,-7.89,200.12,"0.0, 0.0, 0.0, 1.0",0));
        Near(p.X,-123.45,1e-10,"negative X"); Near(p.Y,-7.89,1e-10,"negative Y"); Near(p.Z,200.12,1e-10,"Z");
        Near(p.Yaw,0,1e-8,"forward yaw");
        Check(p.NameTime==new DateTime(2026,10,26,12,34,0),"time from filename");
        Parse(Name(1,0,2,"0.0, 0.0, 0.0, 1.0",23).Replace("_",""));
        Parse(Name(1,0,2,"0.0, 0.0, 0.0, 1.0",1).ToUpperInvariant());
        CultureInfo saved=Thread.CurrentThread.CurrentCulture;
        try
        {
            foreach(string culture in new[]{"pl-PL","de-DE","en-US"})
            { Thread.CurrentThread.CurrentCulture=new CultureInfo(culture); Near(Parse(Name(11.25,0,10,"0.0, 0.0, 0.0, 1.0",0)).X,11.25,1e-10,"invariant "+culture); }
        }
        finally { Thread.CurrentThread.CurrentCulture=saved; }
        string[] rotations={"0.0, 0.0, 0.0, 1.0","0.0, 0.70711, 0.0, 0.70711","0.0, 1.0, 0.0, 0.0","0.0, -0.70711, 0.0, 0.70711"};
        for(int i=0;i<rotations.Length;i++) Near(Parse(Name(0,0,0,rotations[i],0)).Yaw,i*90,0.001,"cardinal yaw "+i);
        // yaw=45, pitch=60, q = yaw quaternion * pitch quaternion (independent fixture).
        p=Parse(Name(0,0,0,"0.46194, 0.33141, -0.19134, 0.80010",0));
        Near(p.Yaw,45,0.01,"pitched yaw keeps heading");
        p=Parse(Name(0,0,0,"0.70711, 0.0, 0.0, 0.70711",0));
        Check(!p.HasHeading,"vertical look has no horizontal direction");
        // Regression: real game screenshots may append _16.60 before (counter).
        // Non-capturing metadata must not move the counter's capture group.
        string extended=Name(84.53,24.14,211.53,"-0.00027, -0.99904, 0.00617, -0.04332",37);
        Position legacy=Parse(extended);
        foreach(string suffix in new[]{"16.60","16.32","16.29","16.17","16.16","16.14","16","16.600","-0.50"})
        {
            p=Parse(extended.Replace(" (37)","_"+suffix+" (37)"));
            Near(p.X,84.53,1e-10,"extended filename X");Near(p.Y,24.14,1e-10,"extended filename Y");Near(p.Z,211.53,1e-10,"extended filename Z");
            Near(p.Yaw,legacy.Yaw,1e-10,"metadata does not change heading");
            Check(p.Counter==37 && p.NameTime==legacy.NameTime,"metadata does not replace counter or timestamp");
        }
        foreach(string suffix in new[]{"NaN","Infinity","word","16,60","16.60_extra","16.60_16.61","","16.60.1"})
        { Position ignored;Check(!ScreenshotParser.TryParse(extended.Replace(" (37)","_"+suffix+" (37)"),out ignored),"reject malformed trailing metadata"); }
        string good=Name(0,0,0,"0.0, 0.0, 0.0, 1.0",0);
        string[] invalid={null,"","screen.png","junk"+good,good+".exe",good.Replace(".png",".jpg"),
            good.Replace("2026-10-26","2026-02-30"),good.Replace("12-34","25-99"),good.Replace("0.00, 0.00, 0.00","0,00, 0,00, 0,00"),
            good.Replace("0.0, 0.0, 0.0, 1.0","0.0, 0.0, 0.0, 0.0"),
            good.Replace("0.0, 0.0, 0.0, 1.0","5.0, 5.0, 5.0, 5.0"),
            good.Replace("0.00, 0.00, 0.00","NaN, 0.00, 0.00"),
            good.Replace("0.00, 0.00, 0.00","100001.00, 0.00, 0.00"),new string('0',300)};
        foreach(string name in invalid) { Position ignored; Check(!ScreenshotParser.TryParse(name,out ignored),"reject malformed name"); }
    }
    static void MapTests()
    {
        Console.WriteLine("Projection: independent corner/landmark fixtures, inverse, headings, bounds");
        MapPoint p=CustomsMap.Project(698,-307); Near(p.X,0,1e-10,"top-left X"); Near(p.Y,0,1e-10,"top-left Y");
        p=CustomsMap.Project(-372,237); Near(p.X,3210,1e-10,"bottom-right X"); Near(p.Y,1632,1e-10,"bottom-right Y");
        p=CustomsMap.Project(163,-35); Near(p.X,1605,1e-10,"center X"); Near(p.Y,816,1e-10,"center Y");
        // Landmarks from maps.json, not generated from our projection implementation.
        p=CustomsMap.Project(200,150); Near(p.X,1494,1e-10,"Dorms X"); Near(p.Y,1371,1e-10,"Dorms Y");
        p=CustomsMap.Project(-215,-119); Near(p.X,2739,1e-10,"Big Red X"); Near(p.Y,564,1e-10,"Big Red Y");
        Random random=new Random(17);
        for(int i=0;i<500;i++)
        {
            double x=-372+random.NextDouble()*1070, z=-307+random.NextDouble()*544;
            p=CustomsMap.Project(x,z); MapPoint w=CustomsMap.Unproject(p.X,p.Y);
            Near(w.X,x,1e-9,"inverse X"); Near(w.Y,z,1e-9,"inverse Z");
        }
        double[] dx={0,-1,0,1},dy={1,0,-1,0};
        for(int i=0;i<4;i++) { p=CustomsMap.Direction(i*90); Near(p.X,dx[i],1e-8,"direction X"); Near(p.Y,dy[i],1e-8,"direction Y"); }
        Check(CustomsMap.Contains(Parse(Name(698,99,-307,"0.0, 0.0, 0.0, 1.0",0))),"edge included, Y does not affect projection");
        Check(!CustomsMap.Contains(Parse(Name(699,0,0,"0.0, 0.0, 0.0, 1.0",0))),"outside bounds");
        DateTime tie=new DateTime(2026,10,26,12,34,0,DateTimeKind.Utc);
        Snapshot a=new Snapshot(Parse(Name(-2,11998)),tie,tie,tie);
        Snapshot b=new Snapshot(Parse(Name(-1,11999)),tie,tie,tie);
        Check(b.IsNewerThan(a),"identical filesystem times use numeric counter, not coordinate text");
        Check(!a.IsNewerThan(b),"tie ordering does not rewind");
        Snapshot c=new Snapshot(Parse(Name(-3,0)),tie,tie.AddTicks(1),tie);
        Check(c.IsNewerThan(b),"creation timestamp resolves equal write times");
    }
    static void WatcherTests()
    {
        Console.WriteLine("Watcher: startup scan, created, rename, invalid PNG, order, burst, recovery, dispose");
        string folder=Path.Combine(root,"watch"); Directory.CreateDirectory(folder);
        DateTime t=DateTime.UtcNow.AddDays(-1);
        Write(folder,Name(10,0),t); Write(folder,"invalid.png",t.AddHours(1)); Write(folder,Name(20,1).Replace(" (1)","_16.60 (1)"),t.AddMinutes(1));
        Directory.CreateDirectory(Path.Combine(folder,Name(99,8)));
        using(FolderMonitor m=new FolderMonitor(folder))
        {
            WaitUntil(delegate { return m.Latest!=null && m.Latest.Position.X==20; },"startup latest valid (invalid newest skipped)",5000);
            Write(folder,Name(30,2).Replace(" (2)","_16.75 (2)"),null);
            WaitUntil(delegate { return m.Latest.Position.X==30; },"Created event",5000);
            Thread.Sleep(50); long accepted=m.Accepted;
            m.RequestRescan(); Thread.Sleep(200);
            Check(m.Accepted==accepted,"duplicate scan does not update current position");
            Write(folder,Name(40,3),t);
            Thread.Sleep(200); Check(m.Latest.Position.X==30,"old arriving file never rewinds position");
            Write(folder,"bad.png",null); Write(folder,"unrelated.txt",null);
            WaitUntil(delegate { return m.Ignored>=2; },"malformed PNG counted",5000);
            Check(m.Latest.Position.X==30,"invalid input preserves marker");
            string temp=Path.Combine(folder,"pending.tmp"); File.WriteAllText(temp,"not an image");
            File.Move(temp,Path.Combine(folder,Name(50,4)));
            WaitUntil(delegate { return m.Latest.Position.X==50; },"rename to PNG",5000);
            for(int i=0;i<2000;i++) Write(folder,Name(100+(i%400),100+i),null);
            Thread.Sleep(200);
            string sentinel=Name(612.34,1,-10,"0.0, 0.70711, 0.0, 0.70711",9999).Replace(" (9999)","_17.00 (9999)");
            Write(folder,sentinel,null);
            m.RequestRescan();
            WaitUntil(delegate { return m.Latest.Position.FileName==sentinel; },"2000-file burst retains newest",10000);
            Check(m.CacheCount<=256,"duplicate cache bounded at 256");
            Check(Directory.GetFiles(folder,"*.png").Length==2008,"screenshots retained");
        }
        string recoveryFolder=Path.Combine(root,"recovery"); Directory.CreateDirectory(recoveryFolder);
        using(FolderMonitor m=new FolderMonitor(recoveryFolder))
        {
            // Only the empty test directory is removed; never a user screenshot folder.
            Directory.Delete(recoveryFolder);
            Thread.Sleep(2400);
            Directory.CreateDirectory(recoveryFolder);
            Write(recoveryFolder,Name(42,0),null);
            WaitUntil(delegate { return m.Latest!=null && m.Latest.Position.X==42; },"folder recreated and rescanned",8000);
        }
        using(FolderMonitor m=new FolderMonitor(recoveryFolder))
        {
            WaitUntil(delegate { return m.Latest!=null; },"second monitor starts",5000);
            m.Dispose();
            Snapshot before=m.Latest;
            Write(recoveryFolder,Name(43,1),null);
            Thread.Sleep(200); Check(m.Latest==before,"disposed monitor ignores further events");
        }
        bool missing=false;
        try { using(FolderMonitor m=new FolderMonitor(Path.Combine(root,"missing"))) { } }
        catch(DirectoryNotFoundException) { missing=true; }
        Check(missing,"missing folder gives clear error");
        Check(File.Exists(Path.Combine(folder,Name(10,0))),"original fixture preserved");
    }
    static void AllMapsTests()
    {
        Console.WriteLine("All maps: 13 catalog entries, independent rotation fixtures, SVG bounds, affine inverse, headings");
        string assets=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","assets"));
        MapDefinition[] maps=MapCatalog.Load(assets);
        Check(maps.Length==13,"all interactive locations");
        MapDefinition factory=null,reserve=null,customs=null,labs=null,ice=null;
        int offline=0,online=0,layers=0;
        foreach(MapDefinition m in maps)
        {
            if(m.Id=="factory") factory=m;
            if(m.Id=="reserve") reserve=m;
            if(m.Id=="customs") customs=m;
            if(m.Id=="the-lab") labs=m;
            if(m.Id=="icebreaker") ice=m;
            if(m.Layers[0].Remote) online++; else offline++;
            foreach(MapLayer l in m.Layers)
            {
                layers++;
                Check(l.Width<=3210 && l.Height<=3210,"bounded bitmap dimensions "+m.Id);
                if(!l.Remote) Check(File.Exists(Path.Combine(assets,l.Image)),"offline layer included "+m.Id+"/"+l.Id);
                Random random=new Random(17);
                for(int i=0;i<40;i++)
                {
                    double px=random.NextDouble()*l.Width,py=random.NextDouble()*l.Height;
                    MapPoint world=l.Unproject(px,py),back=l.Project(world.X,world.Y);
                    Near(back.X,px,1e-7,"inverse pixel X "+m.Id); Near(back.Y,py,1e-7,"inverse pixel Y "+m.Id);
                }
                foreach(double yaw in new[]{0.0,45,90,180,270})
                { MapPoint d=l.Direction(yaw); Near(d.X*d.X+d.Y*d.Y,1,1e-8,"unit screen direction "+m.Id); }
            }
        }
        Check(offline==10 && online==3,"10 bundled and 3 downloadable map sources");
        Check(layers==55,"55 selectable map levels");
        MapPoint p=factory.Layers[0].Project(77,67.4);
        Near(p.X,0,1e-8,"Factory rotated top left X"); Near(p.Y,0,1e-8,"Factory rotated top left Y");
        p=factory.Layers[0].Project(-65.5,-64.5);
        Near(p.X,factory.Layers[0].Width,1e-8,"Factory bottom right X"); Near(p.Y,factory.Layers[0].Height,1e-8,"Factory bottom right Y");
        p=factory.Layers[0].Direction(0); Near(p.X,-1,1e-8,"Factory forward points left"); Near(p.Y,0,1e-8,"Factory forward Y");
        p=labs.Layers[0].Direction(0); Near(p.X,1,1e-8,"Labs 270 degree forward points right"); Near(p.Y,0,1e-8,"Labs forward Y");
        p=reserve.Layers[0].Project(289,-274); Near(p.X,0,1e-8,"Reserve SVG bounds X"); Near(p.Y,0,1e-8,"Reserve uses SVG -274, not satellite -293");
        p=customs.Layers[0].Project(200,150); Near(p.X,1494,1e-8,"Customs keeps calibrated Dorms X"); Near(p.Y,1371,1e-8,"Customs keeps calibrated Dorms Y");
        p=ice.Layers[0].Direction(45); Near(Math.Abs(p.Y/p.X),1.75,1e-8,"Icebreaker anisotropic CRS applied to heading");
    }
    static int Main(string[] args)
    {
        try
        {
            // Test fixtures are deliberately kept for inspection. No screenshot is deleted.
            string basePath=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"EftPosition-tests");
            root=Path.Combine(basePath,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(root);
            ParserTests(); MapTests(); AllMapsTests(); WatcherTests();
            Console.WriteLine("PASS: "+assertions+" assertions. Fixtures retained: "+root);
            return 0;
        }
        catch(Exception ex) { Console.WriteLine(ex.ToString()); return 1; }
    }
}
