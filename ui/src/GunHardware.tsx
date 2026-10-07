import { t } from './i18n';
export type Control = { id:string; label:string; x:number; y:number; token?:string; hardware?:boolean };
const c=(id:string,label:string,x:number,y:number,token?:string,hardware=false):Control=>({id,label,x,y,token,hardware});
export function controls(model:string,player=1):Control[] {
 const arrows=player===1?[38,40,37,39]:[85,86,87,88];
 const pad=(x:number,y:number,keys?:number[],push?:number)=>[
  c('up','Steuerkreuz oben',x,y-16,keys&&`key:${keys[0]}`),c('down','Steuerkreuz unten',x,y+16,keys&&`key:${keys[1]}`),
  c('left','Steuerkreuz links',x-16,y,keys&&`key:${keys[2]}`),c('right','Steuerkreuz rechts',x+16,y,keys&&`key:${keys[3]}`),
  ...(push?[c('stick','Stick drücken',x,y,`key:${push}`)]:[])];
 switch(model){
 case 'rs3':return [c('trigger','Abzug',307,165,'mouse:1'),c('reload','Grifftasten links / rechts',368,169,'mouse:2'),c('magazine','Magazinboden',398,258,'mouse:3'),c('start','Start',142,154,`key:${48+player}`),c('coin','Münze · halten: Esc',179,154,`key:${52+player}`),c('side','Seitentaste M / N',353,135,`key:${player===1?77:78}`),...pad(228,136,arrows,player===1?81:83),c('selector','Rückstoß-Schalter · Rückseite',108,188,undefined,true)];
 case 'sinden':return [c('trigger','Abzug',338,172,'mouse:1'),c('pump','Pumpgriff',170,203),c('front-left','Vorne links',139,159),c('back-left','Hinten links',187,159),c('front-right','Vorne rechts · Rückseite',139,45),c('back-right','Hinten rechts · Rückseite',187,45),...pad(267,159)];
 case 'xgunner':return [c('trigger','Abzug',253,180,'mouse:1'),c('side-a','Seitentaste A',327,158),c('side-b','Seitentaste B',362,158),c('stick','Front-Stick drücken',423,142),c('up','Front-Stick oben',423,122),c('down','Front-Stick unten',423,162),c('left','Front-Stick links',403,142),c('right','Front-Stick rechts',443,142)];
 case 'blamcon':return [c('trigger','Abzug',342,157,'mouse:1'),c('magazine','Magazin ziehen',246,239),c('a','Taste A · Vordergriff',118,103),c('b','Taste B · Vordergriff',154,103),...pad(273,96,undefined),c('select','Select',249,65),c('start','Start',296,65),c('selector','Feuerwahl / Rückstoß',376,134,undefined,true)];
 default:return [];
 }
}
export function factoryMap(player:number,model='rs3'):Record<string,string>{
 if(model!=='rs3')return {'mouse:1':'shoot','mouse:2':'reload'};
 const keys=player===1?[38,40,37,39]:[85,86,87,88];
 return {'mouse:1':'shoot','mouse:2':'reload','mouse:3':'secondary',[`key:${48+player}`]:'start',[`key:${52+player}`]:'coin',[`key:${player===1?81:83}`]:'start',[`key:${player===1?77:78}`]:'secondary',...Object.fromEntries(keys.map((key,i)=>[`key:${key}`,['up','down','left','right'][i]]))};
}
export function GunShape({model='rs3',mini=false,pressed=new Set<string>(),select}:{model?:string;mini?:boolean;pressed?:Set<string>;select?:(control:string)=>void}) {
 const metal=`metal-${model}-${mini?'mini':'full'}`;
 return <svg className={'gun-shape '+model+(mini?' mini':'')} viewBox="0 0 560 290" role="img" aria-label={mini?t('Lightgun-Silhouette'):t('Modellansicht mit physischen Tasten')}>
 <defs><linearGradient id={metal} x2="0" y2="1"><stop stopColor={model==='blamcon'?'#685c9c':model==='sinden'?'#426f91':'#555d68'}/><stop offset=".5" stopColor="#263343"/><stop offset="1" stopColor="#111b28"/></linearGradient></defs>
 <g fill={`url(#${metal})`} stroke={mini?"currentColor":"#8595aa"} strokeWidth={mini?4:1.6} strokeLinejoin="round">
 {model==='rs3'?<>
 <path d="M88 97H350L374 110H419L426 124 412 144H370L420 251 401 267H348L315 187H271L260 170H103L85 151Z"/><path d="M98 94H352V126H94Z"/><path d="M97 130H250V169H106Z"/><path d="M275 148H334L346 183H275Z" fill="#090f17"/><path d="M311 149Q322 162 310 177" fill="none" strokeWidth="5"/><path d="M357 182L392 247H361L335 189Z" fill="#17202b"/><path d="M350 189L382 246M356 187L388 244M336 201L375 203M342 218L384 220M348 237L390 239" stroke="#536273"/><path d="M290 98V121M298 98V121M306 98V121M314 98V121M322 98V121M330 98V121"/><path d="M98 113H85V100H98" fill="#fa7845"/>{[120,142,164,186,208].map(x=><path key={x} d={`M${x} 128h13`} stroke="#8bc5e8" strokeWidth="3"/>)}<rect x="91" y="180" width="35" height="15" rx="6"/>
 </>:model==='sinden'?<>
 <path d="M85 96H390L410 128 394 162 441 247 392 265 342 192H305L291 175H94Z"/><path d="M89 95H396V130H89Z"/><path d="M299 151H348L369 190H307Z" fill="#080f17"/><path d="M340 155L338 181" strokeWidth="5"/><path d="M101 174H287L266 218H102Z"/><path d="M103 180V209M126 180V209M149 180V209M172 180V209M195 180V209M218 180V209M241 180V209"/><path d="M91 105H76V141H91" fill="#fa7845"/><path d="M99 98V88H108V98M122 98V88H133V98M148 98V88H160V98M175 98V88H187V98M201 98V88H213V98M229 98V88H240V98M255 98V88H266V98M283 98V88H294V98M310 98V88H322V98M338 98V88H350V98M366 98V88H378V98"/><path d="M111 121L121 141M135 121L145 141M159 121L169 141" strokeWidth="5"/>
 </>:model==='xgunner'?<>
 <path d="M79 88H464V163H315L295 189H264L284 253 270 272H210L187 198H173L175 179H81Z"/><path d="M90 94H344V124H88Z"/><path d="M84 133H182L192 119H464V175H291L277 196H207L185 175H81Z"/><path d="M222 169H265L276 190H219Z" fill="#080f17"/><path d="M249 170L256 186" strokeWidth="5"/><path d="M211 199H248L266 255H231Z" fill="#111d29"/><path d="M213 215L252 215M219 230L258 230M224 245L263 245"/><path d="M466 94H477V121H466" fill="#e5a966"/><circle cx="423" cy="142" r="26" fill="#17222f"/><path d="M288 89V101M303 89V101M318 89V101M333 89V101M348 89V101M363 89V101M378 89V101"/>
 </>:<>
 <path d="M91 80H394L409 99V136H366L401 210 376 221 339 172H303L292 149H209L196 132H93Z"/><path d="M209 133L253 137Q256 198 282 250L252 267Q220 208 210 157H109L104 132Z"/><path d="M295 140H342L358 171H298Z" fill="#080f17"/><path d="M342 144L333 164" strokeWidth="5"/><path d="M405 99H507V110H405Z"/><path d="M504 94H524V197H510L498 173Z"/><path d="M86 90H69V109H86" fill="#fb8050"/><path d="M219 83V70H283V82"/><path d="M237 69V30H282V69Z" fill="#17222f"/><path d="M241 29H279V20H246Z"/><path d="M105 88H193L180 106H101Z" fill="#17222f"/><path d="M106 117H203L210 136H108Z" fill="#111b28"/><path d="M121 80V72M137 80V72M153 80V72M169 80V72M185 80V72M201 80V72"/><path d="M221 158Q228 206 258 250M236 159Q243 206 272 247"/><path d="M111 124L119 137M132 124L140 137M153 124L161 137M174 124L182 137"/>
 </>}
 </g>
 {!mini&&<><text x="420" y="42" fill="#9db0c6" fontSize="12">{model==='rs3'?'RS3 REAPER':model==='blamcon'?'VYPER':model.toUpperCase()}</text>{controls(model).map((control,i)=><g key={control.id} className={'gun-hotspot '+(pressed.has(control.id)?'pressed':'')+(control.hardware?' hardware':'')} data-control={control.id} onClick={()=>!control.hardware&&select?.(control.id)}><title>{t(control.label)}</title><circle cx={control.x} cy={control.y} r={control.id==='trigger'?13:control.hardware?7:9}/><text x={control.x} y={control.y+3} textAnchor="middle" fontSize="9" fill="#dbe8f6" pointerEvents="none">{i+1}</text></g>)}</>}
 </svg>;
}
