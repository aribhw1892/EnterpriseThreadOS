import type { ReactNode } from "react";

/** Import-hub gold title row: 30px bold + muted subtitle + action cluster. */
export function PageHeader({
  title,
  description,
  actions,
  eyebrow,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
  /** Optional; prefer omit for mockup parity (mockups have no eyebrow). */
  eyebrow?: string;
}) {
  return (
    <div className="mb-[18px] flex flex-wrap items-start justify-between gap-4">
      <div>
        {eyebrow ? (
          <p className="mb-1 text-xs font-semibold uppercase tracking-[0.25em] text-etos-accent-cyan">
            {eyebrow}
          </p>
        ) : null}
        <h1 className="text-[30px] font-bold tracking-tight text-etos-ink">
          {title}
        </h1>
        {description ? (
          <p className="mt-2 max-w-[900px] text-sm text-etos-ink-muted">
            {description}
          </p>
        ) : null}
      </div>
      {actions ? (
        <div className="flex flex-wrap items-center gap-2.5">{actions}</div>
      ) : null}
    </div>
  );
}
