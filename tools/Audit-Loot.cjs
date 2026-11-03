// SPDX-License-Identifier: GPL-3.0-only
const fs=require('fs'),path=require('path'),assert=require('assert');
const raw=path.resolve(process.argv[2]),file=path.resolve(process.argv[3]),data=JSON.parse(fs.readFileSync(file));
const definitions=JSON.parse(fs.readFileSync(path.join(__dirname,'../assets/maps.json'),'utf8').replace(/^\uFEFF/,''));
const itemData=JSON.parse(fs.readFileSync(path.join(raw,'regular-items.json'))).data;
const itemNames=JSON.parse(fs.readFileSync(path.join(raw,'regular-items_en.json'))).data;
const aliases={'night-factory':'factory','ground-zero-21':'ground-zero','the-lab-dark':'the-lab'},round=n=>Math.round(n*1000)/1000;
function signature(type,p,items=[],key=null,power=false,detail=null,outline=null){return JSON.stringify([type,round(p.x),round(p.y),round(p.z),items.sort(),key,!!power,detail,outline]);}
const result=new Map(data.Maps.map(m=>[m.Id,m]));let checked=0,complete=0;
assert.equal(data.Version,2);assert.equal(result.size,13);assert.equal(new Set(data.Types.map(t=>t.Id)).size,data.Types.length);
for(const item of data.Items){const source=itemData.items[item.Id],category=itemData.handbookCategories[source.handbookCategories[0]];assert.equal(data.Types[item.Type].Id,'loose:'+(category?.normalizedName||'others'));}
for(const [modeIndex,mode]of ['regular','pve','pvp-season'].entries()){
 const source=JSON.parse(fs.readFileSync(path.join(raw,mode+'-maps.json'))).data,names=JSON.parse(fs.readFileSync(path.join(raw,mode+'-maps_en.json'))).data;
 const expected=new Map(data.Maps.map(m=>[m.Id,new Set()]));
 for(const map of Object.values(source.maps)){
  const id=aliases[map.normalizedName]||map.normalizedName,definition=definitions.find(m=>m.Id===id);if(!definition)continue;
  const add=(type,p,items,key,power,detail,outline)=>{
   if(!p||![p.x,p.y,p.z].every(Number.isFinite)||![p.x,p.y,p.z].some(Boolean))return;
   const points=[p,...(outline||[]).map(([x,z])=>({x,z}))];let inBounds=false;
   for(const l of definition.Layers){let xx=[],yy=[];for(const point of points){const [a,b,c,d,tx,ty]=l.Affine;xx.push(a*point.x+b*point.z+tx);yy.push(c*point.x+d*point.z+ty);}if(Math.max(...xx)>=-64&&Math.min(...xx)<=l.Width+64&&Math.max(...yy)>=-64&&Math.min(...yy)<=l.Height+64)inBounds=true;}
   if(inBounds)expected.get(id).add(signature(type,p,items,key,power,detail,outline));
  };
  for(const c of map.lootContainers)add('container:'+source.lootContainers[c.lootContainer].normalizedName,c.position);
  for(const l of map.lootLoose){assert(l.items.length>0);const ids=[...new Set(l.items)];add('loose',l.position,ids);}
  for(const h of map.hazards)add('hazard:'+h.hazardType,h.position,[],null,false,null,h.outline.map(p=>[round(p.x),round(p.z)]));
  for(const l of map.locks)if(l.lockType==='door'&&l.key)add('utility:locked-room',l.position,[],itemNames[itemData.items[l.key].name]||itemData.items[l.key].normalizedName,l.needsPower);
  for(const w of map.stationaryWeapons)add('utility:stationary-weapon',w.position,[],null,false,names[source.stationaryWeapons[w.stationaryWeapon].name]||'Stationary weapon');
 }
 for(const map of data.Maps){const actual=new Set();for(const p of map.Points)if(p.Modes&(1<<modeIndex)){
   const type=data.Types[p.Type],key=signature(p.Items?'loose':type.Id,{x:p.X,y:p.Y,z:p.Z},(p.Items||[]).map(i=>data.Items[i].Id),p.KeyName||null,p.NeedsPower,p.Detail,p.Outline);
   assert(expected.get(map.Id).has(key),`No raw match: ${mode} ${map.Id} ${key}`);assert(!actual.has(key),'Duplicate payload in export');actual.add(key);checked++;
  }
  assert.equal(actual.size,expected.get(map.Id).size,`Incomplete source coverage: ${mode} ${map.Id}`);complete+=actual.size;
 }
}
console.log(JSON.stringify({Result:'PASS',MatchedPointsAcrossModes:checked,CompletePointsAcrossModes:complete,Maps:data.Maps.length,Types:data.Types.length,ItemClassifications:data.Items.length},null,2));
