// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace EftPosition
{
    public sealed class DetectedGameMap
    {
        public readonly string MapId;
        public readonly DateTime TimeUtc;
        public readonly long Revision;
        public DetectedGameMap(string id,DateTime time,long revision){MapId=id;TimeUtc=time;Revision=revision;}
    }

    // Only application log files are read. No game process access or remote connection.
    public sealed class GameMapMonitor : IDisposable
    {
        static readonly Dictionary<string,string> aliases=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            {"bigmap","customs"},{"customs","customs"},{"factory4_day","factory"},{"factory4_night","factory"},{"factory","factory"},
            {"factory_day","factory"},{"factory_night","factory"},
            {"sandbox","ground-zero"},{"sandbox_high","ground-zero"},{"ground-zero","ground-zero"},
            {"woods","woods"},{"shoreline","shoreline"},{"interchange","interchange"},{"shopping_mall","interchange"},{"lighthouse","lighthouse"},
            {"rezervbase","reserve"},{"rezerv_base","reserve"},{"reserve","reserve"},{"tarkovstreets","streets-of-tarkov"},{"city","streets-of-tarkov"},{"streets-of-tarkov","streets-of-tarkov"},
            {"laboratory","the-lab"},{"the-lab","the-lab"},{"labyrinth","the-labyrinth"},{"the-labyrinth","the-labyrinth"},
            {"terminal","terminal"},{"icebreaker","icebreaker"}
        };
        static readonly Regex location=new Regex(@"\|application\|TRACE-NetworkGameCreate profileStatus[^\r\n]*?\bLocation:\s*(?<map>[a-zA-Z0-9_-]+)(?:\s*[,|]|\s*$)",RegexOptions.Compiled|RegexOptions.IgnoreCase);
        static readonly Regex scene=new Regex(@"\|application\|scene preset path:maps/[a-zA-Z0-9_/-]+\.bundle\s+rcid:(?<map>[a-zA-Z0-9_-]+)\.scenespreset\.asset",RegexOptions.Compiled|RegexOptions.IgnoreCase);
        readonly object gate=new object();
        readonly System.Threading.Timer timer;
        readonly string folder;
        string file,partial="";
        volatile string status="waiting";
        long offset,revision;
        DateTime nextDiscovery=DateTime.MinValue;
        bool disposed;
        volatile DetectedGameMap latest;
        public DetectedGameMap Latest {get{return latest;}}
        public string Folder {get{return folder;}}
        public string Status {get{return status;}}
        public GameMapMonitor(string logFolder,bool run=true)
        {
            folder=logFolder??"";
            if(run)timer=new System.Threading.Timer(delegate{Poll();},null,0,500);
        }
        public static string MapId(string value)
        {string result;return aliases.TryGetValue((value??"").Trim(),out result)?result:null;}
        public static string ParseLine(string line,out DateTime timeUtc)
        {
            timeUtc=DateTime.MinValue;
            if(String.IsNullOrEmpty(line)||line.Length>32768)return null;
            Match match=location.Match(line);if(!match.Success)match=scene.Match(line);
            if(!match.Success)return null;
            DateTime stamp;
            if(line.Length<23 || !DateTime.TryParseExact(line.Substring(0,23),"yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture,DateTimeStyles.AssumeLocal,out stamp))return null;
            timeUtc=stamp.ToUniversalTime();return MapId(match.Groups["map"].Value);
        }
        public static string ResolveFolder(string path)
        {
            if(String.IsNullOrWhiteSpace(path))return null;
            try
            {
                string full=Path.GetFullPath(path.Trim().Trim('"'));
                foreach(string candidate in new[]{Path.Combine(full,"build","Logs"),Path.Combine(full,"Logs"),full})
                    if(Directory.Exists(candidate) && (String.Equals(Path.GetFileName(candidate),"Logs",StringComparison.OrdinalIgnoreCase) ||
                        Directory.GetFiles(candidate,"*application*.log").Length>0 || Directory.GetDirectories(candidate,"log_*").Length>0))return candidate;
            }
            catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(System.Security.SecurityException){}
            return null;
        }
        public static string FindDefaultFolder()
        {
            foreach(RegistryHive hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})
                foreach(RegistryView view in new[]{RegistryView.Registry64,RegistryView.Registry32})
                    try
                    {
                        using(var root=RegistryKey.OpenBaseKey(hive,view))
                            foreach(string key in new[]{@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\EscapeFromTarkov",@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 3932890"})
                                using(var entry=root.OpenSubKey(key))
                                {string found=ResolveFolder(entry==null?null:entry.GetValue("InstallLocation") as string);if(found!=null)return found;}
                    }
                    catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}catch(IOException){}
            return null;
        }
        string DiscoverFile()
        {
            if(!Directory.Exists(folder))return null;
            string session=Directory.GetDirectories(folder,"log_*").OrderByDescending(SessionTime).ThenByDescending(p=>p,StringComparer.OrdinalIgnoreCase).FirstOrDefault()??folder;
            return Directory.GetFiles(session,"*application*.log").Where(p=>Regex.IsMatch(Path.GetFileName(p),@"(?:^| )application(?:_\d+)?\.log$",RegexOptions.IgnoreCase))
                .OrderByDescending(p=>File.GetLastWriteTimeUtc(p)).ThenByDescending(p=>p,StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        }
        static DateTime SessionTime(string path)
        {
            Match m=Regex.Match(Path.GetFileName(path),@"^log_(?<date>\d{4}\.\d{2}\.\d{2})_(?<hour>\d{1,2})-(?<min>\d{2})-(?<sec>\d{2})(?:_|$)");
            DateTime result;
            if(m.Success && DateTime.TryParseExact(m.Groups["date"].Value+" "+m.Groups["hour"].Value.PadLeft(2,'0')+":"+m.Groups["min"].Value+":"+m.Groups["sec"].Value,"yyyy.MM.dd HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out result))return result;
            return Directory.GetCreationTimeUtc(path);
        }
        public void Poll()
        {
            if(!System.Threading.Monitor.TryEnter(gate))return;
            try
            {
                if(disposed)return;
                if(DateTime.UtcNow>=nextDiscovery)
                {
                    nextDiscovery=DateTime.UtcNow.AddSeconds(2);
                    string next=DiscoverFile();
                    if(next!=file){file=next;offset=0;partial="";}
                }
                if(file==null){status="missing";return;}
                using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
                {
                    if(stream.Length<offset){offset=0;partial="";}
                    if(offset==0 && stream.Length>1048576)
                    {offset=stream.Length-1048576;stream.Position=offset;while(stream.Position<stream.Length && stream.ReadByte()!=10){}offset=stream.Position;}
                    stream.Position=offset;
                    byte[] buffer=new byte[(int)Math.Min(262144,stream.Length-offset)];
                    int count=stream.Read(buffer,0,buffer.Length);offset+=count;
                    string text=partial+Encoding.UTF8.GetString(buffer,0,count);int start=0;
                    for(int i=0;i<text.Length;i++)if(text[i]=='\n')
                    {
                        string line=text.Substring(start,i-start).TrimEnd('\r');start=i+1;DateTime time;
                        string id=ParseLine(line,out time);
                        if(id!=null){latest=new DetectedGameMap(id,time,++revision);}
                    }
                    partial=text.Substring(start);if(partial.Length>32768)partial="";
                }
                status=latest==null?"waiting":"detected";
            }
            catch(IOException){status="unavailable";nextDiscovery=DateTime.MinValue;}
            catch(UnauthorizedAccessException){status="unavailable";}
            finally{System.Threading.Monitor.Exit(gate);}
        }
        public void Dispose()
        {lock(gate){if(disposed)return;disposed=true;if(timer!=null)timer.Dispose();}}
    }
}
