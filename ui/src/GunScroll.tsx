import {useEffect,useState} from 'react';
import {ArrowUp,ArrowDown,ChevronsUp} from 'lucide-react';
import {t} from './i18n';
export function GunScroll({hidden=false}:{hidden?:boolean}) {
 const [position,setPosition]=useState({top:true,bottom:true});
 useEffect(()=>{
  const update=()=>setPosition({top:window.scrollY<10,bottom:window.scrollY+window.innerHeight>=document.documentElement.scrollHeight-10});
  const observer=new ResizeObserver(update); observer.observe(document.body);
  window.addEventListener('scroll',update,{passive:true});window.addEventListener('resize',update);update();
  return ()=>{observer.disconnect();window.removeEventListener('scroll',update);window.removeEventListener('resize',update);};
 },[]);
 if(hidden||position.top&&position.bottom)return null;
 const scroll=(direction:number)=>window.scrollBy({top:direction*window.innerHeight*.7,behavior:'smooth'});
 return <div className="gun-scroll" aria-label={t('Mit der Gun scrollen')}>
  <button aria-label={t('Ganz nach oben')} disabled={position.top} onClick={()=>window.scrollTo({top:0,behavior:'smooth'})}><ChevronsUp size={24}/></button>
  <button aria-label={t('Eine Seite nach oben')} disabled={position.top} onClick={()=>scroll(-1)}><ArrowUp size={24}/><span>{t('Hoch')}</span></button>
  <button aria-label={t('Eine Seite nach unten')} disabled={position.bottom} onClick={()=>scroll(1)}><ArrowDown size={24}/><span>{t('Runter')}</span></button>
  <small>{t('Zielen + abdrücken')}</small>
 </div>;
}
