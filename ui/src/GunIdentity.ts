import type {Binding} from './types';

// The USB/firmware player is independent of the chosen game slot.
export function devicePlayer(binding:Binding|undefined, fallback=1):number {
 if(!binding)return fallback;
 if(binding.hardwarePlayer!=null)return binding.hardwarePlayer;
 if((binding.systemId??'rs3')==='rs3') {
  const receiver=binding.mouseId.match(/PID_575([0-3])/i);
  if(receiver)return Number(receiver[1])+1;
 }
 return binding.player;
}
