'use client';

/**
 * The demo itself: a face, a companion or a pit wall drawn in the browser from its own `.djson`
 * files, replaying a recorded scenario, with the panel beside it writing what the plugin writes.
 *
 * Twenty ticks a second, each one the engine's (`lib/demo/engine.ts`) against the trace at that
 * moment (`lib/demo/frame.ts`) with the panel's properties laid over it, drawn on a canvas
 * (`lib/demo/renderer.ts`). The canvas is the screen's own pixel size scaled down to fit the column
 * and the window, never up.
 */
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Engine, type Frame, type Problem } from '../lib/demo/engine';
import { Replay } from '../lib/demo/frame';
import { loadDemoFonts } from '../lib/demo/fonts';
import { parseTrace } from '../lib/demo/ncalc';
import { forcePending, initialPanel, openCompanion, panelProperties, type PanelCatalogue, type PanelState } from '../lib/demo/panel';
import { render, type OutlineBox } from '../lib/demo/renderer';
import { parseDashboard, type SceneDashboard } from '../lib/demo/scene';
import type { DemoFace, DemoGroup, DemoTrace } from '../lib/demo/types';
import { sizeLabel } from '../lib/packages';
import { DemoPanel } from './DemoPanel';
import styles from './Demo.module.css';

export interface DemoProps {
  faces: readonly DemoFace[];
  /** Every trace the page offers, the one it opens on first. */
  traces: readonly DemoTrace[];
  catalogue: PanelCatalogue;
  initial: string;
}

interface Loaded {
  slug: string;
  engine: Engine;
  images: Map<string, HTMLImageElement>;
}

const TICK_MS = 50;

const GROUP_LABELS: Record<DemoGroup, string> = { face: 'Faces', theme: 'Theme', companion: 'Companion', pitwall: 'Pit wall' };

const clock = (ms: number): string => {
  const s = ms / 1000;
  return `${Math.floor(s / 60)}:${(s % 60).toFixed(1).padStart(4, '0')}`;
};

/** The panel a screen opens with: the plugin's fresh install, with a companion's start module forced. */
function panelFor(catalogue: PanelCatalogue, face: DemoFace): PanelState {
  if (face.group === 'companion') return openCompanion(catalogue, initialPanel(catalogue, null, { screen: 'companion' }), performance.now());
  if (face.group === 'pitwall') return initialPanel(catalogue, null, { screen: 'pitwall' });
  return initialPanel(catalogue, face.prefix, { theme: face.theme });
}

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

