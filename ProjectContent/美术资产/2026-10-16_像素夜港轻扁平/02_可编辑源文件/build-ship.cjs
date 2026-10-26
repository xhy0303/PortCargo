/** Original flat pixel cargo ship. Run with Node + sharp. No Unity project writes. */
const fs=require('node:fs');
const path=require('node:path');
let sharp;try{sharp=require('sharp')}catch(_){sharp=require(process.env.SHARP_MODULE||'sharp')}
const root=path.resolve(__dirname,'..');
const r=(x,y,w,h,c)=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="${c}"/>`;
const p=(d,c)=>`<path d="${d}" fill="${c}"/>`;
// Integer 128 x 64 grid; each logical pixel exports to a 4 x 4 block.
// Transparent top/right space deliberately allows separate cargo and demand UI.
const body=
  // Cabin silhouette and antenna (aft is left; bow is right).
  r(22,7,2,11,'#6485c2')+r(19,9,8,2,'#6485c2')+
  r(14,18,23,3,'#142344')+
  r(16,21,19,20,'#9fbce8')+
  r(13,23,25,4,'#142344')+
  r(16,23,4,4,'#ffcf58')+r(23,23,4,4,'#ffcf58')+r(30,23,5,4,'#ffcf58')+
  r(18,31,4,4,'#4672bc')+r(28,31,4,4,'#4672bc')+
  r(23,37,5,4,'#4672bc')+
  // Small exhaust, no smoke/glow.
  r(9,28,4,13,'#6485c2')+r(8,27,6,3,'#e54e59')+
  // Clear empty deck from x=40 through x=116. This is the cargo anchor line.
  r(8,39,109,3,'#6485c2')+
  // One flat hull plane with stepped bow facing right.
  p('M6 42H120V46H116V50H112V54H106V58H22V56H16V52H12V48H8V46H6Z','#142344')+
  // Thin red waterline entirely within hull; never a shadow or water reflection.
  p('M18 53H110V54H106V57H22V55H18Z','#e54e59')+
  // Four small portholes: no emission halo.
  r(29,46,3,3,'#ffcf58')+r(48,46,3,3,'#ffcf58')+r(67,46,3,3,'#ffcf58')+r(86,46,3,3,'#ffcf58');
const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="512" height="256" viewBox="0 0 128 64" shape-rendering="crispEdges"><title>Port Cargo empty ship, original flat pixel geometry</title>${body}</svg>\n`;
async function main(){
 const source='02_可编辑源文件/Harbor/spr_ship_empty.svg',file='01_可交付/Sprites/Harbor/spr_ship_empty.png';
 for(const f of [source,file])fs.mkdirSync(path.dirname(path.join(root,f)),{recursive:true});
 fs.writeFileSync(path.join(root,source),svg);
 await sharp(Buffer.from(svg)).png({compressionLevel:9}).toFile(path.join(root,file));
 const {data,info}=await sharp(path.join(root,file)).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const alphas=new Set();for(let i=3;i<data.length;i+=4)alphas.add(data[i]);
 if(info.width!==512||info.height!==256||[...alphas].some(a=>a!==0&&a!==255))throw Error('Ship dimensions or alpha failed');
 const entry={id:'spr_ship_empty',file,source,width:512,height:256,transparent:true,pivot:[0.5,0.5],border:[0,0,0,0],category:'Harbor',purpose:'通用空甲板船身与船舱；当前船和离港影像复用',origin:'original_svg',recommendedFilter:'Point',notes:'左驾驶舱、右船头。无货物、文字、水波、阴影或光晕；仅 0/255 alpha。甲板上沿在导出图 y=156；可用货物区域 x=160..464, y<156。堆放货箱另叠 Cargo，需求图标和装载量另放 UI。不可 9-slice；整数倍缩放。',anchors:{coordinateOrigin:'top-left',deckTopY:156,cargoSafeRect:{x:160,y:32,width:304,height:124},hullBaseY:232},verification:{dimensions:true,alphaValues:[...alphas].sort((a,b)=>a-b),logicalGrid:[128,64],exportScale:4}};
 fs.writeFileSync(path.join(root,'04_预览与验收/ship-manifest.json'),JSON.stringify({schemaVersion:1,count:1,borderOrder:['left','bottom','right','top'],units:'exported PNG pixels',assets:[entry]},null,2)+'\n');
 console.log('Ship PNG/SVG complete: 512x256, alpha 0/255, grid 4px.');
}
main().catch(e=>{console.error(e);process.exit(1)});
