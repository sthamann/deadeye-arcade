const {chromium}=require(require.resolve('playwright',{paths:[require('node:path').join(__dirname,'..','ui')]}));
const assert=require('node:assert/strict');
const url=process.env.REAPER_PREVIEW_URL||'http://127.0.0.1:5199/';
(async()=>{
 const browser=await chromium.launch();const page=await browser.newPage({viewport:{width:1440,height:1080}});const errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 await page.goto(url);await page.getByRole('heading',{name:'Grab your guns. Launch your games.'}).waitFor();
 assert.equal(await page.locator('html').getAttribute('lang'),'en','English is the first-run default');
 await page.getByRole('button',{name:'Settings',exact:true}).click();
 await page.getByRole('button',{name:'Deutsch',exact:true}).click();
 await page.getByRole('button',{name:'Meine Guns',exact:true}).click();
 await page.getByRole('heading',{name:'Deine Gun. Dein Setup.'}).waitFor();
 await page.getByText('USB · 4 IR-Punkte',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Einstellungen',exact:true}).click();
 await page.getByRole('button',{name:'English',exact:true}).click();
 await page.getByRole('button',{name:'My Guns',exact:true}).click();
 await page.getByRole('heading',{name:'Your gun. Your setup.'}).waitFor();
 await page.getByText('USB · 4 IR points',{exact:true}).waitFor();
 await page.reload();await page.getByRole('button',{name:'Settings',exact:true}).click();
 assert.equal(await page.getByRole('button',{name:'English',exact:true}).getAttribute('aria-pressed'),'true','English choice survives reload');
 await page.getByRole('button',{name:'Deutsch',exact:true}).click();
 await page.reload();assert.equal(await page.locator('html').getAttribute('lang'),'de','German choice survives reload');
 await page.getByRole('button',{name:'Spielen',exact:true}).click();
 await page.getByRole('heading',{name:'Guns nehmen. Spiele starten.'}).waitFor();
 assert.equal(await page.locator('.game-tile').count(),0,'No fabricated installed library');
 await page.getByRole('button',{name:'Mit Beispielspielen ansehen'}).click();
 assert.equal(await page.locator('.game-tile').count(),8);
 await page.getByRole('button',{name:'Time Crisis 5 auswählen'}).click();
 await page.getByRole('heading',{name:'Time Crisis 5',exact:true}).waitFor();

 await page.getByRole('button',{name:'Favoriten',exact:true}).click();assert.equal(await page.locator('.game-tile').count(),0);
 await page.getByRole('button',{name:'Alle Spiele',exact:true}).click();assert.equal(await page.locator('.game-tile').count(),8);
 await page.getByRole('textbox',{name:'Spiele suchen'}).fill('jurassic');assert.equal(await page.locator('.game-tile').count(),1);
 await page.getByRole('textbox',{name:'Spiele suchen'}).fill('');
 await page.getByRole('button',{name:'Meine Guns',exact:true}).click();
 await page.getByRole('button',{name:'Einrichtung & Prüfung',exact:true}).click();
 await page.getByRole('button',{name:'Zieltest',exact:true}).first().click();
 for(let i=1;i<=5;i++)await page.getByRole('button',{name:'Ziel '+i,exact:true}).click();
 await page.getByRole('heading',{name:'Bedienprobe abgeschlossen.',exact:true}).waitFor();
 await page.getByRole('button',{name:'Zurück zu meinen Guns',exact:true}).click();
 await page.getByRole('button',{name:'Spiele finden',exact:true}).click();await page.getByRole('button',{name:/TeknoParrot Vorhandene/}).click();
 await page.getByRole('status').filter({hasText:'Diese Funktion arbeitet'}).waitFor();
 for(const width of [1440,1024,768,390,320]){
  await page.setViewportSize({width,height:1000});
  for(const section of ['Spielen','Meine Guns','Spiele finden','Einstellungen']){
   await page.getByRole('button',{name:section,exact:true}).click();
   const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth);
   assert.equal(overflow,false,section+' overflow at '+width);
  }
 }
 const native=await browser.newPage({viewport:{width:1440,height:1000}});native.on('pageerror',e=>errors.push(e.message));
 await native.addInitScript(()=>{
  const listeners=[];window.fixture={sent:[],emit:(type,payload)=>{if(type==='state')window.fixture.lastState=payload;listeners.forEach(fn=>fn({data:{type,payload}}));}};
  window.chrome={webview:{addEventListener:(name,fn)=>listeners.push(fn),removeEventListener:()=>{},postMessage:message=>{
   window.fixture.sent.push(message);if(message.type==='ready')setTimeout(()=>window.fixture.emit('state',{native:true,version:'fixture',games:[],bindings:[{player:1,mouseId:'gun-1',keyboardId:'key-1',serialPort:'COM3',systemId:'rs3',physicalId:'physical-1',softwareConfigured:true}],guns:[{id:'physical-1',name:'3A-3H Retro Shooter 1',systemId:'rs3',mouseId:'gun-1',keyboardId:'key-1',port:'COM3',inputIds:['gun-1','key-1'],driverHealthy:true,issues:[],identityEvidence:'Fixture container'}],devices:[{id:'gun-1',name:'Fixture gun',kind:'mouse',retroShooter:true}],ports:[],settings:{fullscreen:true,startWithWindows:false,hasCoverKey:false,language:'de'},bindingStage:null,installations:[],remoteSession:false}),0);
  }}};
 });
 await native.goto(url);await native.getByRole('button',{name:'Lightguns einrichten'}).waitFor();
 const clickRaw=async(name,player=1)=>{
  const target=native.getByRole('button',{name,exact:true});await target.scrollIntoViewIfNeeded();const box=await target.boundingBox();assert(box);
  await native.evaluate(({x,y,player})=>window.fixture.emit('input',{deviceId:'gun-'+player,kind:'mouse',player,x:x/window.innerWidth,y:y/window.innerHeight,buttons:1}),{x:box.x+box.width/2,y:box.y+box.height/2,player});
 };
 await clickRaw('Einstellungen');
 await native.getByRole('button',{name:'English',exact:true}).evaluate(b=>b.click());
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='set-language'&&m.payload.language==='en')),'Native language switch requests persisted English');
 await native.getByRole('button',{name:'My Guns',exact:true}).waitFor();
 await native.evaluate(()=>window.fixture.emit('notice',{message:'Gun meldet P2. Die Oberfläche unterstützt aktuell P1/P2.'}));
 await native.getByRole('status').filter({hasText:'Gun reports P2.'}).waitFor();
 await native.getByRole('button',{name:'Deutsch',exact:true}).evaluate(b=>b.click());
 await native.getByRole('status').filter({hasText:'Gun meldet P2.'}).waitFor();
 await clickRaw('Meine Guns');await native.getByRole('heading',{name:'Deine Gun. Dein Setup.'}).waitFor();
 // RS3 HID usage 1 is the physical trigger even when mouse-mode shoot is remapped.
 await native.evaluate(()=>window.fixture.emit('gun-input',{player:1,token:'hid:1',down:true,action:'shoot'}));
 await native.waitForFunction(()=>document.querySelector('[data-control="trigger"].pressed'));
 await native.evaluate(()=>window.fixture.emit('gun-input',{player:1,token:'hid:1',down:false,action:'shoot'}));
 await native.waitForFunction(()=>!document.querySelector('[data-control="trigger"].pressed'));
 // Pure button testing retains visualization while suppressing menu routes.
 await native.waitForFunction(()=>window.fixture.sent.some(m=>m.type==='button-test'&&m.payload.player===1));
 await native.evaluate(()=>{
  window.fixture.emit('menu-action',{action:'coin'});
  window.fixture.emit('input',{deviceId:'key-1',kind:'keyboard',player:1,key:53,down:true,action:'coin'});
 });
 await clickRaw('Einstellungen');
 assert.equal(await native.getByRole('heading',{name:'Deine Gun. Dein Setup.'}).count(),1,'Test input cannot leave Gun Studio');
 assert(await native.evaluate(()=>!document.dispatchEvent(new MouseEvent('contextmenu',{bubbles:true,cancelable:true}))),'Test suppresses legacy context menus');
 await native.evaluate(()=>window.fixture.emit('gun-input',{player:1,token:'mouse:1',down:true,action:'shoot'}));
 await native.waitForFunction(()=>document.querySelector('.gun-hotspot.pressed'));
 await native.evaluate(()=>window.fixture.emit('gun-input',{player:1,token:'mouse:1',down:false,action:'shoot'}));
 await native.waitForFunction(()=>!document.querySelector('.gun-hotspot.pressed'));
 // The selected gun can reach setup and end its own test. P2 remains independently usable.
 await clickRaw('Einrichtung & Prüfung');
 await native.getByRole('button',{name:'P1 am Bildschirm kalibrieren',exact:true}).waitFor();
 await clickRaw('Tasten & Live-Eingabe');
 await clickRaw('Tastentest beenden');
 await native.getByText('Gun steuert das Menü',{exact:true}).waitFor();
 await clickRaw('Tastentest starten');
 await clickRaw('Einstellungen',2);
 await native.getByRole('textbox',{name:'SteamGridDB API Schlüssel'}).waitFor();
 await clickRaw('Meine Guns',2);
 const singleGunState=await native.evaluate(()=>window.fixture.lastState);
 await native.evaluate(()=>window.fixture.emit('state',{...window.fixture.lastState,bindings:[...window.fixture.lastState.bindings,{player:2,mouseId:'gun-2',keyboardId:'key-2',serialPort:'COM4',systemId:'rs3',physicalId:'physical-2',softwareConfigured:true}],guns:[...window.fixture.lastState.guns,{id:'physical-2',name:'3A-3H Retro Shooter 2',systemId:'rs3',mouseId:'gun-2',keyboardId:'key-2',port:'COM4',inputIds:['gun-2','key-2'],driverHealthy:true,issues:[],identityEvidence:'Fixture container'}]}));
 await clickRaw(/^P2\s*Verbunden$/,2);
 await native.waitForFunction(()=>window.fixture.sent.at(-1)?.type==='button-test'&&window.fixture.sent.at(-1)?.payload.player===2);
 await clickRaw('Einrichtung & Prüfung',2);
 await clickRaw('P2 am Bildschirm kalibrieren',2);
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='calibrate-rs3'&&m.payload.player===2)),'P2 trigger reaches its own physical calibration command');
 await clickRaw(/^P1\s*Verbunden$/,1);
 await clickRaw('Tasten & Live-Eingabe',1);
 await native.evaluate(s=>window.fixture.emit('state',s),singleGunState);
 // Attaching RDP during a physical button test must restore trusted mouse clicks.
 await native.evaluate(()=>window.fixture.emit('state',{...window.fixture.lastState,remoteSession:true}));
 await native.getByRole('button',{name:'Tastentest beenden',exact:true}).click();
 await native.getByText('Gun steuert das Menü',{exact:true}).waitFor();
 await native.getByRole('button',{name:'Einstellungen',exact:true}).click();
 await native.getByRole('textbox',{name:'SteamGridDB API Schlüssel'}).waitFor();
 await native.getByRole('button',{name:'Meine Guns',exact:true}).click();
 await native.getByRole('button',{name:'Tastentest beenden',exact:true}).waitFor();
 await native.evaluate(()=>window.fixture.emit('state',{...window.fixture.lastState,remoteSession:false}));
 await clickRaw('Einstellungen');
 assert.equal(await native.getByRole('heading',{name:'Deine Gun. Dein Setup.'}).count(),1,'Returning to the physical screen restores pure button testing');
 await native.evaluate(()=>window.fixture.emit('button-test-ended',{}));
 await native.getByText('Gun steuert das Menü',{exact:true}).waitFor();
 await native.waitForFunction(()=>window.fixture.sent.some(m=>m.type==='button-test'&&m.payload.player===null));
 await clickRaw('Einstellungen');
 await native.getByRole('textbox',{name:'SteamGridDB API Schlüssel'}).waitFor();
 await clickRaw('Meine Guns');
 await native.getByRole('button',{name:'Tastentest beenden',exact:true}).evaluate(b=>b.click());
 // Physical highlight follows the input, even when its assigned action is changed.
 await native.evaluate(()=>window.fixture.emit('gun-input',{player:1,token:'mouse:1',down:true,action:'reload'}));
 await native.waitForFunction(()=>document.querySelector('[data-control="trigger"].pressed'));
 assert.equal(await native.locator('[data-control="reload"].pressed').count(),0);
 await native.evaluate(()=>window.fixture.emit('gun-input',{player:1,token:'mouse:1',down:false,action:'reload'}));
 await native.locator('.physical-row').filter({hasText:'Magazinboden'}).evaluate(b=>b.click());
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='learn-control'&&m.payload.control==='magazine')),'Physical control capture is separate from action binding');
 const studioPaths=[];
 for(const model of ['Sinden Lightgun','X-Gunner Wireless','Blamcon Vyper','RS3 Reaper Pro']){
  await native.locator('.system-card').filter({hasText:model}).evaluate(b=>b.click());
  studioPaths.push(await native.locator('.gun-live-panel .gun-shape').innerHTML());
 }
 assert.equal(new Set(studioPaths).size,4,'All manufacturers have distinct anatomy and physical controls');
 await native.getByRole('button',{name:'Einrichtung & Prüfung',exact:true}).evaluate(b=>b.click());
 await native.getByRole('button',{name:'Kalibrierung automatisch vorbereiten',exact:true}).evaluate(b=>b.click());
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='prepare-calibration')),'Calibration package preparation reaches Windows');
 await native.getByRole('button',{name:'P1 am Bildschirm kalibrieren',exact:true}).evaluate(b=>b.click());
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='calibrate-rs3'&&m.payload.player===1)),'Calibration uses the selected physical player');
 await native.evaluate(()=>window.fixture.emit('state',{...window.fixture.lastState,remoteSession:true}));
 assert(await native.getByRole('button',{name:'P1 am Bildschirm kalibrieren',exact:true}).isDisabled(),'Remote Desktop cannot start physical screen calibration');
 assert(await native.getByRole('button',{name:'Kalibrierung automatisch vorbereiten',exact:true}).isEnabled(),'Remote Desktop can still prepare the vendor module');
 await native.evaluate(()=>window.fixture.emit('state',{...window.fixture.lastState,remoteSession:false}));

 await native.getByRole('button',{name:'Zieltest',exact:true}).first().evaluate(b=>b.click());
 // An unassigned mouse / the other player's gun cannot certify this player's target test.
 await native.evaluate(()=>window.fixture.emit('input',{deviceId:'other',kind:'mouse',player:2,x:.5,y:.5,buttons:1}));
 await native.getByText('0 / 5 Ziele · 0 Fehlschüsse', {exact:true}).waitFor();
 const positions=[[.5,.5],[.12,.16],[.88,.84],[.88,.16],[.12,.84]];
 for(const [x,y]of positions){await native.evaluate(({x,y})=>window.fixture.emit('input',{deviceId:'gun-1',kind:'mouse',player:1,x,y,buttons:1}),{x,y});await native.waitForTimeout(40);}
 await native.getByRole('heading',{name:'Eingabetest abgeschlossen.',exact:true}).waitFor();
 await clickRaw('Zurück zu meinen Guns');
 await native.getByRole('dialog',{name:'Zieltest'}).waitFor({state:'hidden'});
 await native.getByRole('button',{name:'Tasten & Live-Eingabe',exact:true}).evaluate(b=>b.click());
 await native.locator('.mapping-row').first().evaluate(b=>b.click());
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='learn-button'&&m.payload.player===1&&m.payload.action==='shoot')),'Physical input learning requests the selected player and action');
 await native.getByRole('button',{name:'Rückstoß & Vibration',exact:true}).evaluate(b=>b.click());
 await native.getByRole('button',{name:'Rückstoß im Test aktivieren'}).evaluate(b=>b.click());
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='set-gun-feedback'&&m.payload.feedback.recoil===false)),'Feedback toggle persists actual test selection');
 await native.evaluate(()=>window.fixture.emit('picker',{title:'Ordner wählen',foldersOnly:true,page:{path:'C:/Arcade',parent:'C:/',entries:[{name:'TeknoParrot',path:'C:/Arcade/TeknoParrot',directory:true}],warning:null},shortcuts:[{name:'C:',path:'C:/'}]}));
 await native.waitForTimeout(350);
 await clickRaw('Diesen Ordner verwenden');
 await native.waitForTimeout(100);
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='choose-path'&&m.payload.path==='C:/Arcade')),'Gun selects actual folder through full-screen picker');
 await native.evaluate(()=>window.fixture.emit('picker',{closed:true}));
 await clickRaw('Einstellungen');
 await native.getByRole('textbox',{name:'SteamGridDB API Schlüssel'}).focus();
 await native.getByRole('dialog',{name:'Cover-Schlüssel eingeben'}).waitFor();
 await clickRaw('a');await clickRaw('b');await clickRaw('Übernehmen');
 assert.equal(await native.getByRole('textbox',{name:'SteamGridDB API Schlüssel'}).inputValue(),'ab','Gun keyboard enters cover key without physical keyboard');
 await native.evaluate(()=>window.fixture.emit('state',{native:true,remoteSession:true,installations:[],version:'remote fixture',games:[],bindings:[],devices:[],ports:[],settings:{fullscreen:true,startWithWindows:false,hasCoverKey:false,language:'de'},bindingStage:null}));
 await native.getByRole('button',{name:'Spiele finden',exact:true}).click();
 await native.getByRole('button',{name:'Installationen automatisch finden',exact:true}).click();
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='scan-installations')),'Remote mouse remains functional without raw input');
 await native.evaluate(()=>window.fixture.emit('import-result',{count:0,warnings:[],validation:true}));
 await native.getByRole('heading',{name:'0 Spiele mit vorhandenen Startdateien.'}).waitFor();
 await native.getByText('BIBLIOTHEK GEPRÜFT',{exact:true}).waitFor();
 // A long library must be navigable without a wheel, then selection returns to the launch area.
 await native.getByRole('button',{name:'Zur Bibliothek',exact:false}).evaluate(b=>b.click());
 const longGames=Array.from({length:72},(_,i)=>({id:'scroll-'+i,title:'Scroll game '+i,platform:'MAME',source:'custom',sourcePath:'fixture',executable:'fixture',workingDirectory:'fixture',arguments:[],status:'unverified',cover:null,favorite:false,aspect:'4:3',lastPlayed:null}));
 await native.evaluate(games=>window.fixture.emit('state',{native:true,remoteSession:false,installations:[],version:'fixture',games,bindings:[],devices:[],ports:[],settings:{fullscreen:true,startWithWindows:false,hasCoverKey:false,language:'de'},bindingStage:null}),longGames);
 await native.waitForFunction(()=>document.querySelectorAll('.game-tile').length===72);
 await native.evaluate(()=>window.scrollTo(0,0));
 await clickRaw('Eine Seite nach unten');
 await native.waitForFunction(()=>window.scrollY>300);
 await native.locator('.game-tile').last().scrollIntoViewIfNeeded();
 await clickRaw('Scroll game 71 auswählen');
 await native.waitForFunction(()=>window.scrollY===0&&document.querySelector('.hero h1')?.textContent==='Scroll game 71');
 assert.equal(await native.evaluate(()=>document.activeElement?.textContent?.trim()),'Spiel starten','Selection focuses launch without starting automatically');
 const handoffPath=process.env.REAPER_HANDOFF_CHECK;
 if(handoffPath){
  const {readFileSync}=require('node:fs');
  const imported=JSON.parse(readFileSync(handoffPath)).games;
  assert(imported.length > 0,'Collection fixture must contain games');
  await native.evaluate(()=>{window.mediaDiagnostic=[];document.addEventListener('error',e=>{if(e.target instanceof HTMLVideoElement)window.mediaDiagnostic.push({code:e.target.error?.code,message:e.target.error?.message,src:e.target.currentSrc});},true);});
  native.on('requestfailed',r=>{if(r.url().includes('fixture.mp4'))console.log('Video network:',r.failure());});
  const first={...imported[0],status:'unverified',setupIssues:[],previewVideo:'http://127.0.0.1:5199/fixture.mp4'};
  await native.route('http://127.0.0.1:5199/fixture.mp4',route=>route.fulfill({contentType:'video/mp4',body:readFileSync(process.env.REAPER_VIDEO_FIXTURE)}));
  await native.getByRole('button',{name:'Zur Bibliothek',exact:false}).click();
  await native.evaluate(games=>window.fixture.emit('state',{native:true,remoteSession:false,installations:[],version:'0.3 fixture',games,bindings:[],devices:[],ports:[],settings:{fullscreen:true,startWithWindows:false,hasCoverKey:false,language:'de'},bindingStage:null}),[first,...imported.slice(1)]);
  await native.waitForFunction(()=>document.querySelector('video')?.currentTime>.1,{},{timeout:5000}).catch(async e=>{console.log('Media diagnostic:',await native.evaluate(()=>({video:document.querySelector('video')?.outerHTML, caption:document.querySelector('.video-caption')?.textContent, codecs:document.createElement('video').canPlayType('video/mp4; codecs="avc1.42E01E"'), errors:window.mediaDiagnostic, tiles:document.querySelectorAll('.game-tile').length, heading:document.querySelector('.hero h1')?.textContent})));throw e;});
  assert.equal(await native.locator('.game-tile').count(),imported.length,'All fixture games remain visible');
  await clickRaw('Vorschau pausieren');
  await native.waitForFunction(()=>document.querySelector('video')?.paused===true);
  assert(await native.locator('video').evaluate(v=>v.paused),'Preview can be paused');
  await clickRaw('Vorschau abspielen');
  await native.waitForFunction(()=>document.querySelector('video')?.paused===false);
  await clickRaw('Spieldetails');
  await native.waitForFunction(()=>document.querySelector('video')?.paused===true);
  assert(await native.locator('video').evaluate(v=>v.paused),'Dialog pauses preview');
  await clickRaw('Dialog schließen');
  await native.waitForFunction(()=>document.querySelector('video')?.paused===false);
  await native.evaluate(()=>window.fixture.emit('session',{status:'running'}));
  await native.waitForFunction(()=>document.querySelector('video')?.paused===true);
  assert(await native.locator('video').evaluate(v=>v.paused),'Actual session message pauses preview');
  await native.evaluate(()=>window.fixture.emit('session',{status:'ended'}));
  await clickRaw('Erste Auswahl');
  assert.equal(await native.locator('.game-tile').count(),imported.filter(g=>g.priority===1).length,'Priority filter preserves fixture priorities');
  await clickRaw('Alle Spiele');
  await native.getByLabel('System filtern').selectOption('Nintendo Wii');
  assert.equal(await native.locator('.game-tile').count(),imported.filter(g=>g.platform==='Nintendo Wii').length,'Wii system filter matches fixture');
  await native.getByLabel('System filtern').selectOption('all');
  await clickRaw('Dateien vorhanden');
  assert.equal(await native.locator('.game-tile').count(),1,'Unavailable fixture games cannot appear file ready');
  await clickRaw('Spiele finden');
  assert.equal(await native.locator('video').count(),0,'View change removes the video player');
  await clickRaw('Übergabepaket importieren');
  assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='import-collection')),'Native handoff import is connected');
  console.log('Collection UI passed: fixture entries, priorities, platform filter, missing-file filter, real MP4 playback and pause on dialog/session/view change.');
 }
 await native.evaluate(()=>window.fixture.emit('session',{status:'running'}));
 await native.getByRole('button',{name:'Spielmenü öffnen',exact:true}).dispatchEvent('click');
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='show-overlay')),'Running session offers the native game overlay command');
 await native.evaluate(()=>window.fixture.emit('session',{status:'ended'}));
 const updateFixture={native:true,remoteSession:true,installations:[],version:'0.3.2',games:[],bindings:[],devices:[],ports:[],settings:{fullscreen:true,startWithWindows:false,hasCoverKey:false,language:'de',checkForUpdates:true},bindingStage:null,update:{status:'available',progress:0,error:null,release:{version:'0.3.3',notes:'Verified release',page:'https://github.com/sthamann/deadeye-arcade/releases/tag/v0.3.3'}}};
 if(await native.getByRole('button',{name:'Zur Bibliothek',exact:false}).isVisible()) await native.getByRole('button',{name:'Zur Bibliothek',exact:false}).click();
 await native.evaluate(s=>window.fixture.emit('state',s),updateFixture);
 await native.getByRole('button',{name:'Einstellungen',exact:true}).click();
 await native.getByText('Neue Version verfügbar.',{exact:false}).waitFor();
 await native.getByRole('button',{name:'Nach Updates suchen',exact:true}).click();
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='check-updates')),'Manual update check reaches Windows');
 await native.getByRole('switch',{name:'Automatisch nach Updates suchen'}).click();
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='update-preference'&&m.payload.enabled===false)),'Automatic update preference is persisted');
 await native.evaluate(()=>window.fixture.emit('session',{status:'running'}));
 await native.waitForFunction(()=>[...document.querySelectorAll('button')].find(b=>b.textContent.includes('Update installieren'))?.disabled===true);
 assert(await native.getByRole('button',{name:'Update installieren und neu starten'}).isDisabled(),'Updates cannot interrupt an active game');
 await native.evaluate(()=>window.fixture.emit('session',{status:'ended'}));
 await native.getByRole('button',{name:'Update installieren und neu starten'}).click();
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='install-update')),'Install and restart reaches Windows');
 await native.getByRole('button',{name:'English',exact:true}).click();
 await native.getByRole('button',{name:'Install update and restart'}).waitFor();
 await native.getByText('App updates',{exact:true}).waitFor();
 await native.evaluate(s=>window.fixture.emit('state',s),{...updateFixture,settings:{...updateFixture.settings,language:'en'},update:{...updateFixture.update,status:'downloading',progress:37}});
 await native.getByRole('status').filter({hasText:'Downloading update'}).waitFor();
 assert(await native.getByRole('button',{name:'Install update and restart'}).isDisabled(),'Downloading blocks duplicate installers');
 assert((await native.getByRole('status').filter({hasText:'Downloading update'}).innerText()).includes('37%'),'Download progress is visible');
 // Enriched game facts must be visible after selection and survive language changes.
 const enrichedGames=[{...longGames[0],title:'Enriched game',description:'A short English description of the selected rail shooter.',releaseYear:2009,hardware:'Nintendo Wii',metadataSources:['https://example.com/game']},{...longGames[1],title:'Prototype game',description:'An unreleased target-shooting prototype.',releaseYear:null,releaseInfo:'Unreleased prototype',hardware:'Atari 2600'}];
 await native.evaluate(games=>window.fixture.emit('state',{...window.fixture.lastState,games,settings:{...window.fixture.lastState.settings,language:'en'}}),enrichedGames);
 await native.getByRole('button',{name:'Play',exact:true}).click();
 await native.getByRole('button',{name:'Enriched game select',exact:true}).click();
 await native.locator('.hero').getByText(enrichedGames[0].description,{exact:true}).waitFor();
 await native.locator('.hero-meta').getByText('2009',{exact:true}).waitFor();
 await native.locator('.hero-meta').getByText('Nintendo Wii',{exact:true}).waitFor();
 await native.getByRole('button',{name:'Game details',exact:true}).click();
 await native.locator('.game-facts').getByText('Release year',{exact:true}).waitFor();
 await native.locator('.game-facts').getByText('Original hardware',{exact:true}).waitFor();
 await native.getByRole('button',{name:'Close dialog',exact:true}).click();
 await native.getByRole('button',{name:'Prototype game select',exact:true}).click();
 await native.locator('.hero-meta').getByText('Unreleased prototype',{exact:true}).waitFor();
 await native.setViewportSize({width:1366,height:768});
 assert.equal(await native.evaluate(()=>document.documentElement.scrollWidth>window.innerWidth),false,'Enriched game facts fit the Windows display');
 await native.getByRole('button',{name:'Settings',exact:true}).click();
 await native.getByRole('button',{name:'Deutsch',exact:true}).click();
 await native.getByRole('button',{name:'Spielen',exact:true}).click();
 await native.getByRole('button',{name:'Spieldetails',exact:true}).click();
 await native.locator('.game-facts').getByText('Erscheinungsjahr',{exact:true}).waitFor();
 await native.locator('.game-facts').getByText('Original-Hardware',{exact:true}).waitFor();
 await native.evaluate(()=>window.fixture.emit('state',{...window.fixture.lastState,settings:{...window.fixture.lastState.settings,language:'de'},gameChecks:Object.fromEntries(window.fixture.lastState.games.map(g=>[g.id,{title:'Startprüfung benötigt Aufmerksamkeit',notes:['Kein Spielfenster in der Beobachtungszeit erkannt.','Treffer, Rückstoß und gleichzeitiges Spielen mit beiden Guns benötigen weiterhin einen Test am angeschlossenen Bildschirm.']}]))}));
 await native.getByText('Startprüfung benötigt Aufmerksamkeit',{exact:true}).waitFor();
 await native.getByText('Kein Spielfenster in der Beobachtungszeit erkannt.',{exact:true}).waitFor();
 assert.equal(await native.getByText('Start und Beenden geprüft',{exact:true}).count(),0,'Failed native launch evidence cannot appear as a successful game check');

 await native.getByRole('button',{name:'Dialog schließen',exact:true}).click();
 if(process.env.REAPER_VIDEO_FIXTURE){
  const {readFileSync}=require('node:fs');
  await native.route('**/broken-enrichment.mp4',r=>r.fulfill({contentType:'video/mp4',body:'invalid video'}));
  await native.route('**/working-enrichment.mp4',r=>r.fulfill({contentType:'video/mp4',body:readFileSync(process.env.REAPER_VIDEO_FIXTURE)}));
  await native.evaluate(games=>window.fixture.emit('state',{...window.fixture.lastState,games,settings:{...window.fixture.lastState.settings,language:'de'}}),[{...enrichedGames[0],previewVideo:url+'broken-enrichment.mp4'},{...enrichedGames[1],previewVideo:url+'working-enrichment.mp4'}]);
  await native.getByRole('button',{name:'Enriched game auswählen',exact:true}).click();
  await native.getByText('Video nicht abspielbar',{exact:true}).waitFor();
  await native.getByRole('button',{name:'Prototype game auswählen',exact:true}).click();
  await native.waitForFunction(()=>document.querySelector('video')?.currentTime>.2);
  console.log('Preview recovery passed: corrupt video falls back, the next selected game plays a real MP4.');
 }
 console.log('Enrichment UI passed: selected English description, edition year, original hardware, prototype status, German labels and Windows layout.');
 await native.evaluate(()=>{
  window.fixture.emit('busy',{message:''});
  window.fixture.emit('state',{...window.fixture.lastState,settings:{...window.fixture.lastState.settings,language:'en'},update:{status:'idle'},
   display:{time:new Date().toISOString(),remote:true,outputs:[{device:'fixture-rdp',adapter:'Microsoft Remote Display Adapter',primary:true,current:{width:1920,height:1080,hz:32},saved:{width:1920,height:1080,hz:32}}]},
   fixes:{time:new Date().toISOString(),catalogVersion:1,games:2,
    findings:[{gameId:'fixture',game:'Fixture game',ruleId:'openal',status:'repaired',message:'Fehlende OpenAL-Laufzeit in passender Architektur ergänzt und erneut gelesen.',files:['fixture.dll']}],
    history:[{gameId:'fixture',game:'Fixture game',ruleId:'usb-identity',status:'configured',message:'Aktives Spielprofil automatisch angepasst; geänderte Dateien protokolliert.',files:['fixture.ini']}],
    catalog:[{id:'openal',name:'OpenAL-Laufzeit',mode:'automatic',scope:'OpenAL',source:'https://openal-soft.org/',verification:'Fixture'}]}});
 });
 await native.getByRole('button',{name:'Settings',exact:true}).click();
 await native.getByText('Automatic repairs',{exact:true}).waitFor();
 await native.getByText('Remote Desktop is active. Its frame rate does not confirm the physical display refresh rate.',{exact:true}).waitFor();
 await native.getByText('Results and next steps',{exact:true}).click();
 await native.getByText('Missing OpenAL runtime added with the matching architecture and read back.',{exact:true}).waitFor();
 await native.getByText('Recent automatic changes',{exact:true}).click();
 await native.getByText('Active game profile configured automatically; changed files recorded.',{exact:true}).waitFor();
 await native.getByRole('button',{name:'Check and repair library',exact:true}).click();
 assert(await native.evaluate(()=>window.fixture.sent.at(-1)?.type==='check-dependencies'),'Repair action reaches the existing native dependency path');
 await native.getByRole('button',{name:'Deutsch',exact:true}).click();
 await native.getByText('Automatische Reparaturen',{exact:true}).waitFor();
 await native.getByText('Fehlende OpenAL-Laufzeit in passender Architektur ergänzt und erneut gelesen.',{exact:true}).waitFor();
 console.log('Repair UI passed: native command, persisted findings/history, English/German switching and honest RDP display scope.');
 assert.equal(errors.length,0,errors.join('\n'));
 console.log('UI passed: empty-library honesty, eight preview entries, selection/filter/search, target test, browser-native boundary, raw-input navigation, player isolation, binding command, four pages at five widths.');
 await browser.close();
})();
