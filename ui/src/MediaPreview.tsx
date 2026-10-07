import { t } from "./i18n";
import { useEffect, useRef, useState } from "react";
import type { Game } from "./types";
export function MediaPreview({
  game,
  active,
}: {
  game: Game;
  active: boolean;
}) {
  const video = useRef<HTMLVideoElement>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [game.id, game.previewVideo]);
  useEffect(() => {
    const player = video.current;
    if (!player) return;
    if (active) void player.play().catch(() => {});
    else player.pause();
    return () => player.pause();
  }, [active, game.previewVideo, failed]);
  const still = game.screenshot || game.cover;
  return (
    <>
      {game.previewVideo && !failed ? (
        <video
          ref={video}
          src={game.previewVideo}
          poster={still || undefined}
          muted
          loop
          playsInline
          preload="metadata"
          onError={() => setFailed(true)}
          aria-label={t("Vorschau {0}", game.title)}
        />
      ) : still ? (
        <img src={still} alt="" />
      ) : (
        <div className="missing-media">
          {t("Noch keine Vorschau vorhanden")}
        </div>
      )}
      {game.logo && <img className="game-logo" src={game.logo} alt="" />}
      {game.previewVideo && (
        <span className="video-caption">
          {failed
            ? t("Video nicht abspielbar")
            : active
              ? t("SPIELVORSCHAU \u00B7 OHNE TON")
              : t("VORSCHAU PAUSIERT")}
        </span>
      )}
    </>
  );
}
