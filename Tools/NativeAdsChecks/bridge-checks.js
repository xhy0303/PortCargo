// Executes the shipped .jslib unchanged against a controlled wx boundary.
// Native show Promise settlement is independently delayed to expose navigation/background races.
const fs=require('fs');
const vm=require('vm');
const path=require('path');
const source=fs.readFileSync(path.join(__dirname,'../../Assets/PortCargo/Scripts/Platform/WeChatNativeAds.jslib'),'utf8');
let checks=0;
function check(value,label){checks++;if(!value)throw new Error('CHECK FAILED: '+label);}
async function suite(label,run){const before=checks;await run();console.log('PASS '+label+' checks='+(checks-before));}
function deferred(){let resolve,reject;const promise=new Promise((a,b)=>{resolve=a;reject=b;});return {promise,resolve,reject};}
async function flush(){await Promise.resolve();await Promise.resolve();}
function fixture(options={}){
  const ads=[],events={};
  const wx={getWindowInfo:()=>({windowWidth:360,windowHeight:800}),
    onHide:fn=>events.Hide=fn,onShow:fn=>events.Show=fn,
    createCustomAd:params=>{
      if(options.throwCreate)throw new Error('create failed');
      if(options.nullCreate)return null;
      const listeners={},ad={params,style:Object.assign({},params.style,options.nativeStyle||{}),listeners,shows:0,hides:0,destroys:0,visible:false,pending:[],offs:0,
        emit(event,value){for(const fn of [...(listeners[event]||[])])fn(value);},
        show(){this.shows++;if(options.throwShow)throw new Error('show failed');const task=deferred();this.pending.push(task);return task.promise;},
        resolveShow(index=0){this.visible=true;this.pending[index].resolve();},
        rejectShow(index=0){this.pending[index].reject(new Error('show rejected'));},
        hide(){this.hides++;this.visible=false;this.emit('Hide');if(options.throwHide)throw new Error('hide failed');return Promise.resolve();},
        destroy(){this.destroys++;this.visible=false;if(options.throwDestroy)throw new Error('destroy failed');}
      };
      for(const event of ['Load','Error','Close','Hide','Resize']){
        if(event==='Resize'&&options.noResize)continue;
        ad['on'+event]=fn=>(listeners[event]||(listeners[event]=[])).push(fn);
        ad['off'+event]=fn=>{ad.offs++;listeners[event]=(listeners[event]||[]).filter(x=>x!==fn);};
      }
      ads.push(ad);return ad;
    }};
  const context={LibraryManager:{library:{}},mergeInto:(dest,value)=>Object.assign(dest,value),UTF8ToString:value=>value,
    wx:options.unsupported?{}:wx,Promise,console};
  vm.createContext(context);vm.runInContext(source,context);
  context.PortCargoNativeAdBridge=context.LibraryManager.library.$PortCargoNativeAdBridge;
  const api=context.LibraryManager.library;
  const create=(id='adunit-home',bounds=[0,300,360,126],sizing=1,fixed=[0,0])=>api.PortCargoNativeAdCreate(id,...bounds,sizing,...fixed);
  const flags=id=>api.PortCargoNativeAdRead(id,0);
  const ready=id=>{const ad=ads[ads.length-1];ad.emit('Load');if(!options.noResize)ad.emit('Resize',{width:360,height:126});return ad;};
  return {api,ads,events,context,create,flags,ready};
}
(async()=>{
  await suite('supported creation and isolated handles',async()=>{
    let f=fixture({unsupported:true});check(f.api.PortCargoNativeAdSupported()===0&&f.create()===0,'missing native API never creates');
    f=fixture();check(f.api.PortCargoNativeAdSupported()===1,'custom API availability detected');
    const first=f.create(),ad=f.ads[0];check(first>0&&ad.shows===0,'create never automatically shows');
    check(ad.params.adUnitId==='adunit-home'&&ad.params.style.left===0&&ad.params.style.top===300&&ad.params.style.width===360,'configured ID and top-left slot coordinates forwarded');
    check(!('adIntervals' in ad.params),'no periodic native ad refresh requested');
    const second=f.create('adunit-completed');check(second>first&&f.ads[1]!==ad,'placement instances have independent monotonic handles');
    f.api.PortCargoNativeAdDestroy(first);check(f.flags(first)===4&&f.flags(second)===0,'retired handle cannot alias current placement');
    check(ad.destroys===1&&ad.offs===5,'destroy unsubscribes all supported callbacks');
    f.api.PortCargoNativeAdDestroy(first);check(ad.destroys===1,'double destroy is harmless');
  });
  await suite('load proof and bounded native show',async()=>{
    const f=fixture(),id=f.create(),ad=f.ads[0];f.api.PortCargoNativeAdShow(id);check(ad.shows===0,'unloaded card cannot show');
    f.ready(id);check((f.flags(id)&17)===17&&f.api.PortCargoNativeAdRead(id,1)===360&&f.api.PortCargoNativeAdRead(id,2)===126,'loaded and measured native state reported');
    f.api.PortCargoNativeAdShow(id);f.api.PortCargoNativeAdShow(id);check(ad.shows===1,'duplicate show during pending promise ignored');
    check((f.flags(id)&8)===0,'Show invocation alone is not visible proof');
    ad.resolveShow();await flush();check((f.flags(id)&8)!==0&&ad.visible,'resolved native show marks visible');
    f.api.PortCargoNativeAdShow(id);check(ad.shows===1,'visible card not shown twice');
    f.api.PortCargoNativeAdHide(id);check((f.flags(id)&8)===0&&(f.flags(id)&2)===0,'programmatic hide does not masquerade as user close');
    f.api.PortCargoNativeAdDestroy(id);check(ad.destroys===1,'visible lease cleanup destroys once');
  });
  await suite('retired show-promise races',async()=>{
    for(const result of ['resolve','reject']){
      const f=fixture(),old=f.create(),ad=f.ready(old);f.api.PortCargoNativeAdShow(old);
      const staleLoad=ad.listeners.Load[0],staleClose=ad.listeners.Close[0],staleResize=ad.listeners.Resize[0];
      f.api.PortCargoNativeAdDestroy(old);const hides=ad.hides;
      const current=f.create('adunit-completed'),other=f.ready(current);f.api.PortCargoNativeAdShow(current);other.resolveShow();await flush();
      if(result==='resolve')ad.resolveShow();else ad.rejectShow();await flush();
      check(!ad.visible&&ad.hides>hides&&ad.destroys===1,result+' late Show settlement re-hides original object without double destroy');
      staleLoad();staleClose();staleResize({width:999,height:999});
      check(other.visible&&other.destroys===0&&(f.flags(current)&8)!==0,result+' obsolete callbacks cannot affect replacement placement');
      check(f.flags(old)===4,result+' old handle remains invalid');
    }
  });
  await suite('user close and hide remain dismissed',async()=>{
    for(const event of ['Close','Hide']){
      const f=fixture(),id=f.create(),ad=f.ready(id);f.api.PortCargoNativeAdShow(id);ad.emit(event);
      check((f.flags(id)&2)!==0&&ad.destroys===1,event+' user dismissal recorded immediately');
      ad.resolveShow();await flush();check(!ad.visible&&(f.flags(id)&8)===0,event+' pending Show cannot undo dismissal');
      const count=ad.shows;f.api.PortCargoNativeAdShow(id);check(ad.shows===count,event+' dismissed record cannot show again');
    }
  });
  await suite('native background gate',async()=>{
    const f=fixture(),id=f.create(),ad=f.ready(id);f.api.PortCargoNativeAdShow(id);f.events.Hide();
    check(ad.hides>=1&&!ad.visible,'wx background callback hides immediately without Unity frames');
    const count=f.ads.length;check(f.create('adunit-completed')===0&&f.ads.length===count,'background prevents new native creation');
    ad.resolveShow();await flush();check(!ad.visible&&ad.destroys===1,'show resolving while background cannot reappear');
    f.events.Show();check(ad.shows===1&&!ad.visible,'foreground does not autonomously reshow');
    const next=f.create('adunit-completed');check(next>id,'fresh authorization after foreground may create a new card');
  });
  await suite('real size events and missing-size fallback',async()=>{
    for(const size of [{width:361,height:126},{width:360,height:128},{width:0,height:126},{width:NaN,height:126},{width:360,height:Infinity}]){
      const f=fixture(),id=f.create(),ad=f.ads[0];ad.emit('Load');ad.emit('Resize',size);
      check((f.flags(id)&4)!==0&&ad.destroys===1,'invalid measured size immediately retires card '+JSON.stringify(size));
      f.api.PortCargoNativeAdShow(id);check(ad.shows===0,'invalid-sized native ad never shows');
    }
    let f=fixture(),id=f.create(),ad=f.ready(id);f.api.PortCargoNativeAdShow(id);ad.resolveShow();await flush();ad.emit('Resize',{width:360,height:200});
    check(!ad.visible&&ad.destroys===1&&(f.flags(id)&4)!==0,'visible template growth is hidden at native event boundary');
    f=fixture({noResize:true});id=f.create();ad=f.ready(id);f.api.PortCargoNativeAdShow(id);
    check(ad.shows===1,'fixed20:7 fallback supports templates without onResize');
    f=fixture({noResize:true});id=f.create('adunit-home',[0,300,360,100]);ad=f.ready(id);f.api.PortCargoNativeAdShow(id);
    check(ad.shows===0&&ad.destroys===1&&(f.flags(id)&4)!==0,'fallback refuses insufficient reserved height');
  });
  await suite('offline errors exceptions and failed cleanup',async()=>{
    for(const mode of ['error','show-reject','show-throw']){
      const f=fixture({throwShow:mode==='show-throw'}),id=f.create(),ad=f.ready(id);
      if(mode==='error')ad.emit('Error');else{f.api.PortCargoNativeAdShow(id);if(mode==='show-reject'){ad.rejectShow();await flush();}}
      check((f.flags(id)&4)!==0&&ad.destroys===1,mode+' marks failed and destroys native card');
      const shown=ad.shows;for(let i=0;i<50;i++){f.api.PortCargoNativeAdRead(id,0);f.api.PortCargoNativeAdShow(id);}
      check(f.ads.length===1&&ad.shows===shown,mode+' polling/Show cannot create refresh or retry storm');
    }
    for(const fault of ['throwCreate','nullCreate']){const f=fixture({[fault]:true});check(f.create()===0&&Object.keys(f.context.PortCargoNativeAdBridge.records).length===0,fault+' leaves no native record');}
    const f=fixture({throwHide:true,throwDestroy:true}),id=f.create(),ad=f.ready(id);f.api.PortCargoNativeAdDestroy(id);
    check(ad.hides>=1&&ad.destroys===1&&f.flags(id)===4,'hide/destroy exceptions cannot retain active record');
  });
  await suite('invalid and overflowing native reservations',async()=>{
    const f=fixture();for(const bounds of [[-1,0,360,126],[0,0,0,126],[0,NaN,360,126],[1,0,360,126],[0,700,360,126]])
      check(f.create('adunit-home',bounds)===0,'bad reservation rejected before wx '+bounds);
    check(f.ads.length===0,'invalid reservations generate no wx requests');
  });
  await suite('strict compact and grid size policy',async()=>{
    let f=fixture({noResize:true}),id=f.create('adunit-grid',[250,50,90,90],0),ad=f.ads[0];ad.emit('Load');
    for(let i=0;i<50;i++){f.flags(id);f.api.PortCargoNativeAdShow(id);}
    check(ad.shows===0&&f.ads.length===1,'unknown grid never uses20:7 nor starts a request storm');
    check(!(f.flags(id)&16),'requested style.width is not actual dimension evidence');
    check(!('height' in ad.params.style),'reserved grid height is not sent as an unsupported native style control');
    for(const nativeStyle of [{realWidth:90,realHeight:90},{realWidth:0,realHeight:90},{realWidth:90,realHeight:Infinity},{realWidth:'90',realHeight:'90'},{realWidth:90}]){
      f=fixture({noResize:true,nativeStyle});id=f.create('adunit-grid',[250,50,90,90],0);ad=f.ads[0];ad.emit('Load');f.api.PortCargoNativeAdShow(id);
      const valid=typeof nativeStyle.realWidth==='number'&&nativeStyle.realWidth>0&&typeof nativeStyle.realHeight==='number'&&isFinite(nativeStyle.realHeight)&&nativeStyle.realHeight>0;
      check(ad.shows===(valid?1:0),'only positive finite native realWidth/realHeight prove grid size '+JSON.stringify(nativeStyle));
      if(valid){ad.resolveShow();await flush();check(ad.visible,'square grid can display without horizontal aspect ratio');ad.style.realHeight=120;f.flags(id);check(!ad.visible&&ad.destroys===1,'native real-size growth is rechecked while visible');}
    }
    f=fixture({nativeStyle:{realWidth:90,realHeight:90}});id=f.create('adunit-grid',[250,50,90,90],0);ad=f.ads[0];ad.emit('Load');ad.emit('Resize',{width:80,height:80});
    ad.style.realWidth=999;f.api.PortCargoNativeAdShow(id);
    check(ad.shows===1&&f.api.PortCargoNativeAdRead(id,1)===80,'authoritative onResize supersedes optional stale style data');
    f=fixture();id=f.create('adunit-home',[40,300,240,84],0);ad=f.ads[0];ad.emit('Load');ad.emit('Resize',{width:320,height:112});
    f.api.PortCargoNativeAdShow(id);check(ad.shows===0&&ad.destroys===1,'Home template ignoring requested smaller width is refused');
    f=fixture({noResize:true,nativeStyle:{realWidth:320,realHeight:112}});id=f.create('adunit-home',[40,300,240,84],0);ad=f.ads[0];ad.emit('Load');
    check((f.flags(id)&4)!==0&&ad.destroys===1,'optional native actual dimensions enforce the same containment guard');
  });
  await suite('same ad unit across independent page visits',async()=>{
    const f=fixture(),old=f.create('adunit-completed'),ad=f.ready(old);f.api.PortCargoNativeAdShow(old);
    const oldLoad=ad.listeners.Load[0],oldError=ad.listeners.Error[0],oldClose=ad.listeners.Close[0],oldResize=ad.listeners.Resize[0];
    f.api.PortCargoNativeAdDestroy(old);const next=f.create('adunit-completed'),current=f.ready(next);f.api.PortCargoNativeAdShow(next);current.resolveShow();await flush();
    ad.resolveShow();await flush();oldLoad();oldError();oldClose();oldResize({width:999,height:999});
    check(next!==old&&current!==ad&&current.params.adUnitId===ad.params.adUnitId,'same ad unit still gets distinct lifetime handles and native objects');
    check(!ad.visible&&current.visible&&current.destroys===0,'old same-unit show and callbacks cannot hide or dismiss current page ad');
    check(f.flags(old)===4&&(f.flags(next)&8)!==0,'retired same-unit handle cannot alias current page');
  });
  await suite('verified fixed grid envelope without native resize',async()=>{
    let f=fixture({noResize:true}),id=f.create('adunit-2060c1fccf2ceecf',[240,50,80,120],2,[80,120]),ad=f.ads[0];ad.emit('Load');
    f.api.PortCargoNativeAdShow(id);check(ad.shows===1,'explicit fixed80x120 grid can display without an unsupported resize callback');
    check(!(f.flags(id)&16)&&f.api.PortCargoNativeAdRead(id,1)===0,'fixed envelope is not reported as measured native dimensions');
    ad.resolveShow();await flush();check(ad.visible,'native show Promise remains the proof of visibility');
    f.api.PortCargoNativeAdDestroy(id);check(!ad.visible&&ad.destroys===1,'fixed-profile native lease is cleaned on navigation');
    for(const args of [ [[240,50,79,120],[80,120]], [[240,50,80,119],[80,120]], [[240,50,80,120],[0,120]], [[240,50,80,120],[80,Infinity]], [[240,50,80,120],[NaN,120]] ]){
      f=fixture({noResize:true});check(f.create('adunit-grid',args[0],2,args[1])===0&&f.ads.length===0,'invalid or insufficient fixed reservation is rejected before native creation');
    }
    f=fixture();id=f.create('adunit-grid',[240,50,80,120],2,[80,120]);ad=f.ads[0];ad.emit('Load');ad.emit('Resize',{width:68,height:110});f.api.PortCargoNativeAdShow(id);
    check(ad.shows===1&&f.api.PortCargoNativeAdRead(id,2)===110,'real resize supersedes fixed grid envelope');
    ad.resolveShow();await flush();ad.emit('Resize',{width:81,height:110});
    check(!ad.visible&&ad.destroys===1&&(f.flags(id)&4)!==0,'late actual grid overflow hides even after fixed profile approved display');
    f=fixture({noResize:true,nativeStyle:{realWidth:80,realHeight:121}});id=f.create('adunit-grid',[240,50,80,120],2,[80,120]);ad=f.ads[0];ad.emit('Load');f.api.PortCargoNativeAdShow(id);
    check(ad.shows===0&&ad.destroys===1,'actual style dimensions cannot be overridden by smaller fixed profile');
  });
  console.log('NATIVE_AD_JSLIB_CONTRACT_PASS checks='+checks);
})().catch(error=>{console.error(error.stack||error);process.exitCode=1;});
