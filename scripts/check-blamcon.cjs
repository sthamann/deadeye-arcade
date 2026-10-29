const {chromium}=require(require.resolve('playwright',{paths:[require('node:path').join(__dirname,'..','ui')]}));
const assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch();const page=await browser.newPage({viewport:{width:1920,height:1080}});const errors=[];const click=async locator=>{await locator.scrollIntoViewIfNeeded();await locator.evaluate(el=>el.scrollIntoView({block:'center',behavior:'instant'}));const box=await locator.boundingBox();assert(box);await page.evaluate(({x,y})=>window.fixture.emit('input',{kind:'mouse',player:1,x:x/innerWidth,y:y/innerHeight,buttons:1,down:true}),{x:box.x+box.width/2,y:box.y+box.height/2});};page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  const listeners=[];
  const guns=['rs3','rs3','xgunner','blamcon','blamcon'].map((systemId,i)=>({id:'physical-'+i,name:systemId==='blamcon'?'Blamcon Vyper '+(i-2):systemId,systemId,mouseId:'mouse-'+i,keyboardId:'keys-'+i,port:'COM'+(i+3),inputIds:['mouse-'+i,'keys-'+i],driverHealthy:true,issues:[],identityEvidence:'USB product name and container'}));
  const binding=(i,p)=>({player:p,mouseId:guns[i].mouseId,keyboardId:guns[i].keyboardId,systemId:guns[i].systemId,physicalId:guns[i].id,serialPort:guns[i].port});
  window.fixture={sent:[],state:{native:true,version:'test fixture',remoteSession:false,games:[],bindings:[binding(0,1),binding(1,2)],guns,devices:[],ports:[],settings:{language:'en'},installations:[],bindingStage:null},emit:(type,payload)=>listeners.forEach(fn=>fn({data:{type,payload}})),binding};
  window.chrome={webview:{addEventListener:(n,fn)=>listeners.push(fn),removeEventListener:()=>{},postMessage:m=>{window.fixture.sent.push(m);if(m.type==='ready')setTimeout(()=>window.fixture.emit('state',window.fixture.state),0);}}};
 });
 await page.goto(process.env.REAPER_PREVIEW_URL||'http://127.0.0.1:5199/');await page.locator('.gun-header-count').getByText('5 Guns detected',{exact:true}).waitFor();await click(page.getByRole('button',{name:'My Guns',exact:true}));
 await page.getByText('Choose a gun for each player',{exact:true}).waitFor();assert.equal(await page.locator('.assignment-gun').count(),5);
 const first=page.locator('.assignment-gun').filter({hasText:'Blamcon Vyper 1'});await click(first.getByRole('button',{name:'Use for P1',exact:true}));
 assert(await page.evaluate(()=>window.fixture.sent.some(m=>m.type==='assign-gun'&&m.payload.id==='physical-3'&&m.payload.player===1)));
 await page.evaluate(()=>{const s={...window.fixture.state,bindings:[window.fixture.binding(3,1),window.fixture.binding(4,2)]};window.fixture.state=s;window.fixture.emit('state',s);});
 assert.equal(await page.locator('.physical-controls button:not([disabled])').count(),12);
 await page.getByText('Capture 7 remaining buttons',{exact:true}).waitFor();
 await click(page.locator('.gun-hotspot[data-control="a"]'));
 await click(page.getByRole('button',{name:'End button test',exact:true}));
 await page.locator('.gun-hotspot[data-control="a"]').focus();
 await page.evaluate(()=>window.fixture.emit('input',{kind:'keyboard',deviceId:'keys-4',player:2,key:49,down:true,action:'start'}));
 assert(await page.evaluate(()=>window.fixture.sent.some(m=>m.type==='learn-control'&&m.payload.control==='a'&&m.payload.player===1)),'The physical diagram is operable by directional focus and confirm');
 assert(await page.evaluate(()=>{const ids=[...document.querySelectorAll('svg linearGradient')].map(n=>n.id);return ids.length===new Set(ids).size;}),'Each gun drawing needs its own gradient in mixed model and header views');
 assert((await page.locator('.mapping-panel').innerText()).includes('Button A'),'Actions identify the physical button instead of an opaque mouse number');
 await page.evaluate(()=>window.fixture.emit('gun-input',{player:1,token:'mouse:2',down:true,action:'reload'}));await page.locator('.gun-hotspot[data-control="a"].pressed').waitFor();
 await click(page.getByRole('button',{name:'Recoil & vibration',exact:true}));
 await click(page.getByRole('group',{name:'Recoil mode',exact:true}).getByRole('button',{name:'Full auto',exact:true}));await click(page.getByRole('group',{name:'Calibration profile',exact:true}).getByRole('button',{name:'4',exact:true}));
 await click(page.getByRole('button',{name:'Send settings to Vyper',exact:true}));
 assert(await page.evaluate(()=>window.fixture.sent.some(m=>m.type==='set-blamcon'&&m.payload.player===1&&m.payload.options.recoilMode==='auto'&&m.payload.options.calibrationProfile===4)));
 await click(page.getByRole('button',{name:'Test recoil',exact:true}));assert(await page.evaluate(()=>window.fixture.sent.some(m=>m.type==='test-blamcon-feedback'&&m.payload.effect==='recoil')));
 await page.evaluate(()=>window.fixture.emit('state',{...window.fixture.state,remoteSession:true}));assert(await page.getByRole('button',{name:'Test recoil',exact:true}).isDisabled());
 await page.getByRole('button',{name:'Setup & checks',exact:true}).click();await page.getByRole('heading',{name:'Set up and calibrate Vyper',exact:true}).waitFor();
 await page.getByRole('button',{name:'Install / open Blamcon ARC',exact:true}).click();assert(await page.evaluate(()=>window.fixture.sent.some(m=>m.type==='setup-gun'&&m.payload.system==='blamcon')));
 for(const width of [1920,1024,768,390,320]){await page.setViewportSize({width,height:1080});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false,'overflow at '+width);}
 await page.setViewportSize({width:1920,height:1080});
 if(process.env.REAPER_SCREENSHOT)await page.screenshot({path:process.env.REAPER_SCREENSHOT,fullPage:true});
 assert.deepEqual(errors,[]);await browser.close();console.log('Five-gun selection, Vyper A input, full controls, configuration payload, bounded feedback controls, RDP restriction, ARC and calibration guide passed at five widths.');
})().catch(e=>{console.error(e);process.exit(1)});
