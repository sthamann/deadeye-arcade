import {devicePlayer} from './GunIdentity';
import {t} from './i18n';
import {controls,factoryMap} from './GunHardware';
import type {Binding} from './types';
export function GunMenuGuide({binding}:{binding?:Binding}){
 if(!binding)return null;
 const model=binding.systemId??'rs3',physical=controls(model,devicePlayer(binding)),map=binding.buttonMap??factoryMap(devicePlayer(binding),model);
 const names=(action:string)=>Object.keys(map).filter(token=>map[token]===action).map(token=>physical.find(c=>(binding.controlMap?.[c.id]??c.token)===token)).filter(c=>!!c).map(c=>t(c!.label));
 const directions=['up','down','left','right'].every(action=>names(action).length>0);
 return <section className="gun-menu-guide" aria-label={t('Menüsteuerung dieser Gun')}>
  <strong>{t('Menüsteuerung dieser Gun')}</strong>
  <p>{directions?t('Steuerkreuz oder Stick: durch Spiele und Menüs bewegen. Gedrückt halten zum Weiterscrollen.'):t('Zielen und abdrücken zum Auswählen. Die großen Hoch/Runter-Knöpfe scrollen Listen. Richtungstasten lassen sich unter Tasten erfassen.')}</p>
  <div><span>{t('Auswählen')}: {names('shoot').join(' / ')||t('Abzug')}</span><span>{t('Bestätigen')}: {names('start').join(' / ')||t('Eingang erfassen')}</span><span>{t('Zurück')}: {[...new Set([...names('reload'),...names('coin')])].join(' / ')||t('Eingang erfassen')}</span></div>
 </section>;
}
