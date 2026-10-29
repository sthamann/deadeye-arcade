import {useEffect,useRef,useState} from 'react';
import type {State,Binding} from './types';
import {t} from './i18n';
import {controls,GunShape,type Control} from './GunHardware';
import {RangeGame,type RangeInput} from './RangeGame';
import './shooting-range.css';
const colors=['#5bbcff','#ffad50'];
export function ShootingRange({state,close}:{state:State;close:()=>void}){
 const active=state.bindings.filter(b=>(state.guns??[]).some(g=>g.mouseId===b.mouseId)).slice(0,2);
 const players:Binding[]=active.length?active:state.native?[]:[{player:1,mouseId:'preview',keyboardId:null,serialPort:null,systemId:'rs3'}];
 const engine=useRef(new RangeGame(players.map(b=>b.player)));
 const identity=players.map(b=>`${b.player}:${b.mouseId}`).join("|");
 const previousIdentity=useRef(identity);
 if(previousIdentity.current!==identity){engine.current=new RangeGame(players.map(b=>b.player));previousIdentity.current=identity;}
 const canvas=useRef<HTMLCanvasElement>(null);
 const [frame,refresh]=useState(0);
 const [last,setLast]=useState<RangeInput|null>(null);
 const surface=useRef<HTMLDivElement>(null);
 const input=useRef((v:RangeInput)=>{});
 input.current=(v)=>{
  const rect=surface.current?.getBoundingClientRect();
  if(v.down && (v.token===(players.find(b=>b.player===v.player)?.controlMap?.trigger??'mouse:1')||v.action==='shoot')) {
   const p=engine.current.players.get(v.player);
   const x=typeof v.x==='number'?v.x*innerWidth:rect&&p?rect.left+p.x*rect.width:undefined;
   const y=typeof v.y==='number'?v.y*innerHeight:rect&&p?rect.top+p.y*rect.height:undefined;
   const target=x!==undefined&&y!==undefined?document.elementFromPoint(x,y)?.closest<HTMLButtonElement>('.shooting-range button[data-range-navigation]'):null;
   if(target){target.click();return;}
  }
  const event={...v};
  if(rect&&typeof v.x==='number'&&typeof v.y==='number'){event.x=(v.x*innerWidth-rect.left)/rect.width;event.y=(v.y*innerHeight-rect.top)/rect.height;}
  engine.current.input(event,performance.now());setLast(v);refresh(n=>n+1);
 };
 useEffect(()=>{
  const receive=(e:Event)=>input.current((e as CustomEvent<RangeInput>).detail);
  const aim=(e:Event)=>{const v=(e as CustomEvent<RangeInput>).detail;const p=engine.current.players.get(v.player);const r=surface.current?.getBoundingClientRect();if(p&&r&&typeof v.x==='number'&&typeof v.y==='number'){p.x=(v.x*innerWidth-r.left)/r.width;p.y=(v.y*innerHeight-r.top)/r.height;}};
  const clear=()=>{engine.current.clearHeld();refresh(n=>n+1);};
  window.addEventListener('deadeye-range-input',receive);window.addEventListener('deadeye-range-aim',aim);window.addEventListener('blur',clear);
  return()=>{window.removeEventListener('deadeye-range-input',receive);window.removeEventListener('deadeye-range-aim',aim);window.removeEventListener('blur',clear);};
 },[]);
 useEffect(()=>{engine.current.clearHeld();},[state.remoteSession,state.inputTransport?.generation]);
 useEffect(()=>{
  let id=0;const draw=(now:number)=>{
   const c=canvas.current,ctx=c?.getContext('2d');if(!c||!ctx)return;
   const w=c.clientWidth,h=c.clientHeight,dpr=Math.min(devicePixelRatio,2);
   if(c.width!==Math.round(w*dpr)||c.height!==Math.round(h*dpr)){c.width=Math.round(w*dpr);c.height=Math.round(h*dpr);}
   ctx.setTransform(dpr,0,0,dpr,0,0);engine.current.tick(now);
   const wall=ctx.createLinearGradient(0,0,0,h);wall.addColorStop(0,'#12253c');wall.addColorStop(.65,'#19242b');wall.addColorStop(1,'#080e17');ctx.fillStyle=wall;ctx.fillRect(0,0,w,h);
   ctx.strokeStyle='#344453';ctx.lineWidth=1;
   for(let i=0;i<10;i++){const y=i*h/10;ctx.beginPath();ctx.moveTo(0,y);ctx.lineTo(w,y);ctx.stroke();}
   for(let i=-5;i<=5;i++){ctx.beginPath();ctx.moveTo(w/2,h*.58);ctx.lineTo(w/2+i*w/5,h);ctx.stroke();}
   ctx.font='700 '+Math.max(16,w*.028)+'px system-ui';ctx.fillStyle='#526578';ctx.textAlign='center';ctx.fillText('DEADEYE // FIRING RANGE',w/2,h*.07);
   for(const b of engine.current.barrels){
    const x=b.x*w,y=b.y*h,bw=w*.09,bh=h*.15;
    ctx.fillStyle='#0007';ctx.beginPath();ctx.ellipse(x,y+bh*.55,bw*.7,bh*.16,0,0,Math.PI*2);ctx.fill();
    if(!b.hp)continue;
    const steel=ctx.createLinearGradient(x-bw/2,0,x+bw/2,0);steel.addColorStop(0,'#4a251a');steel.addColorStop(.3,b.hp===1?'#d0652b':'#b89141');steel.addColorStop(.65,'#654527');steel.addColorStop(1,'#251d1a');ctx.fillStyle=steel;ctx.fillRect(x-bw/2,y-bh/2,bw,bh);
    ctx.fillStyle='#af884d';ctx.beginPath();ctx.ellipse(x,y-bh/2,bw/2,bh*.12,0,0,Math.PI*2);ctx.fill();
    for(const dy of [-.32,.28]){ctx.fillStyle='#273137';ctx.fillRect(x-bw*.52,y+bh*dy,bw*1.04,bh*.08);ctx.fillStyle='#8b9da0';ctx.fillRect(x-bw*.5,y+bh*dy,bw,bh*.015);}
    ctx.fillStyle='#efdd97';ctx.fillRect(x-bw*.27,y-bh*.16,bw*.54,bh*.3);ctx.fillStyle='#32281e';ctx.font='bold '+Math.max(14,bw*.25)+'px system-ui';ctx.fillText('×',x,y+bh*.08);
    if(b.hp===1){ctx.fillStyle='#171d23';ctx.beginPath();ctx.arc(x+bw*.17,y-bh*.24,bw*.07,0,Math.PI*2);ctx.fill();}
   }
   for(const s of engine.current.sparks){const age=(now-s.at)/550,x=s.x*w,y=s.y*h;ctx.globalAlpha=1-age;ctx.strokeStyle=colors[s.player-1]??colors[0];ctx.lineWidth=3;ctx.beginPath();ctx.arc(x,y,8+age*55,0,Math.PI*2);ctx.stroke();if(s.hit){for(let i=0;i<12;i++){let a=i*Math.PI/6;ctx.fillStyle=i%2?'#ffad4a':'#fff1b7';ctx.fillRect(x+Math.cos(a)*age*95,y+Math.sin(a)*age*75+age*age*70,5,5);}}ctx.globalAlpha=1;}
   for(const [player,p] of engine.current.players){if(p.x<0||p.x>1||p.y<0||p.y>1)continue;ctx.strokeStyle=colors[player-1];ctx.lineWidth=2;const x=p.x*w,y=p.y*h;ctx.beginPath();ctx.arc(x,y,17,0,Math.PI*2);ctx.moveTo(x-25,y);ctx.lineTo(x+25,y);ctx.moveTo(x,y-25);ctx.lineTo(x,y+25);ctx.stroke();ctx.fillStyle=colors[player-1];ctx.font='bold 15px system-ui';ctx.fillText('P'+player,x,y-32);}
   id=requestAnimationFrame(draw);
  };id=requestAnimationFrame(draw);return()=>cancelAnimationFrame(id);
 },[]);
 const tokenFor=(b:Binding,c:Control)=>b.controlMap?.[c.id]??(b.systemId==='blamcon'&&['magazine','pedal'].includes(c.id)?(b.blamcon?.pedalMode??'assignable')==='assignable'?'key:82':b.blamcon?.pedalMode==='right'?'mouse:2':'mouse:3':c.token);
 const physical=(b:Binding,token:string)=>controls(b.systemId??'rs3',b.hardwarePlayer??b.player).filter(c=>!c.hardware&&tokenFor(b,c)===token).map(c=>t(c.label)).join(' / ')||token;
 const preview=(action:string,x?:number,y?:number)=>{if(state.native)return;const token='preview:'+action;const v={player:1,token,action,down:true,x,y};input.current(v);input.current({...v,down:false});};
 return <section className="shooting-range" role="dialog" aria-modal="true" aria-label={t('Schießstand')} data-frame={frame}>
  <header><div><small>DEADEYE LAB</small><h1>{t('Schießstand')}</h1><p>{t('Zielen. Schießen. Magazin nachladen. Beide Spieler gleichzeitig.')}</p></div><button className="secondary" data-test-navigation data-range-navigation onClick={close}>{t('Zurück zu meinen Guns')}</button></header>
  {state.remoteSession&&<p className="range-notice">{t('Remote Desktop: keine echte Gun-Prüfung. Direkt am angeschlossenen Bildschirm spielen.')}</p>}
  {!players.length&&<p className="range-notice">{t('Zuerst eine erkannte Gun P1 oder P2 zuweisen.')}</p>}
  <div className="range-arena" ref={surface} onContextMenu={e=>{e.preventDefault();preview('reload');}} onClick={e=>{if((e.target as HTMLElement).tagName==='CANVAS')preview('shoot',e.clientX/innerWidth,e.clientY/innerHeight);}}><canvas ref={canvas} aria-label={t('Fässer als Ziele')}/><span className="range-instruction">{t('Zwei Treffer zerstören ein Fass. 12 Schuss pro Magazin.')}</span></div>
  <div className="range-players">{players.map(b=>{const p=engine.current.players.get(b.player)!;return <article key={b.player} style={{borderColor:colors[b.player-1]}} data-player={b.player}>
   <div className="range-player-heading"><b>P{b.player}</b><strong>{b.systemId==='blamcon'?'Blamcon Vyper':b.systemId==='xgunner'?'XGunner':b.systemId==='sinden'?'Sinden':'RS3 Reaper Pro'}</strong><span data-testid={'ammo-'+b.player}>{p.ammo} / 12</span></div>
   <div className="range-gun"><GunShape model={b.systemId} pressed={new Set(controls(b.systemId??'rs3',b.hardwarePlayer??b.player).filter(c=>p.held.has(tokenFor(b,c)??'')).map(c=>c.id))}/><div><p>{t('Treffer: {0} · Schüsse: {1}',p.hits,p.shots)}</p><p>{t('Nachgeladen: {0} · Münzen: {1}',p.reloads,p.coins)}</p><p>{t('Waffe: {0}',p.weapon+1)}</p><strong className={p.ammo===0?'range-empty':''}>{p.ammo===0?t('Magazin leer. Nachladen!'):t('Bereit')}</strong></div></div>
   <div className="range-last" role="status">{p.last?physical(b,p.last):t('Warte auf Eingabe')}<small>{p.last?t(({shoot:'Schießen',reload:'Nachladen',secondary:'Zusatzaktion',auxiliary:'Dritte Aktion',mode:'Modus / Waffenwechsel',coin:'Münze',start:'Start'} as Record<string,string>)[p.lastAction]??p.lastAction):''}</small></div>
   <div className="range-tested">{Array.from(p.seen).map(token=><span key={token}>{physical(b,token)}</span>)}</div>
  </article>})}</div>
  <footer><p>{last?t('Letzte Eingabe: P{0} · {1} · {2}',last.player,last.token,last.down?t('gedrückt'):t('losgelassen')):t('Alle Tasten werden angezeigt. Start und Münze verlassen den Schießstand nicht.')}</p><p>{t('Dieser Test prüft Eingaben und Nachladen. Die Spielprofile werden beim jeweiligen Spielstart angewendet.')}</p>{!state.native&&<div><button className="secondary" onClick={()=>preview('reload')}>{t('Nachladen testen')}</button><button className="secondary" onClick={()=>preview('secondary')}>{t('Waffenwechsel testen')}</button></div>}</footer>
 </section>;
}
