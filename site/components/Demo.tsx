'use client';

/**
 * The demo itself: a face drawn in the browser from its own `.djson` files, replaying a recorded
 * race, with the panel beside it writing what the plugin writes.
 *
 * Twenty ticks a second, each one the engine's (`lib/demo/engine.ts`) against the trace at that
 * moment (`lib/demo/frame.ts`) with the panel's properties laid over it, drawn on a canvas
 * (`lib/demo/renderer.ts`). The canvas is the face's own pixel size scaled down to fit, never up.
 */
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Engine, type Frame, type Problem } from '../lib/demo/engine';
import { Replay } from '../lib/demo/frame';
import { loadDemoFonts } from '../lib/demo/fonts';
import { parseTrace } from '../lib/demo/ncalc';
import { initialPanel, panelProperties, type PanelCatalogue, type PanelState } from '../lib/demo/panel';
import { render, type OutlineBox } from '../lib/demo/renderer';
import { parseDashboard, type SceneDashboard } from '../lib/demo/scene';
import type { DemoFace, DemoTrace } from '../lib/demo/types';
import { sizeLabel } from '../lib/packages';
import { DemoPanel } from './DemoPanel';
import styles from './Demo.module.css';

export interface DemoProps {
  faces: readonly DemoFace[];
  trace: DemoTrace;
  catalogue: PanelCatalogue;
  initial: string;
}

interface Loaded {
  slug: string;
  engine: Engine;
  images: Map<string, HTMLImageElement>;
}

const TICK_MS = 50;

const clock = (ms: number): string => {
  const s = ms / 1000;
  return `${Math.floor(s / 60)}:${(s % 60).toFixed(1).padStart(4, '0')}`;
};

async function loadFace(face: DemoFace): Promise<Loaded> {
  const library = new Map<string, SceneDashboard>();
  await Promise.all(
    face.files.map(async ({ file }) => {
      const response = await fetch(`${face.base}${encodeURIComponent(file)}`);
      if (!response.ok) throw new Error(`${file} answered ${response.status}`);
      library.set(file, parseDashboard(await response.json(), file));
    }),
  );
  const images = new Map<string, HTMLImageElement>();
  await Promise.all(
    face.images.map(
      (image) =>
        new Promise<void>((resolve) => {
          const element = new Image();
          element.onload = () => {
            images.set(`${image.dashboard}/${image.name}`, element);
            resolve();
          };
          // A picture that will not load is drawn as a labelled box by the renderer.
          element.onerror = () => resolve();
          element.src = image.src;
        }),
    ),
  );
  return { slug: face.slug, engine: new Engine(library, face.main), images };
}

