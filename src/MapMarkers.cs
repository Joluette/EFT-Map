// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace EftPosition
{
    public class MarkerPoint
    { public string Id {get;set;} public double X {get;set;} public double? Y {get;set;} public double Z {get;set;} }
    public sealed class ExtractMarker : MarkerPoint
    { public string Name {get;set;} public string Faction {get;set;} }
    public sealed class MapQuest
    {
        public string Id {get;set;} public string Name {get;set;} public string LocalName {get;set;}
        public MarkerPoint[] Points {get;set;}
        public override string ToString() {return Name;}
    }
    public sealed class MarkerMode
    { public string Id {get;set;} public ExtractMarker[] Extracts {get;set;} public MapQuest[] Quests {get;set;} }
    public sealed class MarkerMap
    { public string Id {get;set;} public MarkerMode[] Modes {get;set;} }
    public sealed class MarkerCatalog
    {
        public int Version {get;set;} public string Source {get;set;} public string FetchedUtc {get;set;}
        public MarkerMap[] Maps {get;set;}
        public string Error {get;set;}
        public static MarkerCatalog Load(string file)
        {
            try
            {
                if(new FileInfo(file).Length>4*1024*1024) throw new InvalidDataException();
                var catalog=new JavaScriptSerializer {MaxJsonLength=4*1024*1024}.Deserialize<MarkerCatalog>(File.ReadAllText(file));
                if(catalog==null || catalog.Version!=1 || catalog.Maps==null || catalog.Maps.Length>64) throw new InvalidDataException();
                foreach(MarkerMap map in catalog.Maps)
                {
                    if(map==null || String.IsNullOrEmpty(map.Id) || map.Modes==null || map.Modes.Length>3) throw new InvalidDataException();
                    foreach(MarkerMode mode in map.Modes)
                    {
                        if(mode==null || !ValidMode(mode.Id) || mode.Quests==null || mode.Extracts==null || mode.Quests.Length>2000 || mode.Extracts.Length>512) throw new InvalidDataException();
                        foreach(ExtractMarker e in mode.Extracts) {Validate(e);ValidateName(e.Name);}
                        HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
                        foreach(MapQuest q in mode.Quests)
                        {
                            if(q==null)throw new InvalidDataException();ValidateName(q.Name);
                            if(String.IsNullOrEmpty(q.Id) || !ids.Add(q.Id) || q.Points==null || q.Points.Length==0 || q.Points.Length>4096) throw new InvalidDataException();
                            foreach(MarkerPoint p in q.Points) Validate(p);
                        }
                    }
                }
                return catalog;
            }
            catch(Exception ex)
            {
                if(!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException) && !(ex is InvalidOperationException)) throw;
                return new MarkerCatalog {Maps=new MarkerMap[0],Error=UiText.T("Brak danych znaczników. Sprawdź assets/markers.json.")};
            }
        }
        static void ValidateName(string name) {if(String.IsNullOrWhiteSpace(name) || name.Length>512)throw new InvalidDataException();}
        static void Validate(MarkerPoint p)
        {
            if(p==null || Double.IsNaN(p.X) || Double.IsInfinity(p.X) || Double.IsNaN(p.Z) || Double.IsInfinity(p.Z) || Math.Abs(p.X)>100000 || Math.Abs(p.Z)>100000 ||
                (p.Y.HasValue && (Double.IsNaN(p.Y.Value) || Double.IsInfinity(p.Y.Value) || Math.Abs(p.Y.Value)>100000)))throw new InvalidDataException();
        }
        public static bool ValidMode(string mode) {return mode=="regular" || mode=="pve" || mode=="pvp-season";}
        public MarkerMode ForMap(string mapId,string mode)
        {
            MarkerMap map=Maps.FirstOrDefault(m=>m.Id==mapId);
            return (map==null?null:map.Modes.FirstOrDefault(m=>m.Id==mode))??new MarkerMode {Id=mode,Quests=new MapQuest[0],Extracts=new ExtractMarker[0]};
        }
        public static string SearchText(string value)
        {
            var text=new StringBuilder();
            foreach(char ch in (value??"").Normalize(NormalizationForm.FormD).ToLowerInvariant())
                if(CharUnicodeInfo.GetUnicodeCategory(ch)!=UnicodeCategory.NonSpacingMark)text.Append(ch=='ł'?'l':ch);
            return text.ToString();
        }
        public MapQuest[] Search(string mapId,string mode,string query)
        {
            string[] words=SearchText(query).Split(new[]{' ','\t','\r','\n'},StringSplitOptions.RemoveEmptyEntries);
            return ForMap(mapId,mode).Quests.Where(q=>
            {string haystack=SearchText(q.Name+" "+q.LocalName);return words.All(word=>haystack.Contains(word));}).ToArray();
        }
    }
    public enum MapMarkerKind {Quest,Pmc,Scav}
    public sealed class MapMarker
    { public string Name,QuestId;public double X,Z;public double? Y;public MapMarkerKind Kind; }
    public sealed class MarkerPreferences
    {
        public string Mode {get;set;}
        public bool ShowPmc {get;set;} public bool ShowScav {get;set;} public bool ShowQuests {get;set;}
        public Dictionary<string,string[]> Selected {get;set;}
        public MarkerPreferences() {Mode="regular";ShowPmc=true;ShowScav=true;ShowQuests=true;Selected=new Dictionary<string,string[]>();}
    }
    public sealed class MarkerSelection
    {
        public MarkerPreferences Preferences {get;private set;}
        readonly Dictionary<string,HashSet<string>> selected=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
        public MarkerSelection() {Preferences=new MarkerPreferences();}
        public HashSet<string> ForMap(string map)
        {
            string key=Preferences.Mode+"|"+map;HashSet<string> set;
            if(!selected.TryGetValue(key,out set)) selected[key]=set=new HashSet<string>(StringComparer.Ordinal);
            return set;
        }
        public static MarkerSelection Load(string file)
        {
            var state=new MarkerSelection();
            try
            {
                if(!File.Exists(file) || new FileInfo(file).Length>1024*1024)return state;
                var prefs=new JavaScriptSerializer {MaxJsonLength=1024*1024}.Deserialize<MarkerPreferences>(File.ReadAllText(file));
                if(prefs==null || !MarkerCatalog.ValidMode(prefs.Mode) || prefs.Selected==null || prefs.Selected.Count>128)return state;
                state.Preferences=prefs;
                foreach(var entry in prefs.Selected) if(entry.Key.Length<100 && entry.Value!=null && entry.Value.Length<=2000)
                    state.selected[entry.Key]=new HashSet<string>(entry.Value.Where(id=>!String.IsNullOrEmpty(id) && id.Length<128),StringComparer.Ordinal);
            }
            catch(Exception ex) {if(!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException) && !(ex is InvalidOperationException))throw;}
            return state;
        }
        public bool Save(string file)
        {
            try
            {
                Preferences.Selected=selected.ToDictionary(pair=>pair.Key,pair=>pair.Value.OrderBy(id=>id,StringComparer.Ordinal).ToArray());
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
                string temporary=file+".part";
                File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(Preferences),new UTF8Encoding(false));
                if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);
                return true;
            }
            catch(IOException) {return false;} catch(UnauthorizedAccessException) {return false;}
        }
        public MapMarker[] Visible(MarkerCatalog catalog,string map)
        {
            var view=new List<MapMarker>();var data=catalog.ForMap(map,Preferences.Mode);
            foreach(ExtractMarker e in data.Extracts)
            {
                bool pmc=e.Faction=="pmc",scav=e.Faction=="scav";
                if((pmc && !Preferences.ShowPmc) || (scav && !Preferences.ShowScav) || (!pmc && !scav && !Preferences.ShowPmc && !Preferences.ShowScav))continue;
                view.Add(new MapMarker {Name=e.Name,X=e.X,Y=e.Y,Z=e.Z,Kind=scav || (!pmc && !Preferences.ShowPmc)?MapMarkerKind.Scav:MapMarkerKind.Pmc});
            }
            if(Preferences.ShowQuests) foreach(MapQuest q in data.Quests) if(ForMap(map).Contains(q.Id))
                foreach(MarkerPoint p in q.Points)view.Add(new MapMarker {Name=q.Name,QuestId=q.Id,X=p.X,Y=p.Y,Z=p.Z,Kind=MapMarkerKind.Quest});
            return view.ToArray();
        }
    }
}
