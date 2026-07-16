import Link from "next/link";
import type { ReactNode } from "react";
import { Badge, badgeVariantForStatus } from "@/components/ui/Badge";

export type TraceTimelineStep = {
  id: string;
  title: string;
  description?: string | null;
  status?: string | null;
  meta?: string | null;
  href?: string | null;
};

export function TraceTimeline({
  steps,
  emptyMessage = "No timeline steps recorded.",
  className = "",
}: {
  steps: TraceTimelineStep[];
  emptyMessage?: string;
  className?: string;
}) {
  if (steps.length === 0) {
    return (
      <div className={`rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted ${className}`}>
        {emptyMessage}
      </div>
    );
  }

  return (
    <ol className={`relative space-y-0 ${className}`}>
      {steps.map((step, index) => {
        const isLast = index === steps.length - 1;
        const body: ReactNode = (
          <>
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div>
                <p className="font-semibold text-etos-ink">{step.title}</p>
                {step.description ? (
                  <p className="mt-1 text-sm text-etos-ink-muted">{step.description}</p>
                ) : null}
                {step.meta ? (
                  <p className="mt-2 text-xs text-etos-ink-subtle">{step.meta}</p>
                ) : null}
              </div>
              {step.status ? (
                <Badge variant={badgeVariantForStatus(step.status)}>{step.status}</Badge>
              ) : null}
            </div>
          </>
        );

        return (
          <li key={step.id} className="relative flex gap-4 pb-6 last:pb-0">
            <div className="flex flex-col items-center">
              <span className="mt-1 h-3 w-3 shrink-0 rounded-full border-2 border-etos-accent bg-etos-panel" />
              {!isLast ? (
                <span className="mt-1 w-px flex-1 bg-etos-border" aria-hidden />
              ) : null}
            </div>
            <div className="min-w-0 flex-1 rounded-etos-card border border-etos-border-soft bg-etos-panel p-4">
              {step.href ? (
                <Link href={step.href} className="block transition hover:border-etos-accent">
                  {body}
                </Link>
              ) : (
                body
              )}
            </div>
          </li>
        );
      })}
    </ol>
  );
}
