import {useEffect,useState} from 'react';
import {t} from './i18n';
import type {Binding,BlamconSettings} from './types';

export function BlamconPanel({binding,player,connected,busy,remote,send}:{binding?:Binding;player:number;connected:boolean;busy:boolean;remote:boolean;send:(type:string,payload?:unknown)=>void}) {
 const defaults:BlamconSettings={aspect:'16:9',recoilMode:'semi',reloadMode:'shoot',calibrationProfile:1,pedalMode:'assignable'};
 const [options,setOptions]=useState(binding?.blamcon??defaults);
 useEffect(()=>setOptions(binding?.blamcon??defaults),[binding?.physicalId,binding?.blamcon]);
 const fields:[keyof BlamconSettings,string,[string,string][]][]=[
  ['aspect','Bildformat',[['16:9','Vollbild / 16:9'],['4:3','4:3']]],
  ['recoilMode','Rückstoßmodus',[['off','Aus'],['semi','Einzelschuss'],['auto','Dauerfeuer']]],
  ['reloadMode','Offscreen-Reload',[['off','Aus'],['move','Außerhalb zielen'],['shoot','Außerhalb schießen']]],
  ['pedalMode','Magazin und Pedal',[['assignable','Eigene Taste'],['right','Wie Taste A'],['middle','Wie Taste B']]],
  ['calibrationProfile','Kalibrierungsprofil',[['1','1'],['2','2'],['3','3'],['4','4']]],
 ];
 return <section className="blamcon-panel">
  <h2>{t('Vyper konfigurieren')}</h2>
  <p>{t('Die Vyper speichert Tracking und Kalibrierung in der Gun. USB versorgt die Steuerung; der Rückstoß benötigt zusätzlich die 12-V-Stromversorgung.')}</p>
  <div className="blamcon-options">{fields.map(([key,label,choices])=><fieldset key={key}><legend>{t(label)}</legend><div>{choices.map(([value,title])=><button className={String(options[key])===value?'primary':'secondary'} aria-pressed={String(options[key])===value} disabled={busy||!connected} key={value} onClick={()=>setOptions({...options,[key]:key==='calibrationProfile'?+value:value})}>{t(title)}</button>)}</div></fieldset>)}</div>
  <button className="primary" disabled={busy||!connected} onClick={()=>send('set-blamcon',{player,options})}>{t('Einstellungen an Vyper senden')}</button>
  <p>{t('Wählt Maus/Tastatur und ein vorhandenes Kalibrierungsprofil. Danach Start, Select und Steuerkreuz einmal mit Capture erfassen. Magazin und Pedal können denselben Eingang teilen; geänderte ARC-Belegungen ebenfalls erfassen.')}</p>
  <div className="feedback-tests">{[['recoil',t('Rückstoß testen')],['rumble',t('Vibration testen')]].map(([effect,title])=><button className="secondary" key={effect} disabled={busy||!connected||remote} onClick={()=>send('test-blamcon-feedback',{player,effect})}>{title}</button>)}</div>
  <p>{t('Die Tests senden genau einen Impuls und stellen danach das automatische Feedback wieder her. Spielefeedback aus Munition oder Treffern benötigt ein passendes Output-Profil.')}</p>
  <button className="secondary" disabled={busy} onClick={()=>send('setup-gun',{system:'blamcon'})}>{t('Blamcon ARC installieren / öffnen')}</button>
  <p className="studio-caption">{t('Rumble-Stärke, Smoothing, Kameraempfindlichkeit, IR-Anordnung und Drei-Schuss-Modus lassen sich in ARC einstellen. Firmware und Kalibrierungsdaten werden nicht automatisch überschrieben.')}</p>
 </section>;
}
export function BlamconGuide({busy,send}:{busy:boolean;send:(type:string,payload?:unknown)=>void}) {
 return <section className="xgunner-guide">
  <h2>{t('Vyper einrichten und kalibrieren')}</h2>
  <ol>
   <li>{t('USB und die Rückstoß-Stromversorgung anschließen. Vier IR-Punkte sichtbar anbringen; Blamcon empfiehlt zwei oben und zwei unten.')}</li>
   <li>{t('Bei zwei Vypers in ARC unterschiedliche Hardware-Player-IDs einstellen. Beide werden zunächst als P1 ausgeliefert. Danach in Deadeye jede Gun frei P1 oder P2 zuordnen.')}</li>
   <li>{t('Maus/Tastatur-Modus wählen. Im Tastentest A, B, Magazin, Start, Select und Steuerkreuz prüfen. Unbekannte Tasten mit Capture erfassen; Start + Select dient danach als Notausstieg.')}</li>
   <li>{t('In ARC das gewünschte Kalibrierungsprofil wählen und Re-Calibrate starten. Alternativ B außerhalb des Bildschirms vier Sekunden halten und den Abzug zweimal drücken, um das Standardprofil zu kalibrieren.')}</li>
   <li>{t('Mitte, oben, unten, links und rechts anvisieren. Jeden Punkt erst bei grüner Status-LED bestätigen. Die Gun speichert die Werte selbst; Re-Adjust in ARC korrigiert anschließend einzelne Ränder.')}</li>
  </ol>
  <p>{t('Bei trägem Zielen zuerst in ARC Smoothing prüfen. Einige Spiele glätten bereits selbst. In ARC Live View müssen alle vier IR-Punkte sichtbar sein; danach den Zieltest auf dem echten Bildschirm durchführen.')}</p>
  <button className="primary" disabled={busy} onClick={()=>send('setup-gun',{system:'blamcon'})}>{t('Blamcon ARC installieren / öffnen')}</button>
 </section>;
}