export function Demo({ faces, traces, catalogue, initial }: DemoProps) {
  const [selected, setSelected] = useState(initial);
  const face = faces.find((f) => f.slug === selected) ?? faces[0]!;
  const [scenario, setScenario] = useState(traces[0]!.scenario);
  const trace = traces.find((t) => t.scenario === scenario) ?? traces[0]!;
  const [loaded, setLoaded] = useState<Loaded | null>(null);
  const [replay, setReplay] = useState<Replay | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [playing, setPlaying] = useState(true);
  const [shownTime, setShownTime] = useState(0);
  const [outlined, setOutlined] = useState(false);
  const [outline, setOutline] = useState<OutlineBox[]>([]);
  const [problems, setProblems] = useState<Problem[]>([]);
  const [screen, setScreen] = useState<string | null>(null);
  const [panel, setPanel] = useState<PanelState>(() => panelFor(catalogue, face));
  const [width, setWidth] = useState(0);
  const [viewport, setViewport] = useState(0);

  const canvas = useRef<HTMLCanvasElement>(null);
  const stage = useRef<HTMLDivElement>(null);
  const time = useRef(0);
  const dirty = useRef(true);
  const panelRef = useRef(panel);
  const playingRef = useRef(playing);
  const outlinedRef = useRef(outlined);
  const replays = useRef(new Map<string, Replay>());
  panelRef.current = panel;
  playingRef.current = playing;
  outlinedRef.current = outlined;

  // The fonts once, and the trace whenever the scenario changes. The clock carries on where it was:
  // every trace is twenty seconds, and the engine treats a clock that went back as a rewind anyway.
  useEffect(() => {
    let live = true;
    const cached = replays.current.get(trace.src);
    const load = cached
      ? Promise.resolve(cached)
      : fetch(trace.src)
          .then((r) => (r.ok ? r.text() : Promise.reject(new Error(`the ${trace.scenario} trace answered ${r.status}`))))
          .then((text) => {
            const r = new Replay(parseTrace(text));
            replays.current.set(trace.src, r);
            return r;
          });
    Promise.all([loadDemoFonts(), load])
      .then(([, r]) => {
        if (!live) return;
        setReplay(r);
        dirty.current = true;
      })
      .catch((e: unknown) => live && setFailure(e instanceof Error ? e.message : String(e)));
    return () => {
      live = false;
    };
  }, [trace.src, trace.scenario]);

  // The screen's dashboards, whenever the choice changes.
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

  // A link may name a screen, as the screen picker's do.
  useEffect(() => {
    const hash = window.location.hash.slice(1);
    if (faces.some((f) => f.slug === hash)) setSelected(hash);
  }, [faces]);

  // A screen is configured apart from every other, as the plugin keeps one set of settings per screen.
  useEffect(() => {
    setPanel(panelFor(catalogue, face));
  }, [catalogue, face]);

  useEffect(() => {
    dirty.current = true;
  }, [panel, outlined]);

  // The stage's width and the window's height, which set the scale.
  useEffect(() => {
    const element = stage.current;
    if (!element) return;
    const observer = new ResizeObserver(([entry]) => setWidth(entry!.contentRect.width));
    observer.observe(element);
    const resize = () => setViewport(window.innerHeight);
    resize();
    window.addEventListener('resize', resize);
    return () => {
      observer.disconnect();
      window.removeEventListener('resize', resize);
    };
  }, []);

  // A portrait pit wall is 1920 pixels tall, so a screen fits the window's height as well as the column.
  const tallest = Math.max(320, viewport * 0.8);
  const scale = width > 0 ? Math.min(1, width / face.width, viewport > 0 ? tallest / face.height : 1) : 0;

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
      // Paused, a companion's force still runs out on the wall clock, and the screen has to follow it.
      const ticking = playingRef.current || forcePending(panelRef.current, now);
      if (dirty.current || (ticking && now - lastTick >= TICK_MS)) {
        dirty.current = false;
        lastTick = now;
        const properties = replay.at(time.current);
        const overlay = panelProperties(catalogue, panelRef.current, now);
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

  /** SimHub's NextScreen and PreviousScreen, which page a companion; a tap on either half does the same. */
  const navigate = (direction: 1 | -1) => {
    if (loaded && loaded.slug === face.slug && loaded.engine.navigate(direction)) dirty.current = true;
  };

  const ready = loaded !== null && loaded.slug === face.slug && replay !== null;
  const weight = useMemo(() => face.files.reduce((n, f) => n + f.bytes, 0), [face]);
  const groups = useMemo(() => {
    const out: { key: string; label: string; faces: DemoFace[] }[] = [];
    for (const f of faces) {
      const key = `${f.group}-${f.theme}`;
      let group = out.find((g) => g.key === key);
      if (!group) {
        const label = f.group === 'theme' ? (catalogue.themes.find((t) => t.id === f.theme)?.name ?? f.theme) : GROUP_LABELS[f.group];
        group = { key, label, faces: [] };
        out.push(group);
      }
      group.faces.push(f);
    }
    return out;
  }, [faces, catalogue]);
  const tappable = face.group === 'companion';
  const what = face.group === 'companion' ? 'companion' : face.group === 'pitwall' ? 'pit wall' : 'face';

  return (
    <div className={styles.demo}>
      <div role="radiogroup" aria-label="Screen" className={styles.sizes}>
        {groups.map((g) => (
          <div key={g.key} className={styles.sizeGroup}>
            <span className={styles.sizeGroupLabel} id={`demo-group-${g.key}`}>
              {g.label}
            </span>
            <div className={styles.sizeRow} role="group" aria-labelledby={`demo-group-${g.key}`}>
              {g.faces.map((f) => (
                <button
                  key={f.slug}
                  type="button"
                  role="radio"
                  aria-checked={f.slug === face.slug}
                  aria-label={`${g.label}, ${sizeLabel(f)}`}
                  className={`num ${styles.size} ${f.slug === face.slug ? styles.on : ''}`}
                  onClick={() => choose(f.slug)}
                >
                  {sizeLabel(f)}
                </button>
              ))}
            </div>
          </div>
        ))}
      </div>

      <div className={styles.layout}>
        <div className={styles.player}>
          <div ref={stage} className={styles.stage}>
            <div className={`${styles.canvasWrap} ${face.round ? styles.round : ''}`} style={{ width: face.width * scale, height: face.height * scale }}>
              <canvas
                ref={canvas}
                className={`${styles.canvas} ${face.round ? styles.round : ''} ${tappable ? styles.tappable : ''}`}
                style={{ width: face.width * scale, height: face.height * scale }}
                role="img"
                aria-label={`The ${sizeLabel(face)} ${what}, drawn from its own files${screen ? `, on its ${screen} screen` : ''}`}
                onClick={
                  tappable
                    ? (e) => {
                        const box = e.currentTarget.getBoundingClientRect();
                        navigate(e.clientX - box.left < box.width / 2 ? -1 : 1);
                      }
                    : undefined
                }
              />
              {outlined ? (
                <div className={styles.outline} aria-hidden="true">
                  {outline.slice(0, 4000).map((b, i) => (
                    <div key={i} title={`${b.kind}: ${b.path}${b.error ? `\n${b.error}` : ''}`} className={`${styles.box} ${b.error ? styles.boxError : ''}`} style={{ left: b.x, top: b.y, width: b.width, height: b.height }} />
                  ))}
                </div>
              ) : null}
              {!ready && !failure ? (
                <p className={styles.loading}>
                  Loading the {sizeLabel(face)} {what}, {Math.round(weight / 1024)} KB
                </p>
              ) : null}
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
              aria-label="Time in the recorded scenario"
              disabled={!replay}
            />
            <span className={`num ${styles.time}`}>
              {clock(shownTime)} / {replay ? clock(replay.duration) : '0:00.0'}
            </span>
            <label className={styles.scenario}>
              Scenario
              <select className={styles.select} value={trace.scenario} onChange={(e) => setScenario(e.target.value)}>
                {traces.map((t) => (
                  <option key={t.scenario} value={t.scenario}>
                    {t.scenario}
                  </option>
                ))}
              </select>
            </label>
            <label className={styles.check}>
              <input type="checkbox" checked={outlined} onChange={(e) => setOutlined(e.target.checked)} />
              Outline items
            </label>
          </div>

          <p className={styles.meta}>
            {face.folder}, {sizeLabel(face)}, screen {screen ?? 'not chosen yet'}. Trace: {trace.scenario}, {trace.frames} frames at {trace.hz} Hz, recorded {trace.recorded}.
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

        <DemoPanel catalogue={catalogue} state={panel} onChange={(f) => setPanel((s) => f(s))} slots={face.slots} landscape={face.width > face.height} onNavigate={navigate} />
      </div>
    </div>
  );
}
