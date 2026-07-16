"use client";

import { useId } from "react";
import type { CanvasScene } from "@/lib/digital-thread-map";
import { zoomBandLabel } from "@/lib/digital-thread-map";

type Props = {
  scene: CanvasScene;
  zoomPercent: number;
  pan: { x: number; y: number };
  selectedEventId: string | null;
  onSelectEvent: (eventId: string) => void;
  onPanChange: (pan: { x: number; y: number }) => void;
  onZoomChange: (zoomPercent: number) => void;
};

/** Deterministic sparkle positions along the core for fiber-thread density. */
function fiberSparkles(
  width: number,
  height: number,
  nowX: number,
  nowY: number,
  count = 48,
): Array<{ x: number; y: number; r: number; opacity: number }> {
  const mid = height / 2;
  const items: Array<{ x: number; y: number; r: number; opacity: number }> = [];
  for (let i = 0; i < count; i += 1) {
    const t = i / (count - 1);
    const x = 56 + t * (nowX - 72);
    const wave = Math.sin(t * Math.PI * 2.2) * 18 * (1 - t * 0.35);
    const jitter = Math.sin(i * 12.9898) * 5.5;
    items.push({
      x,
      y: mid + wave + jitter + (nowY - mid) * t * 0.15,
      r: 0.6 + (i % 3) * 0.45,
      opacity: 0.25 + (i % 5) * 0.12,
    });
  }
  return items;
}

