const fs=require('fs'),vm=require('vm'),assert=require('assert');
const source=fs.readFileSync('Assets/PortCargo/Plugins/WebGL/PortCargoStorage.jslib','utf8');
const memory=new Map();let pointer=100, fault=null, written;
const wx={getStorageSync(k){if(fault==='read')throw Error('access denied');return fault==='mismatch'?'wrong':memory.get(k)||'';},setStorageSync(k,v){if(fault==='quota')throw Error('quota exceeded');memory.set(k,v);}};
const ctx={LibraryManager:{library:{}},mergeInto:Object.assign,wx,UTF8ToString:x=>x,_malloc:()=>++pointer,lengthBytesUTF8:s=>Buffer.byteLength(s),stringToUTF8:s=>written=s};
vm.runInNewContext(source,ctx);
const api=ctx.LibraryManager.library;
const read=k=>{api.PortCargoStorageRead(k);return JSON.parse(written);};
const write=(k,v)=>{api.PortCargoStorageWrite(k,v);return written;};
const checks=[];
function check(name,fn){fn();checks.push(name+' PASS');}
check('missing key is absent',()=>assert.deepEqual(read('a'),{ok:true,present:false,value:''}));
check('Unicode snapshot round trip',()=>{assert.equal(write('a','港口🌊完整存档'), '');assert.equal(read('a').value,'港口🌊完整存档');});
check('independent slots',()=>{write('b','older');assert.equal(read('a').value,'港口🌊完整存档');assert.equal(read('b').value,'older');});
check('quota failure surfaced and old slot retained',()=>{fault='quota';assert.match(write('a','new'),/quota/);fault=null;assert.equal(read('a').value,'港口🌊完整存档');});
check('readback mismatch surfaced',()=>{fault='mismatch';assert.match(write('b','new'),/mismatch/);fault=null;});
check('read errors are not empty saves',()=>{fault='read';assert.equal(read('a').ok,false);assert.match(read('a').error,/access denied/);fault=null;});
check('non-string corruption surfaced',()=>{memory.set('bad',{});assert.equal(read('bad').ok,false);});
console.log(checks.join('\n'));console.log('Native bridge contract only; wx host is simulated. Real restart must be tested in WeChat.');
