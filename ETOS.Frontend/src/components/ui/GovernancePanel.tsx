import Link from "next/link";
import type { ReactNode } from "react";
import type { BadgeVariant } from "@/components/ui/Badge";
import { PillLine, SidePanel } from "@/components/ui/SidePanel";

export type GovernancePill = {
  label: string;
  value: string;
  variant?: BadgeVariant;
  href?: string;
};

export type GovernancePanelProps = {
  title?: string;
  description?: string;
  intent?: string | null;
  retrieval?: string | null;
  confidence?: string | null;
  policy?: string | null;
  traceHref?: string | null;
  contextPackageHref?: string | null;
  pills?: GovernancePill[];
  children?: ReactNode;
  className?: string;
};

/** Right-rail governance summary matching mockup side-panel + pill-line. */
export function GovernancePanel({
  title = "Answer Governance",
  description,
  intent,
  retrieval,
  confidence,
  policy,
  traceHref,
  contextPackageHref,
  pills = [],
  children,
  className = "",
}: GovernancePanelProps) {
  const derived: GovernancePill[] = [
    ...(intent ? [{ label: "Intent", value: intent, variant: "info" as const }] : []),
    ...(retrieval ? [{ label: "Retrieval", value: retrieval, variant: "teal" as const }] : []),
    ...(confidence
      ? [{ label: "Confidence", value: confidence, variant: "purple" as const }]
      : []),
    ...(policy ? [{ label: "Policy", value: policy, variant: "warning" as const }] : []),
    ...(traceHref
      ? [{ label: "AI Trace", value: "Open trace", variant: "info" as const, href: traceHref }]
      : []),
    ...(contextPackageHref
      ? [
          {
            label: "Context package",
            value: "Open package",
            variant: "neutral" as const,
            href: contextPackageHref,
          },
        ]
      : []),
    ...pills,
  ];

  return (
    <SidePanel title={title} className={className}>
      {description ? (
        <p className="mb-3 text-xs text-etos-ink-muted">{description}</p>
      ) : null}
      {derived.length > 0 ? (
        <div className="flex flex-col gap-2">
          {derived.map((pill) =>
            pill.href ? (
              <Link
                key={`${pill.label}-${pill.value}`}
                href={pill.href}
                className="block"
              >
                <PillLine
                  label={pill.label}
                  value={pill.value}
                  variant={pill.variant}
                />
              </Link>
            ) : (
              <PillLine
                key={`${pill.label}-${pill.value}`}
                label={pill.label}
                value={pill.value}
                variant={pill.variant}
              />
            ),
          )}
        </div>
      ) : (
        <p className="text-sm text-etos-ink-muted">No governance signals yet.</p>
      )}
      {children ? <div className="mt-4 space-y-3">{children}</div> : null}
    </SidePanel>
  );
}
