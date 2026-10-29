const {chromium}=require(require.resolve('playwright',{paths:[require('node:path').join(__dirname,'..','ui')]}));
const assert=require('node:assert/strict');
(async()=>{
 const {RangeGame}=await import('../ui/src/RangeGame.ts');
 const game=new RangeGame([1,2]);
 const shoot={player:1,token:'mouse:1',action:'shoot',down:true,x:.16,y:.23};
 game.input(shoot,0);game.input(shoot,1);assert.equal(game.players.get(1).ammo,11);assert.equal(game.players.get(1).hits,1);assert.equal(game.players.get(2).ammo,12);
 game.input({...shoot,down:false},2);game.input(shoot,3);assert.equal(game.barrels[0].hp,0);game.tick(1804);assert.equal(game.barrels[0].hp,2);
 for(let i=0;i<13;i++){game.input({...shoot,down:false},10+i);game.input(shoot,10+i);}assert.equal(game.players.get(1).ammo,0);assert.equal(game.players.get(1).shots,12);
 game.input({player:2,token:'key:82',action:'reload',down:true},30);assert.equal(game.players.get(1).ammo,0);assert.equal(game.players.get(2).reloads,1);
 game.input({player:3,token:'mouse:1',action:'shoot',down:true},31);assert.equal(game.players.size,2);game.clearHeld();assert.equal(game.players.get(1).held.size,0);
 const browser=await chromium.launch();const page=await browser.newPage({viewport:{width:1440,height:1080}});const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{
  const listeners=[];const bindings=[{player:1,systemId:'xgunner',mouseId:'xm',keyboardId:'xk',hardwarePlayer:2,physicalId:'x'},{player:2,systemId:'blamcon',mouseId:'vm',keyboardId:'vk',hardwarePlayer:1,physicalId:'v',buttonMap:{'mouse:1':'shoot','key:82':'reload','mouse:2':'secondary','mouse:3':'auxiliary'}}];
  const state={native:true,version:'range fixture',remoteSession:false,games:[],bindings,guns:bindings.map(b=>({id:b.physicalId,systemId:b.systemId,name:b.systemId,mouseId:b.mouseId,keyboardId:b.keyboardId,driverHealthy:true,inputIds:[b.mouseId,b.keyboardId],issues:[]})),devices:[],ports:[],settings:{language:'en'},installations:[],bindingStage:null};
  window.fixture={state,sent:[],emit:(type,payload)=>listeners.forEach(fn=>fn({data:{type,payload}}))};window.chrome={webview:{addEventListener:(n,fn)=>listeners.push(fn),removeEventListener:()=>{},postMessage:m=>{window.fixture.sent.push(m);if(m.type==='ready')setTimeout(()=>window.fixture.emit('state',state),0);}}};
 });
 await page.goto(process.env.REAPER_PREVIEW_URL||'http://127.0.0.1:5199/');await page.getByRole('button',{name:'My Guns',exact:true}).evaluate(el=>el.click());await page.getByRole('button',{name:'Firing range · 1–2 players'}).evaluate(el=>el.click());
 await page.getByRole('dialog',{name:'Firing range',exact:true}).waitFor();assert.equal(await page.locator('.range-players article').count(),2);
 assert(await page.evaluate(()=>window.fixture.sent.some(m=>m.type==='range-test'&&m.payload.enabled===true)));
 const shot=await page.locator('.range-arena').boundingBox();assert(shot);
 const input=async p=>page.evaluate(p=>window.fixture.emit('gun-input',p),p);
 const base={player:2,token:'mouse:1',action:'shoot',down:true,x:(shot.x+shot.width*.16)/1440,y:(shot.y+shot.height*.23)/1080};
 await input(base);await input({...base,down:false});await page.getByTestId('ammo-2').getByText('11 / 12',{exact:true}).waitFor();assert.equal(await page.getByTestId('ammo-1').innerText(),'12 / 12');
 await input({player:2,token:'key:82',action:'reload',down:true,x:null,y:null});await page.getByTestId('ammo-2').getByText('12 / 12',{exact:true}).waitFor();await page.locator('[data-player="2"] .gun-hotspot[data-control="magazine"].pressed').waitFor();
 await input({player:2,token:'key:82',action:'reload',down:false});await input({player:1,token:'key:49',action:'start',down:true});await input({player:2,token:'key:53',action:'coin',down:true});assert.equal(await page.getByRole('dialog',{name:'Firing range',exact:true}).count(),1);assert((await page.locator('[data-player="2"]').innerText()).includes('Coins: 1'));
 const before=await page.locator('[data-player="2"]').innerText();await input({player:1,token:'key:81',action:'secondary',down:true});assert.equal(await page.locator('[data-player="2"]').innerText(),before);
 if(process.env.REAPER_SCREENSHOT)await page.screenshot({path:process.env.REAPER_SCREENSHOT,fullPage:true});
 for(const width of [1920,1024,768,390,320]){await page.setViewportSize({width,height:1080});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false,'Range overflow at '+width);}
 await page.setViewportSize({width:1440,height:1080});const back=await page.getByRole('button',{name:'Back to My Guns',exact:true}).boundingBox();assert(back);
 await input({...base,down:false});await input({...base,x:(back.x+back.width/2)/1440,y:(back.y+back.height/2)/1080});await page.getByRole('heading',{name:'Choose a gun for each player'}).waitFor();
 assert(await page.evaluate(()=>window.fixture.sent.some(m=>m.type==='range-test'&&m.payload.enabled===false)));assert.deepEqual(errors,[]);
 await browser.close();console.log('Range ammo, reload, barrel destruction/respawn, repeat filtering, mixed P1/P2 isolation, Vyper magazine visualization, menu isolation, raw gun exit and responsive layouts passed.');
})().catch(e=>{console.error(e);process.exit(1)});
