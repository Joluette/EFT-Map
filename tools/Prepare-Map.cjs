// Optional developer tool. Runtime and building C# do not need Node or sharp.
// npm install sharp (only if you want to regenerate the already included PNG).
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '..');
let svg = fs.readFileSync(path.join(root, 'assets/Customs.original.svg'), 'utf8');
svg = svg.replace(/<g id="(Second_Floor|Third_Floor|Underground_Level)"/g,
  '<g style="display:none" id="$1"');
// First_Floor has data-keep-with-group="Ground_Level" and stays visible.
// Default preserveAspectRatio (xMidYMid meet) matches tarkov.dev SVGOverlay.
svg = svg.replace('<svg ', '<svg width="3210" height="1632" ');
sharp(Buffer.from(svg)).flatten({background:'#18272b'}).png()
  .toFile(path.join(root, 'assets/customs-ground.png'))
  .then(info => console.log(info)).catch(error => { console.error(error); process.exitCode=1; });
