// SPDX-License-Identifier: GPL-3.0-only
// Input snapshots: https://json.tarkov.dev/{mode}/{maps,tasks}{,_en,_pl}
// This build-time tool never reads screenshots or game files.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const input = path.resolve(process.argv[2]);
const output = path.resolve(process.argv[3] || path.join(__dirname,'../assets/markers.json'));
const catalog = JSON.parse(fs.readFileSync(path.join(__dirname,'../assets/maps.json'),'utf8').replace(/^\uFEFF/,''));
const aliases = {'night-factory':'factory','ground-zero-21':'ground-zero','the-lab-dark':'the-lab'};
const report={sources:[],modes:[],discarded:{outsideBounds:0,noMap:0,invalidPosition:0},notes:[]};
const result={Version:1,Source:'https://tarkov.dev/api',FetchedUtc:new Date().toISOString(),Maps:catalog.map(m=>({Id:m.Id,Modes:[]}))};
const read=name=>{const bytes=fs.readFileSync(path.join(input,name));report.sources.push({file:name,sha256:crypto.createHash('sha256').update(bytes).digest('hex'),bytes:bytes.length});return JSON.parse(bytes.toString('utf8').replace(/^\uFEFF/,''));};
const key=value=>typeof value==='string'?value:value?.id;
const canonical=name=>aliases[name]||name;
const nearKey=p=>[p.X,p.Y,p.Z].map(x=>x==null?'?':Math.round(x*10)).join(',');
for(const mode of ['regular','pve','pvp-season']) {
  const maps=read(`${mode}-maps.json`).data.maps,tasks=read(`${mode}-tasks.json`).data.tasks;
  const mapNames=read(`${mode}-maps_en.json`).data,taskNames=read(`${mode}-tasks_en.json`).data,plNames=read(`${mode}-tasks_pl.json`).data;
  const apiMaps=Object.values(maps),toMap=new Map(apiMaps.map(m=>[m.id,canonical(m.normalizedName)]));
  const nameOf=(name,translations)=>translations[name]||name;
  function point(p,mapId,id) {
    const def=catalog.find(m=>m.Id===mapId);
    if(!def){report.discarded.noMap++;return null;}
    if(!p || !Number.isFinite(p.x) || !Number.isFinite(p.z) || (p.x===0 && p.y===0 && p.z===0)) {report.discarded.invalidPosition++;return null;}
    const inside=def.Layers.some(l=>{const [a,b,c,d,tx,ty]=l.Affine,px=a*p.x+b*p.z+tx,py=c*p.x+d*p.z+ty;return px>=-64 && py>=-64 && px<=l.Width+64 && py<=l.Height+64;});
    if(!inside){report.discarded.outsideBounds++;return null;}
    return {Id:id,X:p.x,Y:Number.isFinite(p.y)?p.y:null,Z:p.z};
  }
  for(const target of result.Maps) {
    const sourceMaps=apiMaps.filter(m=>toMap.get(m.id)===target.Id).sort((a,b)=>(a.normalizedName===target.Id?-1:0)-(b.normalizedName===target.Id?-1:0));
    const extracts=[],extractSeen=new Set();
    for(const m of sourceMaps) for(const e of m.extracts||[]) {
      const p=point(e.position,target.Id,e.id);if(!p)continue;
      const name=nameOf(e.name,mapNames),faction=(e.faction||'unknown').toLowerCase();
      if(extracts.some(old=>old.Name===name && Math.abs(old.X-p.X)<1 && Math.abs(old.Z-p.Z)<1))continue;
      const unique=name.toLowerCase()+'|'+nearKey(p);
      if(extractSeen.has(unique))continue;extractSeen.add(unique);
      extracts.push({...p,Name:name,Faction:faction});
    }
    const quests=[];
    for(const task of Object.values(tasks)) {
      const points=[],seen=new Set();
      const add=(pos,id)=>{const p=point(pos,target.Id,id);if(!p)return;const k=nearKey(p);if(!seen.has(k)){seen.add(k);points.push(p);}};
      for(const objective of task.objectives||[]) {
        let exact=false;
        for(const location of objective.possibleLocations||[]) {
          if(toMap.get(key(location.map))!==target.Id)continue;
          for(const [i,p] of (location.positions||[]).entries()){add(p,objective.id+':item:'+i);exact=true;}
        }
        if(!exact) for(const zone of objective.zones||[]) {
          if(toMap.get(key(zone.map))===target.Id)add(zone.position,objective.id+':zone:'+zone.id);
        }
        if(objective.exitName && (objective.maps||[]).some(m=>toMap.get(key(m))===target.Id)) {
          const raw=objective.exitName,translated=nameOf(raw,mapNames).toLowerCase();
          for(const m of sourceMaps) for(const e of m.extracts||[]) if(e.name===raw || nameOf(e.name,mapNames).toLowerCase()===translated)
            add(e.position,objective.id+':extract:'+e.id);
        }
      }
      if(points.length) {
        const name=nameOf(task.name,taskNames),localName=nameOf(task.name,plNames);
        if(name===task.name && /[a-f0-9]{24} name/i.test(name))throw Error('Missing quest translation: '+name);
        quests.push({Id:task.id,Name:name,LocalName:localName===name?'':localName,Points:points});
      }
    }
    extracts.sort((a,b)=>a.Name.localeCompare(b.Name));quests.sort((a,b)=>a.Name.localeCompare(b.Name));
    target.Modes.push({Id:mode,Extracts:extracts,Quests:quests});
    report.modes.push({mode,map:target.Id,extracts:extracts.length,quests:quests.length,points:quests.reduce((n,q)=>n+q.Points.length,0),factions:[...new Set(extracts.map(e=>e.Faction))]});
  }
}
fs.writeFileSync(output,JSON.stringify(result));
const manifest=process.argv[4]||path.join(path.dirname(output),'markers.source.json');
fs.writeFileSync(manifest,JSON.stringify({Version:1,RetrievedUtc:result.FetchedUtc,Documentation:result.Source,
  MarkerFileSha256:crypto.createHash('sha256').update(fs.readFileSync(output)).digest('hex'),
  Sources:report.sources.map(s=>{const mode=['regular','pve','pvp-season'].find(m=>s.file.startsWith(m+'-'));return {Url:'https://json.tarkov.dev/'+mode+'/'+s.file.slice(mode.length+1,-5),Sha256:s.sha256,Bytes:s.bytes};}),
  Coverage:report.modes,Discarded:report.discarded},null,2));
fs.writeFileSync(path.join(input,'marker-import-report.json'),JSON.stringify(report,null,2));
for(const m of report.modes) console.log(`${m.mode} ${m.map}: ${m.extracts} extracts, ${m.quests} quests, ${m.points} points; ${m.factions.join('/')}`);
console.log('Output bytes: '+fs.statSync(output).size+'; discarded '+JSON.stringify(report.discarded));
