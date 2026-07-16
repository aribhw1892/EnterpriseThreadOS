"use client";

type Props = {
  windowHours: number;
  scrubRatio: number;
  live: boolean;
  onScrubRatioChange: (ratio: number) => void;
  onLiveChange: (live: boolean) => void;
};

export function DigitalThreadScrubber({
  windowHours,
  scrubRatio,
  live,
  onScrubRatioChange,
  onLiveChange,
}: Props) {
  const percent = Math.round(scrubRatio * 100);

  return (
    <div className="rounded-etos-card border border-etos-ops-border bg-etos-ops-panel p-3">
      <div className="mb-2 flex items-center justify-between gap-2">
        <p className="text-[10px] font-bold uppercase tracking-wide text-etos-ops-ink-muted">
          Time scrubber · last {windowHours}h
        </p>
        <button
          type="button"
          onClick={() => onLiveChange(!live)}
          className={`inline-flex items-center gap-2 rounded-full border px-3 py-1 text-[10px] font-bold uppercase tracking-wide ${
            live
              ? "border-emerald-400/40 text-emerald-300"
              : "border-etos-ops-border text-etos-ops-ink-muted"
          }`}
        >
          <span
            aria-hidden
            className={`h-2 w-2 rounded-full ${live ? "bg-emerald-400" : "bg-slate-500"}`}
          />
          {live ? "Live" : "Paused"}
        </button>
      </div>
      <div className="flex items-center gap-3">
        <input
          type="range"
          min={0}
          max={100}
          value={percent}
          aria-label="Scrub digital thread window"
          className="w-full accent-sky-400"
          onChange={(event) => {
            onLiveChange(false);
            onScrubRatioChange(Number(event.target.value) / 100);
          }}
        />
        <span className="whitespace-nowrap rounded-full border border-etos-ops-border px-2 py-1 text-[10px] font-bold uppercase tracking-wide text-etos-ops-ink-muted">
          {percent}%
        </span>
      </div>
    </div>
  );
}
