'use client';

/**
 * Clip: a few seconds of a dashboard, looping, in the same frame as a still.
 *
 * The server renders the poster, which is a real capture, so a reader with no JavaScript, with
 * motion reduced, or on a page that has not scrolled to it yet sees the photograph. The video is
 * mounted only when motion is allowed and the frame is near the viewport, plays muted and looped,
 * and fades over the poster once it is actually playing, so the frame never jumps. If the file
 * fails to load the poster simply stays.
 */
import Image from 'next/image';
import { useEffect, useRef, useState } from 'react';
import type { ClipSources } from '../lib/clips';
import frame from './Capture.module.css';
import styles from './Clip.module.css';
import { PANNABLE_ASPECT } from './frame';

export interface ClipProps {
  clip: ClipSources;
  alt: string;
  caption?: string;
  /** Above the fold: mount the video at once rather than when scrolled to. */
  priority?: boolean;
  sizes?: string;
  scale?: number;
}

export function Clip({ clip, alt, caption, priority, sizes = '(min-width: 88rem) 84rem, 100vw', scale = 1 }: ClipProps) {
  const { width, height } = clip;
  const pannable = width / height > PANNABLE_ASPECT;
  const box = useRef<HTMLDivElement>(null);
  const video = useRef<HTMLVideoElement>(null);
  const [wanted, setWanted] = useState(false);
  const [playing, setPlaying] = useState(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    const motion = window.matchMedia('(prefers-reduced-motion: reduce)');
    if (motion.matches) return;
    if (priority) {
      setWanted(true);
      return;
    }
    const el = box.current;
    if (!el) return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) {
          setWanted(true);
          observer.disconnect();
        }
      },
      { rootMargin: '200px 0px' },
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [priority]);

  // Pause when scrolled away, so a gallery page does not decode three videos at once.
  useEffect(() => {
    const el = box.current;
    const v = video.current;
    if (!el || !v || !wanted) return;
    const observer = new IntersectionObserver((entries) => {
      const visible = entries.some((e) => e.isIntersecting);
      if (visible) void v.play().catch(() => undefined);
      else v.pause();
    });
    observer.observe(el);
    return () => observer.disconnect();
  }, [wanted]);

  return (
    <figure className={frame.figure}>
      <div
        ref={box}
        className={[frame.frame, pannable ? frame.pan : ''].filter(Boolean).join(' ')}
        style={{ aspectRatio: `${width} / ${height}`, maxWidth: `${Math.round(width * scale)}px` }}
      >
        <Image src={clip.poster} alt={alt} width={width} height={height} sizes={sizes} priority={priority} className={frame.img} />
        {wanted && !failed ? (
          <video
            ref={video}
            className={`${frame.video} ${styles.video} ${playing ? styles.playing : ''}`}
            width={width}
            height={height}
            autoPlay
            muted
            loop
            playsInline
            preload="auto"
            disablePictureInPicture
            aria-hidden="true"
            onPlaying={() => setPlaying(true)}
            onError={() => setFailed(true)}
          >
            <source src={clip.webm} type="video/webm" />
            <source src={clip.mp4} type="video/mp4" />
          </video>
        ) : null}
      </div>
      {caption ? <figcaption className={frame.caption}>{caption}</figcaption> : null}
    </figure>
  );
}
