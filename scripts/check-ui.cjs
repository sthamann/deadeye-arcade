const {chromium}=require(require.resolve('playwright',{paths:[require('node:path').join(__dirname,'..','ui')]}));
const assert=require('node:assert/strict');
const url=process.env.REAPER_PREVIEW_URL||'http://127.0.0.1:5199/';
(async()=>{
 const browser=await chromium.launch();const page=await browser.newPage({viewport:{width:1440,height:1080}});const errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 await page.goto(url);await page.getByRole('heading',{name:'Guns nehmen. Spiele starten.'}).waitFor();
 assert.equal(await page.locator('.game-tile').count(),0,'No fabricated installed library');
 await page.getByRole('button',{name:'Mit Beispielspielen ansehen'}).click();
 assert.equal(await page.locator('.game-tile').count(),8);
 await page.getByRole('button',{name:'Time Crisis 5 auswählen'}).click();
 await page.getByRole('heading',{name:'Time Crisis 5',exact:true}).waitFor();
 await page.screenshot({path:require('node:path').join(__dirname,'..','oberflaeche.png'),fullPage:true});
 await page.getByRole('button',{name:'Favoriten',exact:true}).click();assert.equal(await page.locator('.game-tile').count(),0);
 await page.getByRole('button',{name:'Alle Spiele',exact:true}).click();assert.equal(await page.locator('.game-tile').count(),8);
 await page.getByRole('textbox',{name:'Spiele suchen'}).fill('jurassic');assert.equal(await page.locator('.game-tile').count(),1);
 await page.getByRole('textbox',{name:'Spiele suchen'}).fill('');
 await page.getByRole('button',{name:'Meine Guns',exact:true}).click();
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
  const listeners=[];window.fixture={sent:[],emit:(type,payload)=>listeners.forEach(fn=>fn({data:{type,payload}}))};
  window.chrome={webview:{addEventListener:(name,fn)=>listeners.push(fn),removeEventListener:()=>{},postMessage:message=>{
   window.fixture.sent.push(message);if(message.type==='ready')setTimeout(()=>window.fixture.emit('state',{native:true,version:'fixture',games:[],bindings:[{player:1,mouseId:'gun-1',keyboardId:'key-1',serialPort:null}],devices:[{id:'gun-1',name:'Fixture gun',kind:'mouse',retroShooter:true}],ports:[],settings:{fullscreen:true,startWithWindows:false,hasCoverKey:false},bindingStage:null,installations:[],remoteSession:false}),0);
  }}};
 });
 await native.goto(url);await native.getByRole('button',{name:/1 Gun verbunden/}).waitFor();
 const clickRaw=async(name)=>{
  const box=await native.getByRole('button',{name,exact:true}).boundingBox();assert(box);
  await native.evaluate(({x,y})=>window.fixture.emit('input',{deviceId:'gun-1',kind:'mouse',player:1,x:x/window.innerWidth,y:y/window.innerHeight,buttons:1}),{x:box.x+box.width/2,y:box.y+box.height/2});
 };
 await clickRaw('Meine Guns');await native.getByRole('heading',{name:'Gute Kontrolle. Ab dem ersten Schuss.'}).waitFor();
 await native.getByRole('button',{name:'Zieltest',exact:true}).first().evaluate(b=>b.click());
 // An unassigned mouse / the other player's gun cannot certify this player's target test.
 await native.evaluate(()=>window.fixture.emit('input',{deviceId:'other',kind:'mouse',player:2,x:.5,y:.5,buttons:1}));
 await native.getByText('0 / 5 Ziele · 0 Fehlschüsse', {exact:true}).waitFor();
 const positions=[[.5,.5],[.12,.16],[.88,.84],[.88,.16],[.12,.84]];
 for(const [x,y]of positions){await native.evaluate(({x,y})=>window.fixture.emit('input',{deviceId:'gun-1',kind:'mouse',player:1,x,y,buttons:1}),{x,y});await native.waitForTimeout(40);}
 await native.getByRole('heading',{name:'Eingabetest abgeschlossen.',exact:true}).waitFor();
 await clickRaw('Zurück zu meinen Guns');
 await native.getByRole('dialog',{name:'Zieltest'}).waitFor({state:'hidden'});
 await native.getByRole('button',{name:'Neu zuordnen',exact:true}).evaluate(b=>b.click());
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='bind'&&m.payload.player===1)),'Bind requests proper player');
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
 await native.evaluate(()=>window.fixture.emit('state',{native:true,remoteSession:true,installations:[],version:'remote fixture',games:[],bindings:[],devices:[],ports:[],settings:{fullscreen:true,startWithWindows:false,hasCoverKey:false},bindingStage:null}));
 await native.getByRole('button',{name:'Spiele finden',exact:true}).click();
 await native.getByRole('button',{name:'Installationen automatisch finden',exact:true}).click();
 assert(await native.evaluate(()=>window.fixture.sent.some(m=>m.type==='scan-installations')),'Remote mouse remains functional without raw input');
 await native.evaluate(()=>window.fixture.emit('import-result',{count:0,warnings:[],validation:true}));
 await native.getByRole('heading',{name:'0 Spiele mit vorhandenen Startdateien.'}).waitFor();
 await native.getByText('BIBLIOTHEK GEPRÜFT',{exact:true}).waitFor();
 assert.equal(errors.length,0,errors.join('\n'));
 console.log('UI passed: empty-library honesty, eight preview entries, selection/filter/search, target test, browser-native boundary, raw-input navigation, player isolation, binding command, four pages at five widths.');
 await browser.close();
})();
