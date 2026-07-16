import type { ReactNode } from "react";
import { Badge, type BadgeVariant } from "@/components/ui/Badge";

export type PillLineItem = {
  label: string;
  value: string;
  variant?: BadgeVariant;
};

/** Mockup `.pill-line` row for side panels. */
export function PillLine({
  label,
  value,
  variant = "neutral",
}: PillLineItem) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-xl border border-etos-border-soft bg-etos-panel-muted px-2.5 py-2 text-xs">
      <span className="font-semibold text-etos-ink-muted">{label}</span>
      <Badge variant={variant} className="normal-case tracking-normal">
        {value}
      </Badge>
    </div>
  );
}

export function PillStack({ items }: { items: PillLineItem[] }) {
  return (
    <div className="flex flex-col gap-2">
      {items.map((item) => (
        <PillLine key={`${item.label}-${item.value}`} {...item} />
      ))}
    </div>
  );
}

/** Mockup `.side-panel` / rationale rail. */
export function SidePanel({
  title,
  children,
  className = "",
}: {
  title: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <aside
      className={`rounded-etos-card border border-etos-border bg-gradient-to-b from-etos-panel to-etos-panel-muted p-4 shadow-etos ${className}`}
    >
      <h3 className="mb-3 text-base font-semibold text-etos-ink">{title}</h3>
      {children}
    </aside>
  );
}

/** Mockup `.quote` block with accent bar. */
export function Quote({ children }: { children: ReactNode }) {
  return (
    <blockquote className="border-l-[3px] border-etos-purple-border pl-3 text-[13px] leading-relaxed text-etos-ink">
      {children}
    </blockquote>
  );
}
