// Record actual UI navigation. No simulated hardware or installed games.
const {chromium}=require(require.resolve('playwright',{paths:[require('node:path').join(__dirname,'..','ui')]}));
const fs=require('node:fs/promises');const path=require('node:path');
const root=path.join(__dirname,'..');const output=path.join(root,'work','readme-recordings');
(async()=>{
 await fs.mkdir(output,{recursive:true});
 const browser=await chromium.launch();
 async function record(name,steps){
  const context=await browser.newContext({viewport:{width:1440,height:1080},recordVideo:{dir:output,size:{width:1440,height:1080}},colorScheme:'dark'});
  const page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
  await page.goto(process.env.REAPER_PREVIEW_URL||'http://127.0.0.1:5199/');
  await page.getByRole('heading',{name:'Grab your guns. Launch your games.'}).waitFor();
  const hold=()=>page.waitForTimeout(1200);
  const click=async(label)=>{await page.getByRole('button',{name:label,exact:true}).click();await page.evaluate(()=>window.scrollTo(0,0));await hold();};
  await hold();await steps({page,click,hold});
  if(errors.length)throw new Error(errors.join('\n'));
  await page.screenshot({path:path.join(root,'docs','images',name+'.png')});
  const video=page.video();await context.close();await video.saveAs(path.join(output,name+'.webm'));
 }
 await record('library',async({page,click,hold})=>{
  await click('Preview sample games');await click('Time Crisis 5 select');
  await click('Point Blank select');await click('Favorites');await click('All games');
  await page.getByRole('textbox',{name:'Search games'}).fill('jurassic');await hold();
  await page.getByRole('textbox',{name:'Search games'}).fill('');await click('Game details');
  await click('Close dialog');await click('Time Crisis 5 select');
 });
 await record('gun-studio-language',async({page,click,hold})=>{
  await click('My Guns');await page.getByRole('button',{name:/Sinden Lightgun.*USB/}).click();await hold();
  await page.getByRole('button',{name:/X-Gunner Wireless/}).click();await hold();
  await page.getByRole('button',{name:/Blamcon Vyper/}).click();await hold();
  await page.getByRole('button',{name:/RS3 Reaper Pro.*USB/}).click();await hold();
  await click('Recoil & vibration');await click('Setup & checks');await click('Settings');
  await click('Deutsch');await click('Meine Guns');await click('Rückstoß & Vibration');
  await click('Einstellungen');await click('English');await click('My Guns');
 });
 await browser.close();console.log('Recorded library + Gun Studio/language navigation.');
})().catch(e=>{console.error(e);process.exit(1)});
