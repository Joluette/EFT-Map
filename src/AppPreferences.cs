// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace EftPosition
{
    public sealed class AppPreferences
    {
        public string ScreenshotFolder { get; set; }
        public bool DeleteScreenshots { get; set; }
        public string Language {get;set;}
        public string Theme {get;set;}
        public int AccentArgb {get;set;}
        public string BackgroundMode {get;set;}
        public string BackgroundFile {get;set;}
        public long BackgroundRevision {get;set;}
        public int BackgroundDim {get;set;}
        public int MapOpacity {get;set;}
        public int WindowOpacity {get;set;}
        public bool FullscreenOpacityOnly {get;set;}
        public int MarkerSize {get;set;}
        public bool ShowStatusBar {get;set;}
        public bool RoundedCorners {get;set;}
        public bool OverlayEnabled {get;set;}
        public int OverlaySize {get;set;}
        public int OverlayZoom {get;set;}
        public int OverlayOpacity {get;set;}
        public string OverlayCorner {get;set;}
        public string OverlayMonitor {get;set;}
        public bool OverlayCustomPosition {get;set;}
        public int OverlayX {get;set;}
        public int OverlayY {get;set;}
        public bool AutoMapEnabled {get;set;}
        public string GameLogsFolder {get;set;}
        public AppPreferences()
        {
            ScreenshotFolder=""; DeleteScreenshots=true; Language="pl"; ResetAppearance();
            OverlaySize=320;OverlayZoom=180;OverlayOpacity=90;OverlayCorner="top-right";OverlayMonitor="";
            AutoMapEnabled=false;GameLogsFolder="";
        }
        public void ResetAppearance()
        { Theme="tarkov";AccentArgb=0;BackgroundMode="tarkov";BackgroundFile="";BackgroundRevision=0;BackgroundDim=30;MapOpacity=96;WindowOpacity=100;FullscreenOpacityOnly=true;MarkerSize=100;ShowStatusBar=true;RoundedCorners=true; }
        public void Normalize()
        {
            if(Language!="pl" && Language!="en")Language="pl";
            if((GameLogsFolder??"").Length>32767)GameLogsFolder="";
            OverlaySize=Math.Max(240,Math.Min(520,OverlaySize));OverlayZoom=Math.Max(100,Math.Min(400,OverlayZoom));
            OverlayOpacity=Math.Max(30,Math.Min(100,OverlayOpacity));
            if(OverlayCorner!="top-left" && OverlayCorner!="top-right" && OverlayCorner!="bottom-left" && OverlayCorner!="bottom-right")OverlayCorner="top-right";
            if((OverlayMonitor??"").Length>200)OverlayMonitor="";
            OverlayX=Math.Max(-100000,Math.Min(100000,OverlayX));OverlayY=Math.Max(-100000,Math.Min(100000,OverlayY));
            if(Theme!="tarkov" && Theme!="night" && Theme!="forest")Theme="tarkov";
            if(BackgroundMode!="tarkov" && BackgroundMode!="custom" && BackgroundMode!="plain" && BackgroundMode!="cyberpunk")BackgroundMode="tarkov";
            if((BackgroundFile??"").Length>32767)BackgroundFile="";
            BackgroundDim=Math.Max(0,Math.Min(90,BackgroundDim));MapOpacity=Math.Max(40,Math.Min(100,MapOpacity));
            WindowOpacity=Math.Max(30,Math.Min(100,WindowOpacity));MarkerSize=Math.Max(80,Math.Min(150,MarkerSize));
        }
        public static AppPreferences Load(string file)
        {
            try
            {
                if(File.Exists(file) && new FileInfo(file).Length<=16384)
                {
                    AppPreferences p=new JavaScriptSerializer().Deserialize<AppPreferences>(File.ReadAllText(file));
                    if(p!=null && (p.ScreenshotFolder??"").Length<=32767){p.Normalize();return p;}
                }
            }
            catch(IOException) {} catch(UnauthorizedAccessException) {} catch(ArgumentException) {} catch(InvalidOperationException) {}
            return new AppPreferences();
        }
        public bool Save(string file)
        {
            string temp=file+".part";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
                File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this));
                if(File.Exists(file)) File.Replace(temp,file,null); else File.Move(temp,file);
                return true;
            }
            catch(IOException) {return false;} catch(UnauthorizedAccessException) {return false;}
        }
    }
}