export function DigitalThreadCanvas({
  scene,
  zoomPercent,
  pan,
  selectedEventId,
  onSelectEvent,
  onPanChange,
  onZoomChange,
}: Props) {
  const reactId = useId().replace(/:/g, "");
  const bloomWideId = `dt-bloom-wide-${reactId}`;
  const bloomMidId = `dt-bloom-mid-${reactId}`;
  const bloomTightId = `dt-bloom-tight-${reactId}`;
  const coreGradId = `dt-core-${reactId}`;
  const haloGradId = `dt-halo-${reactId}`;
  const nowGradId = `dt-now-${reactId}`;
  const nowHaloId = `dt-now-halo-${reactId}`;
  const scale = Math.max(0.05, Math.min(6, zoomPercent / 100));
  const showSystems = zoomPercent >= 25;
  const showPulseLabels = zoomPercent >= 200;
  const macro = zoomPercent < 25;
  const sparkles = fiberSparkles(scene.width, scene.height, scene.now.x, scene.now.y);

  return (
    <div className="digital-thread-canvas relative min-h-[460px] overflow-hidden rounded-etos-card border border-etos-ops-border">
      <div className="digital-thread-canvas-stars pointer-events-none absolute inset-0" />
      <div className="digital-thread-canvas-nebula pointer-events-none absolute inset-0" />

      <div className="pointer-events-none absolute left-3 top-3 z-10 rounded-full border border-etos-ops-border bg-etos-ops-panel/90 px-3 py-1 text-[10px] font-bold uppercase tracking-wide text-etos-ops-ink-muted">
        {zoomBandLabel(zoomPercent)} · {Math.round(zoomPercent)}%
      </div>
      <div className="absolute right-3 top-3 z-10 flex gap-1">
        <button
          type="button"
          className="rounded-md border border-etos-ops-border bg-etos-ops-panel px-2 py-1 text-xs text-etos-ops-ink"
          onClick={() => onZoomChange(Math.min(600, zoomPercent + 25))}
        >
          +
        </button>
        <button
          type="button"
          className="rounded-md border border-etos-ops-border bg-etos-ops-panel px-2 py-1 text-xs text-etos-ops-ink"
          onClick={() => onZoomChange(Math.max(5, zoomPercent - 25))}
        >
          −
        </button>
        <button
          type="button"
          className="rounded-md border border-etos-ops-border bg-etos-ops-panel px-2 py-1 text-xs text-etos-ops-ink"
          onClick={() => {
            onZoomChange(100);
            onPanChange({ x: 0, y: 0 });
          }}
        >
          Fit
        </button>
      </div>

      <svg
        className="relative z-[1] h-[520px] w-full touch-none"
        viewBox={`0 0 ${scene.width} ${scene.height}`}
        role="img"
        aria-label="Digital thread timeline canvas — past left, now right"
        onWheel={(event) => {
          event.preventDefault();
          const delta = event.deltaY > 0 ? -10 : 10;
          onZoomChange(Math.max(5, Math.min(600, zoomPercent + delta)));
        }}
        onMouseDown={(event) => {
          const originX = event.clientX;
          const originY = event.clientY;
          const startPan = pan;
          const onMove = (moveEvent: MouseEvent) => {
            onPanChange({
              x: startPan.x + (moveEvent.clientX - originX),
              y: startPan.y + (moveEvent.clientY - originY),
            });
          };
          const onUp = () => {
            window.removeEventListener("mousemove", onMove);
            window.removeEventListener("mouseup", onUp);
          };
          window.addEventListener("mousemove", onMove);
          window.addEventListener("mouseup", onUp);
        }}
      >
        <defs>
          {/* Wide atmospheric bloom */}
          <filter id={bloomWideId} x="-80%" y="-200%" width="260%" height="500%">
            <feGaussianBlur stdDeviation="14" result="blur" />
            <feColorMatrix
              in="blur"
              type="matrix"
              values="0 0 0 0 0.05
                      0 0 0 0 0.55
                      0 0 0 0 1
                      0 0 0 0.85 0"
              result="glow"
            />
            <feMerge>
              <feMergeNode in="glow" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>

          {/* Mid neon bloom */}
          <filter id={bloomMidId} x="-60%" y="-160%" width="220%" height="420%">
            <feGaussianBlur stdDeviation="7" result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>

          {/* Tight hot core */}
          <filter id={bloomTightId} x="-40%" y="-100%" width="180%" height="300%">
            <feGaussianBlur stdDeviation="2.2" result="blur" />
            <feMerge>
              <feMergeNode in="blur" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>

          <linearGradient id={haloGradId} x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stopColor="#0ea5e9" stopOpacity="0.15" />
            <stop offset="40%" stopColor="#38bdf8" stopOpacity="0.45" />
            <stop offset="75%" stopColor="#22d3ee" stopOpacity="0.55" />
            <stop offset="100%" stopColor="#7dd3fc" stopOpacity="0.7" />
          </linearGradient>

          <linearGradient id={coreGradId} x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stopColor="#0284c7" />
            <stop offset="25%" stopColor="#0ea5e9" />
            <stop offset="50%" stopColor="#38bdf8" />
            <stop offset="75%" stopColor="#22d3ee" />
            <stop offset="100%" stopColor="#e0f2fe" />
          </linearGradient>

          <radialGradient id={nowGradId} cx="50%" cy="50%" r="50%">
            <stop offset="0%" stopColor="#ffffff" />
            <stop offset="25%" stopColor="#e0f2fe" />
            <stop offset="55%" stopColor="#38bdf8" />
            <stop offset="80%" stopColor="#0284c7" />
            <stop offset="100%" stopColor="#0369a1" stopOpacity="0" />
          </radialGradient>

          <radialGradient id={nowHaloId} cx="50%" cy="50%" r="50%">
            <stop offset="0%" stopColor="#7dd3fc" stopOpacity="0.9" />
            <stop offset="45%" stopColor="#0ea5e9" stopOpacity="0.35" />
            <stop offset="100%" stopColor="#0284c7" stopOpacity="0" />
          </radialGradient>
        </defs>

        <g transform={`translate(${pan.x} ${pan.y}) scale(${scale})`}>
          <text
            x={56}
            y={28}
            fill="rgba(148, 163, 184, 0.55)"
            fontSize={11}
            fontWeight={700}
            letterSpacing="0.14em"
          >
            PAST
          </text>
          <text
            x={scene.now.x}
            y={28}
            textAnchor="middle"
            fill="rgba(186, 230, 253, 0.75)"
            fontSize={11}
            fontWeight={700}
            letterSpacing="0.14em"
          >
            NOW
          </text>

          {/* Layer 1 — huge soft halo */}
          <path
            d={scene.corePath}
            fill="none"
            stroke={`url(#${haloGradId})`}
            strokeWidth={macro ? 52 : 42}
            strokeLinecap="round"
            filter={`url(#${bloomWideId})`}
            opacity={0.95}
          />

          {/* Layer 2 — mid bloom tube */}
          <path
            d={scene.corePath}
            fill="none"
            stroke="#38bdf8"
            strokeWidth={macro ? 22 : 16}
            strokeLinecap="round"
            filter={`url(#${bloomMidId})`}
            opacity={0.55}
          />

          {/* Branch strands with colored bloom (under core hot line) */}
          {scene.branches.map((branch) =>
            branch.d ? (
              <g key={branch.id}>
                <path
                  d={branch.d}
                  fill="none"
                  stroke={branch.color}
                  strokeWidth={macro ? 4 : 5.5}
                  strokeOpacity={0.28}
                  strokeLinecap="round"
                  filter={`url(#${bloomMidId})`}
                />
                <path
                  d={branch.d}
                  fill="none"
                  stroke={branch.color}
                  strokeWidth={macro ? 1.4 : 1.8}
                  strokeOpacity={0.9}
                  strokeLinecap="round"
                  filter={`url(#${bloomTightId})`}
                >
                  <title>
                    {branch.id} · {branch.eventCount} events · {branch.health}
                  </title>
                </path>
              </g>
            ) : null,
          )}

          {/* Layer 3 — bright core */}
          <path
            d={scene.corePath}
            fill="none"
            stroke={`url(#${coreGradId})`}
            strokeWidth={macro ? 7 : 5.5}
            strokeLinecap="round"
            filter={`url(#${bloomTightId})`}
          />

          {/* Layer 4 — white-hot filament */}
          <path
            d={scene.corePath}
            fill="none"
            stroke="#f0f9ff"
            strokeWidth={macro ? 2.2 : 1.7}
            strokeLinecap="round"
            opacity={0.95}
          />

          {/* Layer 5 — flow dash */}
          <path
            d={scene.corePath}
            fill="none"
            stroke="#ffffff"
            strokeWidth={1}
            strokeDasharray="5 14"
            strokeLinecap="round"
            opacity={0.75}
          />

          {/* Fiber sparkles along the string */}
          {sparkles.map((dot, index) => (
            <circle
              key={`spark-${index}`}
              cx={dot.x}
              cy={dot.y}
              r={dot.r}
              fill="#e0f2fe"
              opacity={dot.opacity}
              filter={`url(#${bloomTightId})`}
            />
          ))}

          {/* NOW orb — heavy bloom */}
          <g transform={`translate(${scene.now.x} ${scene.now.y})`}>
            <circle r={54} fill={`url(#${nowHaloId})`} filter={`url(#${bloomWideId})`} />
            <circle r={32} fill={`url(#${nowGradId})`} filter={`url(#${bloomMidId})`} opacity={0.85} />
            <circle r={18} fill={`url(#${nowGradId})`} filter={`url(#${bloomTightId})`} />
            <circle r={7} fill="#ffffff" opacity={0.98} />
            <text
              y={52}
              textAnchor="middle"
              fill="#e0f2fe"
              fontSize={11}
              fontWeight={800}
              letterSpacing="0.16em"
            >
              LIVE
            </text>
          </g>

          {/* Enterprise system endpoints */}
          {showSystems
            ? scene.systemNodes.map((node) => (
                <g key={node.id} transform={`translate(${node.x} ${node.y})`}>
                  <circle
                    r={16}
                    fill={node.color}
                    opacity={0.22}
                    filter={`url(#${bloomMidId})`}
                  />
                  <circle
                    r={11}
                    fill={node.color}
                    opacity={0.95}
                    stroke="rgba(255,255,255,0.55)"
                    strokeWidth={1.5}
                    filter={`url(#${bloomTightId})`}
                  />
                  <circle r={4} fill="#ffffff" opacity={0.95} />
                  <text
                    x={18}
                    y={4}
                    fill="#e2e8f0"
                    fontSize={11}
                    fontWeight={600}
                  >
                    {node.label}
                  </text>
                  <title>
                    {node.label} · {node.status}
                  </title>
                </g>
              ))
            : null}

          {/* Event pulses */}
          {scene.pulses.map((pulse) => (
            <g
              key={pulse.id}
              transform={`translate(${pulse.x} ${pulse.y})`}
              className="cursor-pointer"
              onClick={(event) => {
                event.stopPropagation();
                onSelectEvent(pulse.id);
              }}
            >
              {pulse.kind === "overlay" ? (
                <rect
                  x={-5}
                  y={-5}
                  width={10}
                  height={10}
                  rx={2}
                  fill={pulse.color}
                  opacity={selectedEventId === pulse.id ? 1 : 0.9}
                  filter={`url(#${bloomTightId})`}
                  className="animate-pulse"
                />
              ) : (
                <>
                  <circle
                    r={selectedEventId === pulse.id ? 10 : 7}
                    fill={pulse.color}
                    opacity={0.3}
                    filter={`url(#${bloomMidId})`}
                  />
                  <circle
                    r={selectedEventId === pulse.id ? 6 : 4}
                    fill={pulse.color}
                    filter={`url(#${bloomTightId})`}
                    className="animate-pulse"
                  />
                </>
              )}
              {showPulseLabels ? (
                <text y={-12} textAnchor="middle" fontSize={8} fill="#e2e8f0">
                  {pulse.title.slice(0, 24)}
                </text>
              ) : null}
              <title>{`${pulse.title} · ${pulse.eventType} · ${pulse.trustState}`}</title>
            </g>
          ))}
        </g>
      </svg>
    </div>
  );
}
