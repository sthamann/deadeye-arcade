import { t } from './i18n';
import {useId} from 'react';
import artwork from '../../src/Reaper.Windows/GunArtwork.json';
export type Control = { id:string; label:string; x:number; y:number; token?:string; hardware?:boolean };
const c=(id:string,label:string,x:number,y:number,token?:string,hardware=false):Control=>({id,label,x,y,token,hardware});
export function controls(model:string,player=1):Control[] {
 const arrows=player===1?[38,40,37,39]:[85,86,87,88];
 const pad=(x:number,y:number,keys?:number[],push?:number)=>[
  c('up','Steuerkreuz oben',x,y-16,keys&&`key:${keys[0]}`),c('down','Steuerkreuz unten',x,y+16,keys&&`key:${keys[1]}`),
  c('left','Steuerkreuz links',x-16,y,keys&&`key:${keys[2]}`),c('right','Steuerkreuz rechts',x+16,y,keys&&`key:${keys[3]}`),
  ...(push?[c('stick','Stick drücken',x,y,`key:${push}`)]:[])];
 switch(model){
 case 'rs3':return [c('trigger','Abzug',307,165,'mouse:1'),c('reload','Grifftasten links / rechts',368,169,'mouse:2'),c('magazine','Magazinboden',398,258,'mouse:3'),c('start','Start',142,154,`key:${48+player}`),c('coin','Münze · halten: Esc',179,154,`key:${52+player}`),c('side','Seitentaste M / N',353,135,`key:${player===1?77:78}`),...pad(228,136,arrows,player===1?81:83),c('selector','Rückstoß-Schalter · Rückseite',108,45,undefined,true)];
 case 'sinden':return [c('trigger','Abzug',338,172,'mouse:1'),c('pump','Pumpgriff',170,203),c('front-left','Vorne links',139,159),c('back-left','Hinten links',187,159),c('front-right','Vorne rechts · Rückseite',139,45),c('back-right','Hinten rechts · Rückseite',187,45),...pad(267,159)];
 case 'xgunner':return [c('trigger','Abzug',330,180,'mouse:1'),c('reload','Rechte Maustaste',368,164,'mouse:2'),c('magazine','Mittlere Maustaste',350,135,'mouse:3'),c('stick','Enter · Rückseite',150,65,'key:13'),c('start','Start',235,155,'key:49'),c('coin','Münztaste',200,155,'key:53'),c('side','Leertaste',280,164,'key:32'),c('side-a','Taste Q · Rückseite',200,65,'key:81'),c('side-b','Taste E · Rückseite',235,65,'key:69'),c('up','Steuerkreuz oben',150,124,'key:87'),c('down','Steuerkreuz unten',150,156,'key:83'),c('left','Steuerkreuz links',134,140,'key:65'),c('right','Steuerkreuz rechts',166,140,'key:68'),c('rapid-fire','Rapid Fire · Schalter',400,93,undefined,true),c('vibration','Vibration · Schalter',285,65,undefined,true),c('upgrade','Firmware-Taste · Rückseite',335,65,undefined,true),c('power','Ein / Aus · Magazin',405,265,undefined,true),c('magazine-release','Magazinentriegelung',375,220,undefined,true)];
 case 'blamcon':return [c('trigger','Abzug',342,157,'mouse:1'),c('magazine','Magazin ziehen',246,239),c('a','Taste A · Vordergriff',118,103,'mouse:2'),c('b','Taste B · Vordergriff',154,103,'mouse:3'),...pad(273,96,undefined),c('select','Select',249,65),c('start','Start',296,65),c('aux','Feuerwahl als Taste',376,134),c('pedal','Optionales Pedal',480,249)];
 default:return [];
 }
}
export function factoryMap(player:number,model='rs3'):Record<string,string>{
 if(model==='blamcon')return {'mouse:1':'shoot','key:82':'reload','mouse:2':'secondary','mouse:3':'auxiliary'};
 if(model==='xgunner')return {'mouse:1':'shoot','mouse:2':'reload','mouse:3':'secondary','key:49':'start','key:13':'start','key:53':'coin','key:32':'reload','key:81':'secondary','key:69':'secondary','key:87':'up','key:83':'down','key:65':'left','key:68':'right'};
 if(model!=='rs3')return {'mouse:1':'shoot','mouse:2':'reload'};
 const keys=player===1?[38,40,37,39]:[85,86,87,88];
 return {'mouse:1':'shoot','mouse:2':'reload','mouse:3':'secondary',[`key:${48+player}`]:'start',[`key:${52+player}`]:'coin',[`key:${player===1?81:83}`]:'start',[`key:${player===1?77:78}`]:'secondary',...Object.fromEntries(keys.map((key,i)=>[`key:${key}`,['up','down','left','right'][i]]))};
}
export function GunShape({model='rs3',mini=false,pressed=new Set<string>(),select}:{model?:string;mini?:boolean;pressed?:Set<string>;select?:(control:string)=>void}) {
 const metal=useId().replaceAll(':','')+'-metal';
 const paths=(artwork as Record<string,{d:string;fill:string;stroke:string;width:string}[]>)[model]??[];
 return <svg className={'gun-shape '+model+(mini?' mini':'')} viewBox="0 0 560 290" role="img" aria-label={mini?t('Lightgun-Silhouette'):t('Modellansicht mit physischen Tasten')}>
 <defs><linearGradient id={metal} x2="0" y2="1"><stop stopColor={model==='blamcon'?'#685c9c':model==='sinden'?'#426f91':'#555d68'}/><stop offset=".5" stopColor="#263343"/><stop offset="1" stopColor="#111b28"/></linearGradient></defs>
 <g fill={`url(#${metal})`} stroke={mini?"currentColor":"#8595aa"} strokeWidth={mini?4:1.6} strokeLinejoin="round">
 {paths.map((path,i)=><path key={i} d={path.d} fill={path.fill||undefined} stroke={path.stroke||undefined} strokeWidth={mini?4:+path.width}/>)}
 </g>
 {!mini&&<>{['rs3','sinden','xgunner'].includes(model)&&<text x="88" y="20" fill="#9db0c6" fontSize="12">{t('Obere Markierungen: Gegenseite')}</text>}<text x="420" y="42" fill="#9db0c6" fontSize="12">{model==='rs3'?'RS3 REAPER':model==='blamcon'?'VYPER':model.toUpperCase()}</text>{controls(model).map((control,i)=><g key={control.id} className={'gun-hotspot '+(pressed.has(control.id)?'pressed':'')+(control.hardware?' hardware':'')} data-control={control.id} data-test-navigation={select&&!control.hardware?true:undefined} role={!control.hardware&&select?'button':undefined} tabIndex={!control.hardware&&select?0:undefined} aria-label={t(control.label)} onKeyDown={e=>{if(!control.hardware&&select&&(e.key==='Enter'||e.key===' ')){e.preventDefault();select(control.id);}}} onClick={()=>!control.hardware&&select?.(control.id)}><title>{t(control.label)}</title><circle cx={control.x} cy={control.y} r={control.id==='trigger'?13:control.hardware?7:9}/><text x={control.x} y={control.y+3} textAnchor="middle" fontSize="9" fill="#dbe8f6" pointerEvents="none">{i+1}</text></g>)}</>}
 </svg>;
}
