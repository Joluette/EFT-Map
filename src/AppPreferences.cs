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
        public AppPreferences() { ScreenshotFolder=""; DeleteScreenshots=true; }
        public static AppPreferences Load(string file)
        {
            try
            {
                if(File.Exists(file) && new FileInfo(file).Length<=16384)
                {
                    AppPreferences p=new JavaScriptSerializer().Deserialize<AppPreferences>(File.ReadAllText(file));
                    if(p!=null && (p.ScreenshotFolder??"").Length<=32767) return p;
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
