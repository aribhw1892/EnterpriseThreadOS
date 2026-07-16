import Link from "next/link";
import type { ReactNode } from "react";
import { StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { PageHeader } from "@/components/ui/PageHeader";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";

export type DefinitionListRow = {
  id: string;
  name: string;
  secondary: string;
  versionLabel?: string | null;
  readinessState?: string | null;
  dependencyHint?: string | null;
};

/**
 * Mockup-parity Layer 3–6 library: title+actions, 4 KPIs, registry table + definition preview rail.
 */
export function DefinitionLibraryPage({
  title,
  description,
  hrefBase,
  rows,
  error,
  emptyMessage,
  primaryActionLabel = "New definition",
  secondaryAction,
  columnLabels,
  previewTitle = "Definition preview",
  previewPills,
  showKpis = true,
  registryTitle = "Registry",
  footer,
  sideExtra,
}: {
  title: string;
  description: string;
  hrefBase: string;
  rows: DefinitionListRow[];
  error?: string | null;
  emptyMessage: string;
  primaryActionLabel?: string;
  secondaryAction?: ReactNode;
  columnLabels?: {
    name?: string;
    secondary?: string;
    deps?: string;
  };
  previewTitle?: string;
  previewPills?: {
    label: string;
    value: string;
    variant?: "info" | "purple" | "warning" | "teal" | "neutral" | "success";
  }[];
  showKpis?: boolean;
  registryTitle?: string;
  /** Optional full-width block under the registry split (e.g. optimization contract). */
  footer?: ReactNode;
  /** Extra content below preview pills (e.g. policy composition notice). */
  sideExtra?: ReactNode;
}) {
  const published = rows.filter((r) =>
    String(r.readinessState ?? "")
      .toLowerCase()
      .includes("publish"),
  ).length;
  const drafts = rows.filter((r) =>
    ["draft", "ready"].includes(String(r.readinessState ?? "").toLowerCase()),
  ).length;
  const blocked = rows.filter((r) =>
    ["blocked", "failed", "conflicted"].includes(
      String(r.readinessState ?? "").toLowerCase(),
    ),
  ).length;
  const selected = rows[0];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title={title}
        description={description}
        actions={
          <>
            <Link href={selected ? `${hrefBase}/${selected.id}` : hrefBase}>
              <Button variant="primary">{primaryActionLabel}</Button>
            </Link>
            {secondaryAction ?? (
              <Link href="/model-artifacts">
                <Button variant="ghost">Model packages</Button>
              </Link>
            )}
          </>
        }
      />

      {error ? (
        <div className="mb-4">
          <ErrorState error={error} />
        </div>
      ) : null}

      {showKpis ? (
        <div className="grid gap-4 md:grid-cols-4">
          <KpiCard
            label="Published"
            value={published}
            hint="Ready for governed use"
          />
          <KpiCard label="Drafts / ready" value={drafts} hint="Awaiting publish" />
          <KpiCard
            label="Total definitions"
            value={rows.length}
            hint="Active tenant registry"
          />
          <KpiCard
            label="Blocked"
            value={blocked}
            trend={blocked > 0 ? "warn" : "flat"}
            trendLabel={blocked > 0 ? "!" : undefined}
            hint="Missing dependencies"
          />
        </div>
      ) : null}

      <div className={`grid gap-4 lg:grid-cols-3 ${showKpis ? "mt-4" : ""}`}>
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>{registryTitle}</CardTitle>
          </CardHeader>
          <CardContent>
            {rows.length === 0 ? (
              <EmptyState message={emptyMessage} />
            ) : (
              <DataTable<DefinitionListRow>
                rows={rows}
                rowKey={(row) => row.id}
                emptyMessage={emptyMessage}
                columns={[
                  {
                    key: "name",
                    header: columnLabels?.name ?? "Name",
                    render: (row) => (
                      <Link
                        href={`${hrefBase}/${row.id}`}
                        className="font-extrabold text-etos-accent hover:underline"
                      >
                        {row.name}
                      </Link>
                    ),
                  },
                  {
                    key: "secondary",
                    header: columnLabels?.secondary ?? "Outcome / key",
                    render: (row) => (
                      <span className="text-etos-ink-muted">{row.secondary}</span>
                    ),
                  },
                  {
                    key: "deps",
                    header: columnLabels?.deps ?? "Compatible package",
                    render: (row) => (
                      <span className="text-etos-ink-subtle">
                        {row.dependencyHint ?? "—"}
                      </span>
                    ),
                  },
                  {
                    key: "state",
                    header: "State",
                    render: (row) =>
                      row.readinessState ? (
                        <StatusBadge status={row.readinessState} />
                      ) : (
                        "Unknown"
                      ),
                  },
                ]}
              />
            )}
          </CardContent>
        </Card>

        <SidePanel title={previewTitle}>
          {selected ? (
            <>
              <p className="mb-3 text-sm font-extrabold text-etos-ink">
                {selected.name}
              </p>
              <p className="mb-3 text-xs text-etos-ink-muted">
                {selected.secondary}
              </p>
              <PillStack
                items={
                  previewPills ?? [
                    {
                      label: "Version",
                      value: selected.versionLabel ?? "No version",
                      variant: "info" as const,
                    },
                    {
                      label: "State",
                      value: selected.readinessState ?? "Unknown",
                      variant: "neutral" as const,
                    },
                    {
                      label: "Dependencies",
                      value: selected.dependencyHint ?? "—",
                      variant: "teal" as const,
                    },
                  ]
                }
              />
              {sideExtra}
              <div className="mt-4">
                <Link href={`${hrefBase}/${selected.id}`}>
                  <Button variant="ghost">Open detail</Button>
                </Link>
              </div>
            </>
          ) : (
            <p className="text-sm text-etos-ink-muted">{emptyMessage}</p>
          )}
        </SidePanel>
      </div>

      {footer}
    </main>
  );
}
