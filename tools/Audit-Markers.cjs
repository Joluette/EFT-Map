const fs=require('fs'),path=require('path');
const dir=path.resolve(process.argv[2]),read=n=>JSON.parse(fs.readFileSync(path.join(dir,n),'utf8').replace(/^\uFEFF/,''));
const output=JSON.parse(fs.readFileSync(process.argv[3],'utf8'));
const alias={'night-factory':'factory','ground-zero-21':'ground-zero','the-lab-dark':'the-lab'};
let checks=0,extracts=0,points=0,quests=0;
function assert(test,message){checks++;if(!test)throw Error(message);}
function same(source,p){return source && Math.abs(source.x-p.X)<1e-7 && Math.abs(source.z-p.Z)<1e-7 && (p.Y==null || Math.abs(source.y-p.Y)<1e-7);}
for(const mode of ['regular','pve','pvp-season']) {
  const maps=read(mode+'-maps.json').data.maps,tasks=read(mode+'-tasks.json').data.tasks;
  const mapNames=read(mode+'-maps_en.json').data,names=read(mode+'-tasks_en.json').data;
  const mapId=new Map(Object.values(maps).map(m=>[m.id,alias[m.normalizedName]||m.normalizedName]));
  const canon=id=>mapId.get(typeof id==='string'?id:id?.id);
  for(const m of output.Maps){const data=m.Modes.find(x=>x.Id===mode),sourceMaps=Object.values(maps).filter(x=>canon(x.id)===m.Id);
    for(const extract of data.Extracts){assert(sourceMaps.some(sm=>(sm.extracts||[]).some(e=>(mapNames[e.name]||e.name)===extract.Name && same(e.position,extract))),'extract source mismatch '+m.Id+' '+extract.Name);extracts++;}
    for(const q of data.Quests){
      const source=tasks[q.Id];assert(source && (names[source.name]||source.name)===q.Name,'quest name mismatch '+q.Id);quests++;
      const candidates=[];
      for(const objective of source.objectives||[]){
        for(const z of objective.zones||[])if(canon(z.map)===m.Id)candidates.push(z.position);
        for(const location of objective.possibleLocations||[])if(canon(location.map)===m.Id)candidates.push(...(location.positions||[]));
        if(objective.exitName && (objective.maps||[]).some(id=>canon(id)===m.Id))for(const sm of sourceMaps)for(const e of sm.extracts||[])
          if(e.name===objective.exitName || (mapNames[e.name]||e.name).toLowerCase()===(mapNames[objective.exitName]||objective.exitName).toLowerCase())candidates.push(e.position);
      }
      for(const p of q.Points){assert(candidates.some(s=>same(s,p)),'quest/map coordinate source mismatch '+m.Id+' '+q.Name);points++;}
    }
  }
}
console.log(JSON.stringify({pass:true,checks,extracts,quests,points,statement:'Every output coordinate and quest/map association matched a concrete record in the downloaded primary API snapshot.'},null,2));
