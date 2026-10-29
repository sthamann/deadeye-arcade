// The range consumes the same per-device action stream as the front end.
// Coordinates are normalized to the playfield, never the shared desktop cursor.
export type RangeInput={player:number;token:string;down:boolean;action:string;x?:number;y?:number};
export type RangePlayer={ammo:number;shots:number;hits:number;reloads:number;coins:number;weapon:number;last:string;lastAction:string;held:Set<string>;seen:Set<string>;x:number;y:number};
export class RangeGame {
 players=new Map<number,RangePlayer>();
 barrels=Array.from({length:9},(_,i)=>({x:.16+(i%3)*.33,y:.23+Math.floor(i/3)*.25,hp:2,revive:0,owner:0}));
 sparks:{x:number;y:number;at:number;player:number;hit:boolean}[]=[];
 constructor(players:number[]){for(const p of players)this.players.set(p,{ammo:12,shots:0,hits:0,reloads:0,coins:0,weapon:0,last:'',lastAction:'',held:new Set(),seen:new Set(),x:.5,y:.5});}
 input(v:RangeInput,now:number){
  const p=this.players.get(v.player);if(!p)return;
  if(typeof v.x==='number'&&typeof v.y==='number'){p.x=v.x;p.y=v.y;}
  if(!v.down){p.held.delete(v.token);return;}
  // Ignore keyboard auto-repeat, and keep each player's held state separate.
  if(p.held.has(v.token))return;
  p.held.add(v.token);p.seen.add(v.token);p.last=v.token;p.lastAction=v.action;
  if(v.action==='reload'){p.ammo=12;p.reloads++;return;}
  if(v.action==='coin'){p.coins++;return;}
  if(v.action==='secondary'||v.action==='auxiliary'||v.action==='mode'){p.weapon=(p.weapon+1)%3;return;}
  if(v.action!=='shoot'||p.ammo===0)return;
  p.ammo--;p.shots++;
  const target=this.barrels.find(b=>b.hp>0&&Math.abs(b.x-p.x)<.065&&Math.abs(b.y-p.y)<.09);
  if(target){target.hp--;target.owner=v.player;p.hits++;if(!target.hp)target.revive=now+1800;}
  this.sparks.push({x:p.x,y:p.y,at:now,player:v.player,hit:!!target});
 }
 tick(now:number){for(const b of this.barrels)if(b.hp===0&&now>=b.revive)b.hp=2;this.sparks=this.sparks.filter(s=>now-s.at<550);}
 clearHeld(){for(const p of this.players.values())p.held.clear();}
}
