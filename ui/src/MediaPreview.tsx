import { useEffect, useRef, useState } from "react";
import type { Game } from "./types";

export function MediaPreview({ game, active }: { game: Game; active: boolean }) {
  const video = useRef<HTMLVideoElement>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    const player = video.current;
    if (!player) return;
    if (active) void player.play().catch(() => {});
    else player.pause();
    return () => player.pause();
  }, [active, game.previewVideo]);
  const still = game.screenshot || game.cover;
  return <>
    {game.previewVideo && !failed ? <video ref={video} src={game.previewVideo} poster={still || undefined}
      muted loop playsInline preload="metadata" onError={() => setFailed(true)} aria-label={`Vorschau ${game.title}`} />
      : still ? <img src={still} alt="" /> : <div className="missing-media">Noch keine Vorschau vorhanden</div>}
    {game.logo && <img className="game-logo" src={game.logo} alt="" />}
    {game.previewVideo && <span className="video-caption">{failed ? "Video nicht abspielbar" : active ? "SPIELVORSCHAU · OHNE TON" : "VORSCHAU PAUSIERT"}</span>}
  </>;
}
