// SPDX-License-Identifier: GPL-3.0-only
// Build-time import of public possible locations, never live raid contents.
const fs=require('fs'),path=require('path'),crypto=require('crypto');
const root=path.resolve(process.argv[2]),output=path.resolve(process.argv[3]||path.join(__dirname,'../assets/loot.json'));
const maps=JSON.parse(fs.readFileSync(path.join(__dirname,'../assets/maps.json'),'utf8').replace(/^\uFEFF/,''));
const aliases={'night-factory':'factory','ground-zero-21':'ground-zero','the-lab-dark':'the-lab'},modes=['regular','pve','pvp-season'],sources=[];
function read(mode,name){const bytes=fs.readFileSync(path.join(root,`${mode}-${name}.json`));sources.push({Url:`https://json.tarkov.dev/${mode}/${name}`,Bytes:bytes.length,Sha256:crypto.createHash('sha256').update(bytes).digest('hex')});return JSON.parse(bytes).data;}
const inputs=modes.map(mode=>({mode,data:read(mode,'maps'),names:read(mode,'maps_en')}));
const itemData=read('regular','items'),itemNames=read('regular','items_en');
const itemPolish=fs.existsSync(path.join(root,'regular-items_pl.json'))?read('regular','items_pl'):{};
const pl={'Jacket':'Kurtka','Safe':'Sejf','Cash register':'Kasa sklepowa','Duffle bag':'Torba sportowa','Wooden crate':'Drewniana skrzynia','Drawer':'Szafka z szufladami','Medbag':'Torba medyczna','Grenade box':'Skrzynka z granatami','Wooden ammo box':'Drewniana skrzynka z amunicją','Medcase':'Skrzynka medyczna','Toolbox':'Skrzynka narzędziowa','Weapon box':'Skrzynia na broń','Dead Scav':'Martwy Scav','PC block':'Komputer','Plastic suitcase':'Plastikowa walizka',"Shturman's stash":'Skrytka Shturmana','Ground cache':'Skrytka w ziemi','Buried barrel cache':'Zakopana beczka','Ration supply crate':'Skrzynia z żywnością','Technical supply crate':'Skrzynia techniczna','Medical supply crate':'Skrzynia medyczna','Bank cash register':'Kasa bankowa','Bank safe':'Sejf bankowy','Scav body':'Ciało Scava','Lab technician body':'Ciało technika','PMC body':'Ciało PMC','Civilian body':'Ciało cywila'};
const loosePl={'valuables':'Kosztowności','storage-containers':'Pojemniki','electronics':'Elektronika','medical-supplies':'Materiały medyczne','info-items':'Przedmioty informacyjne','electronic-keys':'Klucze elektroniczne','injectors':'Wstrzykiwacze','drinks':'Napoje','others':'Inne','flammable-materials':'Materiały łatwopalne','energy-elements':'Elementy energetyczne','battle-pass-documents':'Dokumenty Battle Pass','mechanical-keys':'Klucze mechaniczne','special-equipment':'Wyposażenie specjalne'};
const category=name=>/cache/i.test(name)?7:/med/i.test(name)?3:/bag|jacket|suitcase/i.test(name)?1:/weapon|ammo|grenade/i.test(name)?2:/ration|food/i.test(name)?4:/PC block/i.test(name)?5:/safe|cash register/i.test(name)?6:8;
const types=[],typeIndices=new Map(),items=[],itemIndices=new Map(),containerIds=new Map();
function addType(type){if(!typeIndices.has(type.Id)){typeIndices.set(type.Id,types.length);types.push(type);}return typeIndices.get(type.Id);}
for(const input of inputs)for(const container of Object.values(input.data.lootContainers)){
 const slug=container.normalizedName,name=slug==='shturmans-stash'?"Shturman's stash":input.names[container.name]||input.names[container.id+' Name']||'Other container';
 containerIds.set(container.id,addType({Id:'container:'+slug,Name:name,LocalName:pl[name]||name,Category:category(name),Section:'container',Icon:slug}));
}
function addItem(id){
 if(itemIndices.has(id))return itemIndices.get(id);
 const item=itemData.items[id];if(!item)throw new Error('Unresolved source item: '+id);
 const hb=itemData.handbookCategories[(item.handbookCategories||[])[0]],slug=hb?.normalizedName||'others',name=itemNames[hb?.name]||'Others';
 const type=addType({Id:'loose:'+slug,Name:name,LocalName:loosePl[slug]||name,Category:0,Section:'loose',Icon:slug});
 const index=items.length;itemIndices.set(id,index);items.push({Id:id,Name:itemNames[item.name]||itemNames[id+' Name']||item.normalizedName,LocalName:itemPolish[item.name]||itemPolish[id+' Name']||itemNames[item.name]||item.normalizedName,Type:type});return index;
}
const hazardTypes={minefield:['Mines','Miny','mine'],sniper:['Sniper zones','Strefy snajperów','sniper'],hazard:['Other hazards','Inne zagrożenia','warning']};
const hazardIndices=Object.fromEntries(Object.entries(hazardTypes).map(([key,[Name,LocalName,Icon]])=>[key,addType({Id:'hazard:'+key,Name,LocalName,Icon,Section:'hazard',Category:8})]));
const locked=addType({Id:'utility:locked-room',Name:'Locked rooms',LocalName:'Zamknięte pomieszczenia',Icon:'lock',Section:'utility',Category:8});
const stationary=addType({Id:'utility:stationary-weapon',Name:'Stationary weapons',LocalName:'Broń stacjonarna',Icon:'stationary',Section:'utility',Category:8});
const pointMaps=new Map(maps.map(m=>[m.Id,new Map()])),discarded={unknownMap:0,invalidPosition:0,outsideMap:0,nonRoomLocks:0};
const round=n=>Math.round(n*1000)/1000,position=p=>p&&[p.x,p.y,p.z].every(Number.isFinite)&&[p.x,p.y,p.z].every(n=>Math.abs(n)<=100000)&&!!(p.x||p.y||p.z);
function overlaps(definition,positions){return definition.Layers.some(l=>{const [a,b,c,d,tx,ty]=l.Affine,pts=positions.map(p=>[a*p.x+b*p.z+tx,c*p.x+d*p.z+ty]);return Math.min(...pts.map(p=>p[0]))<=l.Width+64&&Math.max(...pts.map(p=>p[0]))>=-64&&Math.min(...pts.map(p=>p[1]))<=l.Height+64&&Math.max(...pts.map(p=>p[1]))>=-64;});}
for(const [modeIndex,input] of inputs.entries())for(const map of Object.values(input.data.maps)){
 const mapId=aliases[map.normalizedName]||map.normalizedName,definition=maps.find(m=>m.Id===mapId);if(!definition){discarded.unknownMap++;continue;}
 const entries=[];
 for(const c of map.lootContainers||[])entries.push({position:c.position,Type:containerIds.get(c.lootContainer)});
 for(const l of map.lootLoose||[]){const Items=[...new Set(l.items.map(addItem))].sort((a,b)=>a-b);if(Items.length)entries.push({position:l.position,Type:items[Items[0]].Type,Items});}
 for(const h of map.hazards||[]){if(!(h.hazardType in hazardIndices))throw new Error('Unknown hazard '+h.hazardType);entries.push({position:h.position,Type:hazardIndices[h.hazardType],Outline:(h.outline||[]).filter(position).map(p=>[round(p.x),round(p.z)])});}
 for(const l of map.locks||[]){if(l.lockType!=='door'||!l.key){discarded.nonRoomLocks++;continue;}const key=itemData.items[l.key];if(!key)throw new Error('Unknown room key '+l.key);entries.push({position:l.position,Type:locked,KeyName:itemNames[key.name]||key.normalizedName,LocalKeyName:itemPolish[key.name]||itemNames[key.name]||key.normalizedName,NeedsPower:!!l.needsPower});}
 for(const s of map.stationaryWeapons||[]){const weapon=input.data.stationaryWeapons[s.stationaryWeapon];entries.push({position:s.position,Type:stationary,Detail:input.names[weapon?.name]||'Stationary weapon'});}
 for(const entry of entries){
  const p=entry.position;if(!position(p)){discarded.invalidPosition++;continue;}
  if(!overlaps(definition,[p,...(entry.Outline||[]).map(([x,z])=>({x,z}))])){discarded.outsideMap++;continue;}
  const {position:unused,...payload}=entry,point={...payload,X:round(p.x),Y:round(p.y),Z:round(p.z)};
  const key=JSON.stringify(point),points=pointMaps.get(mapId),old=points.get(key);if(old)old.Modes|=1<<modeIndex;else points.set(key,{...point,Modes:1<<modeIndex});
 }
}
const result={Version:2,Source:'https://tarkov.dev/api',FetchedUtc:new Date().toISOString(),Types:types,Items:items,Maps:maps.map(m=>({Id:m.Id,Points:[...pointMaps.get(m.Id).values()].sort((a,b)=>a.Type-b.Type||a.X-b.X||a.Z-b.Z||a.Y-b.Y)}))};
fs.writeFileSync(output,JSON.stringify(result));
const coverage=result.Maps.map(m=>({Map:m.Id,UniquePoints:m.Points.length,Sections:Object.fromEntries(['container','loose','hazard','utility'].map(s=>[s,m.Points.filter(p=>types[p.Type].Section===s).length])),Modes:Object.fromEntries(modes.map((mode,i)=>[mode,m.Points.filter(p=>p.Modes&(1<<i)).length]))}));
const manifest={Version:2,Documentation:result.Source,RetrievedUtc:result.FetchedUtc,DataSha256:crypto.createHash('sha256').update(fs.readFileSync(output)).digest('hex'),Sources:sources,Coverage:coverage,Discarded:discarded,Notes:['Possible locations, not live raid contents or guaranteed spawns.','XYZ rounded to 0.001; identical point payloads share a mode bitmask.','Container variants share the source normalized type, matching tarkov.dev filters.','Loose-loot filters use the first source handbook category; mixed locations retain every possible item and category.','Locks include keyed doors/rooms only; vehicle trunks are excluded.','Hazard outlines are retained. Icons are original application artwork.']};
fs.writeFileSync(path.join(path.dirname(output),'loot.source.json'),JSON.stringify(manifest,null,2));
console.log(JSON.stringify({Bytes:fs.statSync(output).size,Types:types.length,Items:items.length,Points:coverage.reduce((n,m)=>n+m.UniquePoints,0),Coverage:coverage,Discarded:discarded},null,2));
