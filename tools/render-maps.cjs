// Optional build-time tool. Requires sharp; the desktop application does not.
const fs=require('fs'),path=require('path'),sharp=require('sharp');
const root=path.resolve(__dirname,'..');
if(!process.argv[2]) throw new Error('Pass the jobs.json path printed by prepare-maps.py');
const jobs=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));
(async()=>{
  for(const job of jobs) await sharp(job.input).flatten({background:'#18272b'}).png().toFile(job.output);
  console.log(`Rendered ${jobs.length} layers, one at a time.`);
})().catch(e=>{console.error(e);process.exitCode=1;});
