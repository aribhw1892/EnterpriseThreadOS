"use client";

import type { DigitalThreadMinimap } from "@/lib/etos-api";
import { buildMinimapViewport } from "@/lib/digital-thread-map";

type Props = {
  minimap: DigitalThreadMinimap | null;
  view: { x: number; y: number; scale: number; width: number; height: number };
};

export function DigitalThreadMinimap({ minimap, view }: Props) {
  const projection = buildMinimapViewport(minimap, view);

  return (
    <div className="rounded-etos-card border border-etos-ops-border bg-etos-ops-panel p-2">
      <p className="mb-1 text-[10px] font-bold uppercase tracking-wide text-etos-ops-ink-muted">
        Minimap
      </p>
      <svg viewBox="0 0 1000 420" className="h-24 w-full" aria-label="Thread minimap">
        {projection.points.map((point, index) => (
          <circle
            key={`${point.x}-${point.y}-${index}`}
            cx={point.x}
            cy={point.y}
            r={3}
            fill="#38bdf8"
            opacity={0.7}
          />
        ))}
        {projection.systems.map((system) => (
          <circle
            key={system.systemId}
            cx={system.x}
            cy={system.y}
            r={4}
            fill={
              system.connectionStatus.toLowerCase() === "healthy"
                ? "#34d399"
                : system.connectionStatus.toLowerCase() === "down"
                  ? "#f87171"
                  : "#fbbf24"
            }
          />
        ))}
        <rect
          x={projection.viewport.x}
          y={projection.viewport.y}
          width={Math.max(40, projection.viewport.width)}
          height={Math.max(24, projection.viewport.height)}
          fill="none"
          stroke="#e2e8f0"
          strokeWidth={2}
          opacity={0.8}
        />
      </svg>
    </div>
  );
}
