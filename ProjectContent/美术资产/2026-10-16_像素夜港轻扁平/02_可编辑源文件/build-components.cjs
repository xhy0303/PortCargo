/**
 * Port Cargo / Flat Pixel Night Harbor — original vector components.
 * Run: node build-components.cjs
 * Requires sharp. Override SHARP_MODULE if sharp is not locally installed.
 * Output only this independent art delivery; never writes a Unity project.
 * SVG coordinates use integer pixel grids. All output scales are integers.
 */
const fs = require('node:fs');
const path = require('node:path');
let sharp;
try { sharp = require('sharp'); } catch (_) {
  sharp = require(process.env.SHARP_MODULE || 'sharp');
}
const root = path.resolve(__dirname, '..');
const manifest = [];
const C = { ink:'#142344', edge:'#6485c2', pale:'#eef5ff', yellow:'#ffcf58', blue:'#4672bc', red:'#e54e59', cargoBlue:'#367bce', green:'#48ac76', cargoYellow:'#e8bd42', dim:'#374968', deep:'#0c1833' };
const rect = (x,y,w,h,c,extra='') => `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="${c}" ${extra}/>`;
const p = (d,c) => `<path d="${d}" fill="${c}" fill-rule="evenodd"/>`;
const step = (x,y,w,h,c,s=1) => p(`M${x+s} ${y}H${x+w-s}V${y+s}H${x+w}V${y+h-s}H${x+w-s}V${y+h}H${x+s}V${y+h-s}H${x}V${y+s}H${x+s}Z`,c);
const group = (x,y,k,body) => `<g transform="translate(${x} ${y}) scale(${k})">${body}</g>`;
const svg = (w,h,bw,bh,body) => `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${bw} ${bh}" shape-rendering="crispEdges"><title>Original flat pixel component</title>${body}</svg>\n`;
async function emit(category,id,w,h,bw,bh,body,purpose,border=[0,0,0,0],notes='') {
  const source=`02_可编辑源文件/${category}/${id}.svg`;
  const file=`01_可交付/Sprites/${category}/${id}.png`;
  for(const f of [source,file])fs.mkdirSync(path.dirname(path.join(root,f)),{recursive:true});
  const src=svg(w,h,bw,bh,body);
  fs.writeFileSync(path.join(root,source),src);
  await sharp(Buffer.from(src)).png({palette:false,compressionLevel:9}).toFile(path.join(root,file));
  const stats=await sharp(path.join(root,file)).stats();
  const md=await sharp(path.join(root,file)).metadata();
  if(md.width!==w||md.height!==h||!md.hasAlpha)throw Error(`Invalid dimensions/alpha: ${id}`);
  const alpha=stats.channels[3];
  manifest.push({id,file,source,width:w,height:h,transparent:alpha.min<255,pivot:[0.5,0.5],border,category,purpose,origin:'original_svg',recommendedFilter:'Point',notes});
}
function cargo(kind){
  const color={R:C.red,B:C.cargoBlue,G:C.green,Y:C.cargoYellow}[kind];
  let b=step(2,2,28,28,C.deep)+step(3,3,26,26,color)+rect(5,5,22,22,C.ink)+rect(6,6,20,20,color);
  if(kind==='R')for(const x of [8,13,18,23])b+=rect(x,7,2,18,C.pale);
  if(kind==='B')for(const y of [9,16,23])for(const x of [9,16,23])b+=rect(x-1,y-2,3,5,C.pale)+rect(x-2,y-1,5,3,C.pale);
  if(kind==='G'){
    b+='<defs><clipPath id="face"><rect x="7" y="7" width="18" height="18"/></clipPath></defs><g clip-path="url(#face)">';
    for(let i=-12;i<=32;i+=8)b+=p(`M${i} 25L${i+18} 7H${i+22}L${i+4} 25Z`,C.pale);
    b+='</g>';
  }
  if(kind==='Y')for(const y of [11,21])for(const x of [11,21])b+=rect(x-1,y-3,2,6,C.pale)+rect(x-3,y-1,6,2,C.pale);
  return b;
}
const cargoBody=Object.fromEntries(['R','B','G','Y'].map(k=>[k,cargo(k)]));
function outline(x,y,w,h,color,th=1){return step(x,y,w,h,color)+step(x+th,y+th,w-2*th,h-2*th,'#000000',1).replace('fill="#000000"','fill="none"');}
function cornerFrame(c){let b='';for(const [x,y,dx,dy]of [[0,0,1,1],[32,0,-1,1],[0,32,1,-1],[32,32,-1,-1]]){b+=rect(dx===1?x:x-8,dy===1?y:y-2,8,2,c);b+=rect(dx===1?x:x-2,dy===1?y:y-8,2,8,c);}return b;}
function emptySlot(){let b='';for(const z of [6,12,18,24])b+=rect(z,2,4,1,C.edge)+rect(z,29,4,1,C.edge)+rect(2,z,1,4,C.edge)+rect(29,z,1,4,C.edge);b+=p('M3 3H6V4H4V6H3ZM26 3H29V6H28V4H26ZM3 26H4V28H6V29H3ZM28 26H29V29H26V28H28Z',C.edge);return b;}
const check=p('M6 16H10V20H14V16H18V12H22V8H26V14H22V18H18V22H14V26H10V22H6Z',C.pale);
const cross=p('M6 6H10V10H14V14H18V10H22V6H26V10H22V14H18V18H22V22H26V26H22V22H18V18H14V22H10V26H6V22H10V18H14V14H10V10H6Z',C.pale);
const arrowRight=p('M5 13H19V7H23V11H27V15H31V17H27V21H23V25H19V19H5Z',C.pale);
const arrowDown=p('M13 3H19V17H25V21H21V25H17V29H15V25H11V21H7V17H13Z',C.pale);
const lock=p('M11 5H21V7H23V15H26V28H6V15H9V7H11ZM12 9V15H20V9ZM14 19V24H18V19Z',C.pale);
const speaker=p('M4 12H10V9H14V6H17V26H14V23H10V20H4ZM21 11H24V14H26V18H24V21H21V18H23V14H21ZM26 6H28V10H30V22H28V26H26V22H28V10H26Z',C.pale);
const icons={
  pause:rect(8,6,5,20,C.pale)+rect(19,6,5,20,C.pale),
  undo:p('M4 13H7V10H10V7H13V12H21V14H25V17H27V23H24V26H15V22H22V18H20V16H13V21H10V18H7V15H4Z',C.pale),
  hint:p('M12 3H20V5H24V8H26V16H23V19H21V23H11V19H9V16H6V8H8V5H12ZM10 9V14H13V18H15V11H17V18H19V14H22V9H20V7H12V9ZM11 25H21V28H18V30H14V28H11Z',C.pale),
  queue:p('M2 22H30V25H27V28H5V25H2ZM4 12H9V20H4ZM11 12H16V20H11ZM18 12H23V20H18ZM25 14H28V20H25ZM7 4H23V7H7ZM11 8H27V10H11Z',C.pale),
  home:p('M2 15H6V11H10V7H14V3H18V7H22V11H26V15H30V18H26V28H19V21H13V28H6V18H2Z',C.pale),
  settings:p('M13 3H19V7H23V9H27V13H29V19H25V23H23V27H19V29H13V25H9V23H5V19H3V13H7V9H9V5H13ZM13 11V13H11V19H13V21H19V19H21V13H19V11Z',C.pale),
  sound:speaker,
  music:p('M10 5H27V24H25V27H20V25H18V21H20V19H23V10H14V26H12V29H6V27H4V23H6V21H10Z',C.pale),
  reduced_motion:p('M4 6H16V9H4ZM2 13H12V16H2ZM4 20H14V23H4ZM18 10H22V26H18ZM25 10H29V26H25Z',C.pale),
  help:p('M11 3H21V5H25V9H27V14H24V17H20V20H18V23H14V18H17V15H21V12H23V9H20V7H12V9H9V12H5V8H7V5H11ZM14 26H18V30H14Z',C.pale),
  lock,
  complete:check,
  previous:p('M22 5H18V9H14V13H10V15H6V17H10V19H14V23H18V27H22V23H18V19H14V13H18V9H22Z',C.pale),
  next:p('M10 5H14V9H18V13H22V15H26V17H22V19H18V23H14V27H10V23H14V19H18V13H14V9H10Z',C.pale),
  retry_save:p('M11 4H22V7H26V11H28V17H24V12H22V9H13V11H16V14H5V3H8V8H11Z',C.pale)+p('M8 19H24V29H8ZM11 22V26H21V22Z',C.pale),
  exit:p('M4 4H18V7H8V25H18V28H4Z',C.pale)+p('M13 13H22V8H25V11H28V14H30V18H28V21H25V24H22V19H13Z',C.pale),
  back:p('M13 5H17V11H28V21H24V16H17V27H13V23H9V19H5V13H9V9H13Z',C.pale),
  close:cross,
  toggle_on:check,
  toggle_off:cross,
  mode_campaign:p('M5 3H9V29H5ZM11 4H25V7H28V10H25V13H11ZM12 18H17V21H22V24H27V28H22V26H17V23H12Z',C.pale),
  mode_endless:p('M5 9H11V11H14V14H18V11H21V9H27V12H30V20H27V23H21V21H18V18H14V21H11V23H5V20H2V12H5ZM6 13V19H10V17H12V15H10V13ZM22 13V15H20V17H22V19H26V13Z',C.pale),
  warning:p('M14 2H18V6H20V10H22V14H24V18H26V22H28V26H30V29H2V26H4V22H6V18H8V14H10V10H12V6H14ZM14 11V21H18V11ZM14 24V27H18V24Z',C.pale),
  play:p('M9 4H13V7H17V10H21V13H25V16H28V18H24V21H20V24H16V27H12V29H9Z',C.pale),
  restart:p('M12 4H22V7H26V11H29V21H26V25H22V28H12V25H7V21H10V23H14V25H20V23H24V19H26V13H24V10H21V8H13V10H16V14H5V3H9V7H12Z',C.pale),
  level_select:rect(4,4,9,9,C.pale)+rect(19,4,9,9,C.pale)+rect(4,19,9,9,C.pale)+rect(19,19,9,9,C.pale),
  privacy:p('M14 2H18V4H23V6H28V18H26V22H23V25H20V28H17V30H15V28H12V25H9V22H6V18H4V6H9V4H14ZM14 11V13H12V21H20V13H18V11Z',C.pale),
  feedback:p('M4 5H28V22H16V25H12V28H8V22H4ZM8 9V18H24V9ZM11 12H13V15H11ZM15 12H17V15H15ZM19 12H21V15H19Z',C.pale),
  save:p('M5 3H24V6H27V29H5ZM10 6V13H22V6ZM9 18V26H23V18ZM17 6H20V11H17Z',C.pale),
  info:p('M13 3H19V9H13ZM10 13H19V25H23V29H9V25H13V17H10Z',C.pale),
};
const iconPurpose={pause:'暂停',undo:'撤销',hint:'提示',queue:'只读船队',home:'首页',settings:'设置',sound:'音效',music:'音乐',reduced_motion:'减少动效',help:'帮助',lock:'锁定',complete:'完成',previous:'上一页',next:'下一页',retry_save:'保存重试',exit:'退出',back:'返回',close:'关闭',toggle_on:'开启语义',toggle_off:'关闭语义',mode_campaign:'闯关模式',mode_endless:'无尽模式',warning:'警告或受阻',play:'开始或继续',restart:'重开',level_select:'选关',privacy:'隐私与本机保存',feedback:'反馈',save:'保存状态',info:'信息与版本'};
function button(kind,state){
 const fill=state==='disabled'?'#66758f':kind==='primary'?(state==='pressed'?'#dfaa39':C.yellow):(state==='pressed'?'#355997':C.blue);
 const line=state==='disabled'?'#8291a8':kind==='primary'?'#ffe6a0':C.edge;
 return step(1,1,94,30,C.deep,3)+step(2,2,92,28,line,2)+step(3,3,90,26,fill,1);
}
function panel(w,h,warning=false){return step(0,0,w,h,C.deep,3)+step(1,1,w-2,h-2,warning?C.red:C.edge,2)+step(2,2,w-4,h-4,C.ink,1);}
function tile(state){
 const edge={normal:C.edge,current:C.yellow,completed:C.green,locked:'#52617c'}[state];
 let b=step(1,1,30,30,C.deep,2)+step(2,2,28,28,edge,1)+step(3,3,26,26,state==='locked'?'#25334c':C.ink,1);
 if(state==='current')b+=rect(11,29,10,2,C.yellow);
 if(state==='completed')b+=p('M23 5H25V7H27V5H29V3H31V6H29V8H27V10H25V8H23Z',C.pale);
 if(state==='locked')b+=p('M25 2H29V3H30V7H31V12H23V7H24V3H25ZM26 4V7H28V4ZM26 9V11H28V9Z',C.pale);
 return b;
}
function flatShip(){return p('M2 29H76V36H72V40H9V36H5V33H2Z',C.ink)+rect(6,27,67,2,C.edge)+rect(48,11,18,16,C.pale)+rect(53,7,5,4,C.blue)+rect(51,14,3,3,C.ink)+rect(57,14,3,3,C.ink)+rect(62,14,2,3,C.ink)+rect(61,4,2,7,C.edge)+rect(11,21,30,6,C.blue);}
async function main(){
 for(const k of ['R','B','G','Y']) {
   const pat={R:'竖条',B:'圆点',G:'斜线',Y:'十字'}[k];
   await emit('Cargo',`cargo_${k}`,128,128,32,32,cargoBody[k],`${k} 类货箱：${pat}`,[0,0,0,0],'4 类使用完全相同的 28×28 基格轮廓；外侧各留 8 导出像素。纹样不依赖颜色。');
   await emit('Cargo',`cargo_icon_${k}`,64,64,32,32,cargoBody[k],`${k} 类小图标：当前需求、船队预告、飞行与首页示意` ,[0,0,0,0],'与对应 cargo_* 共用同一 SVG 图形函数，只有导出倍率不同。');
 }
 await emit('Cargo','cargo_highlight',128,128,32,32,cornerFrame(C.pale),'顶部可交互箱高亮角框',[0,0,0,0],'中心透明，叠在真实货箱上；不覆盖纹样。');
 await emit('Cargo','cargo_hint',128,128,32,32,cornerFrame(C.yellow)+rect(14,0,4,1,C.yellow),'提示角框',[0,0,0,0],'中心透明，减少动效模式直接静态显示。');
 await emit('Cargo','stack_base',128,32,32,8,step(1,2,30,5,C.deep)+rect(2,2,28,3,C.edge)+rect(5,5,3,2,C.blue)+rect(24,5,3,2,C.blue),'箱堆底座，最多六堆复用',[0,0,0,0],'置于最底箱下方，不遮纹样。整图等比缩放，不作 9-slice。');
 await emit('Cargo','buffer_slot',128,128,32,32,emptySlot(),'暂存空槽，一样式复用三次',[0,0,0,0],'透明空心虚线框；已有货箱直接叠入，未来第四格继续复用。');
 for(const kind of ['primary','secondary'])for(const state of ['normal','pressed','disabled'])await emit('UI',`button_${kind}_${state}`,192,64,96,32,button(kind,state),`${kind==='primary'?'主':'次'}按钮 / ${state}`,[12,12,12,12],'纯底板，无文案；文字、图标与点击热区由 UI 层独立控制。按下需同时有位置或缩放反馈。');
 await emit('UI','panel_common',256,192,64,48,panel(64,48),'通用对话框、设置、结果、选关和说明面板',[16,16,16,16],'中心平色可 9-slice；装饰不承接点击。');
 await emit('UI','panel_warning',256,192,64,48,panel(64,48,true),'受阻、退出与保存异常面板',[16,16,16,16],'需要叠加 warning 图标和明确文案，不只靠红色。');
 await emit('UI','toast',256,64,128,32,panel(128,32),'短提示与保存失败通知底板',[8,8,8,8],'可点击重试时保留独立重试控件与足够热区。');
 await emit('UI','scrim',8,8,8,8,rect(0,0,8,8,C.deep,'fill-opacity="0.72"'),'模态遮罩',[0,0,0,0],'唯一半透明填充资产，Alpha 约 184/255；可按需修改 UI 层 alpha，但不要叠乘造成过暗。遮罩阻挡点击由程序设置。');
 for(const state of ['normal','current','completed','locked'])await emit('UI',`level_tile_${state}`,96,96,32,32,tile(state),`选关格 / ${state}`,[0,0,0,0],'不含数字；统一动态叠加 1–50。状态角标已内置，避免重复叠加。整图整数倍缩放，不作 9-slice。');
 await emit('UI','toggle_track_on',96,48,48,24,step(1,2,46,20,C.deep,2)+step(2,3,44,18,C.green,1),'设置开关开启底轨',[8,8,8,8],'无文字；滑块移右，并配独立“开”文字或 toggle_on 图标。');
 await emit('UI','toggle_track_off',96,48,48,24,step(1,2,46,20,C.deep,2)+step(2,3,44,18,'#52617c',1),'设置开关关闭底轨',[8,8,8,8],'无文字；滑块移左，并配独立“关”文字或 toggle_off 图标。');
 await emit('UI','toggle_knob',32,32,16,16,step(0,0,16,16,C.deep,2)+step(1,1,14,14,C.pale,1),'设置开关共用滑块',[0,0,0,0],'同一滑块左右移动。无需分别制作带字开关。');
 for(const [id,body]of Object.entries(icons))await emit('Icons',`icon_${id}`,64,64,32,32,body,iconPurpose[id],[0,0,0,0],'透明底、单色 #eef5ff，可 tint；不含文字。图片大小不等于热区大小。');
 await emit('FX','confetti_square',16,16,16,16,rect(5,5,6,6,C.pale),'庆祝方形碎片',[0,0,0,0],'可 tint；运行时位移旋转，不需要序列帧。');
 await emit('FX','confetti_star',16,16,16,16,p('M7 2H9V5H11V7H14V9H11V11H9V14H7V11H5V9H2V7H5V5H7Z',C.pale),'庆祝四角星碎片',[0,0,0,0],'可 tint；减少动效时不播放。');
 await emit('FX','confetti_diamond',16,16,16,16,p('M7 2H9V4H11V6H13V10H11V12H9V14H7V12H5V10H3V6H5V4H7Z',C.pale),'庆祝菱形碎片',[0,0,0,0],'可 tint；减少动效时不播放。');
 await emit('FX','completion_badge',128,128,32,32,step(2,2,28,28,C.deep,5)+step(3,3,26,26,C.yellow,4)+step(5,5,22,22,C.ink,3)+check,'通关完成徽记',[0,0,0,0],'无文字，通关标题动态叠加；减少动效时仍静态呈现。');
 const top=group(77,28,1,cargoBody.B)+group(77,60,1,cargoBody.G)+group(77,92,1,cargoBody.R)+group(77,28,1,cornerFrame(C.yellow))+group(77,-2,1,arrowDown.replaceAll(C.pale,C.yellow));
 await emit('Tutorial','tutorial_only_top',384,256,192,128,top,'教学 1：只取堆顶',[0,0,0,0],'透明底。黄色箭头只标出堆顶，不表示拖动手势。说明文案由独立 TMP 绘制。');
 const match=group(14,48,1.5,cargoBody.R)+group(68,53,1,arrowRight.replaceAll(C.pale,C.yellow))+group(109,52,1,flatShip())+group(140,31,0.5,cargoBody.R);
 await emit('Tutorial','tutorial_match_to_ship',384,256,192,128,match,'教学 2：同类自动装船',[0,0,0,0],'R 箱与船需求保持同类；箭头表示点击后的自动流向，不表示拖动。船为专用极简示意。');
 const other=group(8,48,1,cargoBody.B)+group(47,48,1,arrowRight.replaceAll(C.pale,C.yellow))+group(91,48,1,emptySlot())+group(125,48,1,emptySlot())+group(159,48,1,emptySlot())+group(91,48,1,cargoBody.B);
 await emit('Tutorial','tutorial_other_to_buffer',384,256,192,128,other,'教学 3：其余货箱进入三格暂存',[0,0,0,0],'第一格放 B 箱，后两格空；箭头只表示自动流向。三格必须保持清楚可数。');
 const dest=path.join(root,'04_预览与验收/components-manifest.json');fs.mkdirSync(path.dirname(dest),{recursive:true});
 fs.writeFileSync(dest,JSON.stringify({schemaVersion:1,title:'Flat Pixel Night Harbor — precise components',count:manifest.length,borderOrder:['left','bottom','right','top'],units:'exported PNG pixels',pixelGrid:'32 px for cargo and icons; integer coordinates and scale per source',assets:manifest},null,2)+'\n');
 console.log(`Built and alpha/dimension verified ${manifest.length} PNG/SVG pairs.`);
}
main().catch(e=>{console.error(e);process.exit(1)});
