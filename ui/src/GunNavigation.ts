// Gun firmware need not generate keyboard autorepeat. Own repeat until release,
// focus loss or an input/session change; ignore duplicate down packets.
export class GunNavigation {
 private held=new Map<string,{timer:ReturnType<typeof setTimeout>;interval?:ReturnType<typeof setInterval>}>();
 constructor(private step:(direction:string)=>void){}
 input(id:string,direction:string,down:boolean){
  if(!down){this.release(id);return;}
  if(this.held.has(id))return;
  this.step(direction);
  const entry={timer:setTimeout(()=>{this.step(direction);entry.interval=setInterval(()=>this.step(direction),125);},400),interval:undefined as ReturnType<typeof setInterval>|undefined};
  this.held.set(id,entry);
 }
 release(id:string){const entry=this.held.get(id);if(!entry)return;clearTimeout(entry.timer);clearInterval(entry.interval);this.held.delete(id);}
 clear(){for(const id of this.held.keys())this.release(id);}
}
export function navigateGun(direction:string){
 const dialog=document.querySelector<HTMLElement>('[role="dialog"]:not([inert])');
 const scope:ParentNode=dialog??document;
 const buttons=Array.from(scope.querySelectorAll<HTMLButtonElement>('button:not(:disabled)')).filter(b=>b.getClientRects().length>0&&!b.closest('[inert],.gun-scroll')&&getComputedStyle(b).visibility!=='hidden');
 let active=document.activeElement as HTMLElement;
 if(!buttons.includes(active as HTMLButtonElement)){
  active=(scope.querySelector('.game-tile.selected')??buttons.find(b=>b.closest('.main-shell'))??buttons[0]) as HTMLElement;
  active?.focus({preventScroll:true});active?.scrollIntoView({block:'center',behavior:'instant'});return;
 }
 const rect=active.getBoundingClientRect(),ax=rect.left+rect.width/2,ay=rect.top+rect.height/2;
 const horizontal=direction==='left'||direction==='right';
 const options=buttons.filter(b=>b!==active).map(b=>{const r=b.getBoundingClientRect();return {b,dx:r.left+r.width/2-ax,dy:r.top+r.height/2-ay};})
 .filter(v=>direction==='right'?v.dx>5:direction==='left'?v.dx< -5:direction==='down'?v.dy>5:v.dy< -5);
 options.sort((a,b)=>{const score=(v:typeof a)=>horizontal?Math.abs(v.dx)+Math.abs(v.dy)*3:Math.abs(v.dy)+Math.abs(v.dx)*3;return score(a)-score(b);});
 const next=options[0]?.b;
 if(next){next.focus({preventScroll:true});const r=next.getBoundingClientRect();if(r.top<90||r.bottom>innerHeight-170)next.scrollIntoView({block:'center',behavior:'instant'});}
 else if(!horizontal)(dialog??document.scrollingElement)?.scrollBy({top:(direction==='down'?1:-1)*innerHeight*.55,behavior:'instant'});
}
