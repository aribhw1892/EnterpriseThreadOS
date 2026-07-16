import type { ReactNode } from "react";

export type TimelineItem = {
  title: string;
  description?: string;
};

/** Vertical timeline matching mockup `.timeline` / `.timeline-item`. */
export function Timeline({ items }: { items: TimelineItem[] }) {
  return (
    <ol className="relative space-y-3.5 pl-5 before:absolute before:bottom-1 before:left-[7px] before:top-1 before:w-0.5 before:bg-etos-info-border">
      {items.map((item) => (
        <li key={item.title} className="relative">
          <span
            aria-hidden
            className="absolute -left-[18px] top-1 h-2.5 w-2.5 rounded-full border-[3px] border-etos-info-border bg-etos-accent"
          />
          <p className="text-sm font-extrabold text-etos-ink">{item.title}</p>
          {item.description ? (
            <p className="mt-0.5 text-xs text-etos-ink-muted">{item.description}</p>
          ) : null}
        </li>
      ))}
    </ol>
  );
}

export function TimelineCard({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <div className="rounded-etos-card border border-etos-border-panel bg-etos-panel-elevated p-[18px] shadow-etos">
      <h2 className="mb-3 text-lg font-semibold text-etos-ink">{title}</h2>
      {children}
    </div>
  );
}
