// Capture the running frontend using a curated external media selection.
// Never commit the selection, original media, private paths or hardware fixtures.
const {chromium}=require(require.resolve('playwright',{paths:[require('node:path').join(__dirname,'..','ui')]}));
const fs=require('node:fs/promises');const path=require('node:path');const assert=require('node:assert/strict');
const root=path.join(__dirname,'..');const output=path.join(root,'work','readme-recordings');
(async()=>{
 if(!process.env.DEADEYE_SHOWCASE)throw new Error('Set DEADEYE_SHOWCASE to an external curated media-selection JSON file.');
 const selection=JSON.parse(await fs.readFile(process.env.DEADEYE_SHOWCASE,'utf8'));
 const media=new Map();
 const games=selection.map((g,i)=>{
  const assets={};for(const [role,file] of Object.entries(g.media||{})){
   if(!['cover','previewVideo','screenshot','logo'].includes(role))continue;
   const url='/showcase/'+i+'-'+role+path.extname(file);media.set(url,file);assets[role]=url;
  }
  return {id:'showcase-'+i,title:g.title,platform:g.platform,aspect:g.aspect||'16:9',source:'custom',executable:'',arguments:[],workingDirectory:'',sourcePath:'',status:'unverified',favorite:i<3,lastPlayed:null,setupIssues:[],...assets};
 });
 const state={native:false,remoteSession:false,version:'0.3.5',games,bindings:[],devices:[],guns:[],ports:[],settings:{fullscreen:true,startWithWindows:false,hasCoverKey:false,language:'en',checkForUpdates:true},bindingStage:null,installations:[]};
 await fs.mkdir(output,{recursive:true});
 const browser=await chromium.launch();
 async function record(name,steps){
  const context=await browser.newContext({viewport:{width:1680,height:1050},recordVideo:{dir:output,size:{width:1680,height:1050}},colorScheme:'dark'});
  const page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.route('**/showcase/**',async route=>{
   const file=media.get(new URL(route.request().url()).pathname);if(!file)return route.abort();
   const ext=path.extname(file).toLowerCase();await route.fulfill({contentType:ext==='.mp4'?'video/mp4':ext==='.png'?'image/png':'image/jpeg',body:await fs.readFile(file)});
  });
  await page.addInitScript(initial=>{
   const listeners=[];const emit=(type,payload)=>listeners.forEach(fn=>fn({data:{type,payload}}));
   window.chrome={webview:{addEventListener:(_,fn)=>listeners.push(fn),removeEventListener:(_,fn)=>{const i=listeners.indexOf(fn);if(i>=0)listeners.splice(i,1);},postMessage:m=>{if(m.type==='ready')setTimeout(()=>emit('state',initial),0);}}};
  },state);
  await page.goto(process.env.REAPER_PREVIEW_URL||'http://127.0.0.1:5199/');
  await page.getByRole('heading',{name:games[0].title,exact:true}).waitFor();
  await page.waitForFunction(()=>[...document.querySelectorAll('.poster img')].every(i=>i.complete&&i.naturalWidth>0));
  const hold=(time=1500)=>page.waitForTimeout(time);
  const click=async(label)=>{const button=page.getByRole('button',{name:label,exact:true});await button.scrollIntoViewIfNeeded();await button.dispatchEvent('click');await hold(650);};
  const playing=async()=>{await page.waitForFunction(()=>{const v=document.querySelector('video');return v&&v.currentTime>.5&&!v.paused&&v.videoWidth>0;});};
  await playing();await hold(1500);await steps({page,click,hold,playing});
  assert.deepEqual(errors,[],'Recorded pages must have no runtime errors');
  const video=page.video();await context.close();await video.saveAs(path.join(output,name+'.webm'));
 }
 await record('deadeye-library',async({page,click,hold,playing})=>{
  await page.screenshot({path:path.join(root,'docs/images/deadeye-library.png')});await hold(2700);
  await click('Page down');await hold(1000);
  await click('Time Crisis 5 select');await page.getByRole('heading',{name:'Time Crisis 5',exact:true}).waitFor();await playing();assert.equal(await page.evaluate(()=>window.scrollY),0);await hold(3200);
  await click('Jurassic Park select');await page.getByRole('heading',{name:'Jurassic Park',exact:true}).waitFor();await playing();await hold(3200);
  await click('Blue Estate select');await page.getByRole('heading',{name:'Blue Estate',exact:true}).waitFor();await page.waitForFunction(()=>document.querySelector('.hero-art img')?.naturalWidth>0);await hold();
  await page.screenshot({path:path.join(root,'docs/images/deadeye-details.png')});await hold(1700);
  await click('House of the Dead: Scarlet Dawn select');await playing();await hold(2500);
  await click('Pause preview');await page.waitForFunction(()=>document.querySelector('video')?.paused===true,undefined,{timeout:3000});await hold(1300);
  await click('Play preview');await playing();await hold(1800);
 });
 await record('deadeye-gun-studio',async({page,click,hold})=>{
  await click('My Guns');
  await page.locator('.studio-console').evaluate(el=>window.scrollTo(0,el.getBoundingClientRect().top+window.scrollY-50));await hold();
  await page.screenshot({path:path.join(root,'docs/images/deadeye-gun-studio.png')});await hold(2500);
  for(const name of [/Sinden Lightgun.*USB/,/X-Gunner Wireless/,/Blamcon Vyper/,/RS3 Reaper Pro.*USB/]){
   await page.getByRole('button',{name}).dispatchEvent('click');await page.locator('.studio-console').evaluate(el=>window.scrollTo(0,el.getBoundingClientRect().top+window.scrollY-50));await hold(1800);
  }
  await click('Settings');await click('Deutsch');await click('Meine Guns');await hold(2000);
  await click('Einstellungen');await click('English');await click('My Guns');await hold(1700);
 });
 await browser.close();console.log('Recorded actual media playback, screenshot preview, library navigation and Gun Studio.');
})().catch(e=>{console.error(e);process.exit(1)});
