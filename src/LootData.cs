// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
namespace EftPosition
{
    public sealed class LootType
    {
        public string Id {get;set;} public string Name {get;set;} public string LocalName {get;set;} public int Category {get;set;}
        public string Section {get;set;} public string Icon {get;set;}
        public string Group {get{return Section??(Category==0?"loose":"container");}}
        public string DisplayName {get{return UiText.Language=="en"?Name:LocalName;}}
    }
    public sealed class LootItem
    {
        public string Id {get;set;} public string Name {get;set;} public string LocalName {get;set;} public int Type {get;set;}
        public string DisplayName {get{return UiText.Language=="en" || String.IsNullOrEmpty(LocalName)?Name:LocalName;}}
    }
    public sealed class LootPoint
    {
        public double X {get;set;}public double Y {get;set;}public double Z {get;set;}public int Type {get;set;}public int Modes {get;set;}
        public int[] Items {get;set;} public int? Key {get;set;} public bool NeedsPower {get;set;}public string Detail {get;set;}
        public string KeyName {get;set;}public string LocalKeyName {get;set;}
        public double[][] Outline {get;set;}
    }
    public sealed class LootMap
    {public string Id {get;set;}public LootPoint[] Points {get;set;}}
    public sealed class LootCatalog
    {
        public int Version {get;set;}public string Source {get;set;}public string FetchedUtc {get;set;}
        public LootType[] Types {get;set;}public LootMap[] Maps {get;set;}public LootItem[] Items {get;set;}
        public string Error {get;private set;}
        public static LootCatalog Load(string file)
        {
            try
            {
                if(new FileInfo(file).Length>8*1024*1024)throw new InvalidDataException();
                var data=new JavaScriptSerializer {MaxJsonLength=8*1024*1024}.Deserialize<LootCatalog>(File.ReadAllText(file));
                if(data==null || (data.Version!=1 && data.Version!=2) || data.Types==null || data.Types.Length==0 || data.Types.Length>256 || data.Maps==null || data.Maps.Length>64)throw new InvalidDataException();
                foreach(var t in data.Types)if(t==null || String.IsNullOrWhiteSpace(t.Name) || t.Name.Length>128 || String.IsNullOrWhiteSpace(t.LocalName) || t.LocalName.Length>128 || t.Category<0 || t.Category>8)throw new InvalidDataException();
                if(data.Items==null)data.Items=new LootItem[0];
                if(data.Items.Length>10000)throw new InvalidDataException();
                var typeIds=new HashSet<string>(StringComparer.Ordinal);
                foreach(var t in data.Types)if(data.Version==2 && (String.IsNullOrEmpty(t.Id) || t.Id.Length>100 || !typeIds.Add(t.Id) || !new[]{"container","loose","hazard","utility"}.Contains(t.Section) || String.IsNullOrEmpty(t.Icon) || t.Icon.Length>80))throw new InvalidDataException();
                foreach(var item in data.Items)if(item==null || String.IsNullOrEmpty(item.Id) || item.Id.Length>80 || String.IsNullOrEmpty(item.Name) || item.Name.Length>256 || (item.LocalName??"").Length>256 || item.Type<0 || item.Type>=data.Types.Length)throw new InvalidDataException();
                var ids=new HashSet<string>(StringComparer.Ordinal);int total=0;
                foreach(var map in data.Maps)
                {
                    if(map==null || String.IsNullOrEmpty(map.Id) || map.Id.Length>80 || !ids.Add(map.Id) || map.Points==null || (total+=map.Points.Length)>100000)throw new InvalidDataException();
                    foreach(var p in map.Points)
                    {
                        if(p==null || p.Type<0 || p.Type>=data.Types.Length || p.Modes<=0 || (p.Modes&~7)!=0 || !Coordinate(p.X) || !Coordinate(p.Y) || !Coordinate(p.Z) || (p.Detail??"").Length>256 || (p.KeyName??"").Length>256 || (p.LocalKeyName??"").Length>256)throw new InvalidDataException();
                        if(p.Items!=null && (p.Items.Length==0 || p.Items.Length>1024 || p.Items.Any(i=>i<0 || i>=data.Items.Length) || p.Items.Distinct().Count()!=p.Items.Length || p.Items.Any(i=>data.Types[data.Items[i].Type].Group!="loose")))throw new InvalidDataException();
                        if(p.Key.HasValue && (p.Key.Value<0 || p.Key.Value>=data.Items.Length))throw new InvalidDataException();
                        if(p.Outline!=null && (p.Outline.Length<3 || p.Outline.Length>256 || p.Outline.Any(v=>v==null || v.Length!=2 || !Coordinate(v[0]) || !Coordinate(v[1]))))throw new InvalidDataException();
                    }
                }
                return data;
            }
            catch(Exception ex)
            {
                if(!(ex is IOException) && !(ex is InvalidDataException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException) && !(ex is InvalidOperationException))throw;
                return new LootCatalog {Version=1,Types=new LootType[0],Maps=new LootMap[0],Error=UiText.T("Dane lootu są niedostępne.")};
            }
        }
        static bool Coordinate(double n){return !Double.IsNaN(n) && !Double.IsInfinity(n) && Math.Abs(n)<=100000;}
        public LootPoint[] ForMap(string map){var found=Maps.FirstOrDefault(m=>m.Id==map);return found==null?new LootPoint[0]:found.Points;}
        public static int ModeBit(string mode){return mode=="pve"?2:mode=="pvp-season"?4:1;}
    }
    public sealed class LootPreferences
    {
        public bool Enabled {get;set;}public bool ShowOnMiniMap {get;set;}public bool GroupNearby {get;set;}public bool AllFloors {get;set;}public int Categories {get;set;}
        public string[] DisabledTypes {get;set;}
        public LootPreferences(){Enabled=true;GroupNearby=true;Categories=511;DisabledTypes=new string[0];}
        public bool Allows(LootType type){return (Categories&(1<<type.Category))!=0 && (DisabledTypes==null || !DisabledTypes.Contains(type.Id));}
        public void SetType(LootType type,bool value)
        {var ids=new HashSet<string>(DisabledTypes??new string[0],StringComparer.Ordinal);if(value){ids.Remove(type.Id);Categories|=1<<type.Category;}else ids.Add(type.Id);DisabledTypes=ids.OrderBy(s=>s,StringComparer.Ordinal).ToArray();}
        public static LootPreferences Load(string file)
        {
            try
            {
                if(!File.Exists(file) || new FileInfo(file).Length>16384)return new LootPreferences();
                var p=new JavaScriptSerializer().Deserialize<LootPreferences>(File.ReadAllText(file));if(p==null || p.Categories<0 || p.Categories>511 || (p.DisabledTypes!=null && (p.DisabledTypes.Length>256 || p.DisabledTypes.Any(id=>String.IsNullOrEmpty(id) || id.Length>100))))return new LootPreferences();return p;
            }
            catch(Exception ex){if(!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException) && !(ex is InvalidOperationException))throw;return new LootPreferences();}
        }
        public bool Save(string file)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));string temporary=file+".part";
                File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(this),new UTF8Encoding(false));
                if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);return true;
            }
            catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
        }
    }
}