export function Demo({ faces, trace, catalogue, initial }: DemoProps) {
  const [selected, setSelected] = useState(initial);
  const face = faces.find((f) => f.slug === selected) ?? faces[0]!;
  const [loaded, setLoaded] = useState<Loaded | null>(null);
  const [replay, setReplay] = useState<Replay | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [playing, setPlaying] = useState(true);
  const [shownTime, setShownTime] = useState(0);
  const [outlined, setOutlined] = useState(false);
  const [outline, setOutline] = useState<OutlineBox[]>([]);
  const [problems, setProblems] = useState<Problem[]>([]);
  const [screen, setScreen] = useState<string | null>(null);
  const [panel, setPanel] = useState<PanelState>(() => initialPanel(catalogue, face.prefix));
  const [width, setWidth] = useState(0);

  const canvas = useRef<HTMLCanvasElement>(null);
  const stage = useRef<HTMLDivElement>(null);
  const time = useRef(0);
  const dirty = useRef(true);
  const panelRef = useRef(panel);
  const playingRef = useRef(playing);
  const outlinedRef = useRef(outlined);
  panelRef.current = panel;
  playingRef.current = playing;
  outlinedRef.current = outlined;

  // The fonts and the trace, once.
  useEffect(() => {
    let live = true;
    Promise.all([loadDemoFonts(), fetch(trace.src).then((r) => (r.ok ? r.text() : Promise.reject(new Error(`the trace answered ${r.status}`))))])
      .then(([, text]) => live && setReplay(new Replay(parseTrace(text))))
      .catch((e: unknown) => live && setFailure(e instanceof Error ? e.message : String(e)));
    return () => {
      live = false;
    };
  }, [trace.src]);

  // The face's dashboards, whenever the size changes.
  useEffect(() => {
    let live = true;
    setLoaded(null);
    setProblems([]);
    loadFace(face)
      .then((l) => {
        if (!live) return;
        setLoaded(l);
        dirty.current = true;
      })
      .catch((e: unknown) => live && setFailure(e instanceof Error ? e.message : String(e)));
    return () => {
      live = false;
    };
  }, [face]);

  // A link may name a size, as the screen picker's do.
  useEffect(() => {
    const hash = window.location.hash.slice(1);
    if (faces.some((f) => f.slug === hash)) setSelected(hash);
  }, [faces]);

  // A face is configured apart from every other, as the plugin keeps one set of settings per screen.
  useEffect(() => {
    setPanel(initialPanel(catalogue, face.prefix));
  }, [catalogue, face.prefix]);

  useEffect(() => {
    dirty.current = true;
  }, [panel, outlined]);

  // The stage's width, which sets the scale.
  useEffect(() => {
    const element = stage.current;
    if (!element) return;
    const observer = new ResizeObserver(([entry]) => setWidth(entry!.contentRect.width));
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  const scale = width > 0 ? Math.min(1, width / face.width) : 0;

  const draw = useCallback(
    (frame: Frame, l: Loaded) => {
      const element = canvas.current;
      if (!element || scale <= 0) return;
      const ratio = window.devicePixelRatio || 1;
      const w = Math.round(face.width * scale * ratio);
      const h = Math.round(face.height * scale * ratio);
      if (element.width !== w || element.height !== h) {
        element.width = w;
        element.height = h;
      }
      const ctx = element.getContext('2d');
      if (!ctx) return;
      const boxes = render(ctx, frame, face.width, face.height, { scale, pixelRatio: ratio, images: l.images, outline: outlinedRef.current });
      return boxes;
    },
    [face.width, face.height, scale],
  );

  // The loop: the clock runs on animation frames, the engine ticks twenty times a second.
  useEffect(() => {
    if (!loaded || !replay) return;
    let raf = 0;
    let last = performance.now();
    let lastTick = -Infinity;
    let lastReport = -Infinity;
    const loop = (now: number) => {
      const dt = now - last;
      last = now;
      if (playingRef.current) {
        time.current += dt;
        if (time.current > replay.duration) time.current = 0;
      }
      if (dirty.current || (playingRef.current && now - lastTick >= TICK_MS)) {
        dirty.current = false;
        lastTick = now;
        const properties = replay.at(time.current);
        const overlay = panelProperties(catalogue, panelRef.current);
        const frame = loaded.engine.tick({ properties: (name) => (overlay.has(name) ? overlay.get(name) : properties[name]), now: time.current });
        const boxes = draw(frame, loaded);
        if (now - lastReport > 400 || !playingRef.current) {
          lastReport = now;
          setShownTime(time.current);
          setScreen(frame.screen);
          setProblems([...loaded.engine.problems.values()]);
          if (outlinedRef.current && boxes) setOutline(boxes);
        }
      }
      raf = requestAnimationFrame(loop);
    };
    raf = requestAnimationFrame(loop);
    return () => cancelAnimationFrame(raf);
  }, [loaded, replay, catalogue, draw]);

  const choose = (slug: string) => {
    setSelected(slug);
    window.history.replaceState(null, '', `#${slug}`);
  };

  const seek = (ms: number) => {
    time.current = ms;
    setShownTime(ms);
    dirty.current = true;
  };

  const ready = loaded !== null && loaded.slug === face.slug && replay !== null;
  const weight = useMemo(() => face.files.reduce((n, f) => n + f.bytes, 0), [face]);

  return (
    <div className={styles.demo}>
      <div role="radiogroup" aria-label="Face size" className={styles.sizes}>
        {faces.map((f) => (
          <button key={f.slug} type="button" role="radio" aria-checked={f.slug === face.slug} className={`num ${styles.size} ${f.slug === face.slug ? styles.on : ''}`} onClick={() => choose(f.slug)}>
            {sizeLabel(f)}
          </button>
        ))}
      </div>

      <div className={styles.layout}>
        <div className={styles.player}>
          <div ref={stage} className={styles.stage}>
            <div className={`${styles.canvasWrap} ${face.round ? styles.round : ''}`} style={{ width: face.width * scale, height: face.height * scale }}>
              <canvas
                ref={canvas}
                className={`${styles.canvas} ${face.round ? styles.round : ''}`}
                style={{ width: face.width * scale, height: face.height * scale }}
                role="img"
                aria-label={`The ${sizeLabel(face)} face, drawn from its own files${screen ? `, on its ${screen} screen` : ''}`}
              />
              {outlined ? (
                <div className={styles.outline} aria-hidden="true">
                  {outline.slice(0, 4000).map((b, i) => (
                    <div key={i} title={`${b.kind}: ${b.path}${b.error ? `\n${b.error}` : ''}`} className={`${styles.box} ${b.error ? styles.boxError : ''}`} style={{ left: b.x, top: b.y, width: b.width, height: b.height }} />
                  ))}
                </div>
              ) : null}
              {!ready && !failure ? <p className={styles.loading}>Loading the {sizeLabel(face)} face, {Math.round(weight / 1024)} KB</p> : null}
            </div>
          </div>

          <div className={styles.transport}>
            <button type="button" className={styles.play} onClick={() => setPlaying((p) => !p)} aria-pressed={!playing}>
              {playing ? 'Pause' : 'Play'}
            </button>
            <input
              type="range"
              className={styles.scrubber}
              min={0}
              max={replay ? Math.round(replay.duration) : 0}
              step={TICK_MS}
              value={Math.round(shownTime)}
              onChange={(e) => seek(Number(e.target.value))}
              aria-label="Time in the recorded lap"
              disabled={!replay}
            />
            <span className={`num ${styles.time}`}>
              {clock(shownTime)} / {replay ? clock(replay.duration) : '0:00.0'}
            </span>
            <label className={styles.check}>
              <input type="checkbox" checked={outlined} onChange={(e) => setOutlined(e.target.checked)} />
              Outline items
            </label>
          </div>

          <p className={styles.meta}>
            {sizeLabel(face)}, screen {screen ?? 'not chosen yet'}. Trace: {trace.scenario}, {trace.frames} frames at {trace.hz} Hz, recorded {trace.recorded}.
          </p>

          {failure ? (
            <p role="alert" className={styles.alert}>
              The demo could not start: {failure}
            </p>
          ) : null}
          {problems.length > 0 ? (
            <div role="alert" className={styles.alert}>
              <p>
                <strong>{problems.length === 1 ? 'One construct' : `${problems.length} constructs`} the demo could not compute as SimHub would.</strong> The items involved are outlined in red on the face.
              </p>
              <ul className={styles.problems}>
                {problems.slice(0, 20).map((p) => (
                  <li key={`${p.kind}|${p.message}`}>
                    <span className={styles.problemKind}>{p.kind}</span> {p.where}
                    <code className={styles.problemMessage}>{p.message.split('\n')[0]}</code>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>

        <DemoPanel catalogue={catalogue} state={panel} onChange={(f) => setPanel((s) => f(s))} slots={face.slots} />
      </div>
    </div>
  );
}
