import Link from "next/link";
import {
  ActionButton,
  DataQualityPanel,
} from "@/components/imports/ImportHubShared";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { PillStack } from "@/components/ui/SidePanel";
import {
  createManualDataQualityIssue,
  createSecurityEventDataQualityIssue,
  generateDataQualityIssues,
} from "@/app/(shell)/imports/actions";
import { getImportLists } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function ImportDataQualityPage() {
  const lists = await getImportLists();
  const issues = lists.dataQualityIssues.data ?? [];
  const critical = issues.filter(
    (i) => i.severity.toLowerCase() === "critical",
  ).length;
  const high = issues.filter((i) => i.severity.toLowerCase() === "high").length;
  const avgPenalty =
    issues.length === 0
      ? 0
      : Math.round(
          issues.reduce((sum, i) => sum + (i.trustImpactPenalty ?? 0), 0) /
            issues.length,
        );
  const selected = issues[0];

  return (
    <main className="px-6 py-8 lg:px-8">
      <div className="mb-[18px] flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-[30px] font-bold tracking-tight text-etos-ink">
            Data quality issue triage
          </h1>
          <p className="mt-2 max-w-[900px] text-sm text-etos-ink-muted">
            Issue artifacts created from validation, identity conflicts,
            document extraction failures, and security events.
          </p>
        </div>
        <div className="flex flex-wrap gap-2.5">
          <ActionButton action={createManualDataQualityIssue}>
            Create manual issue
          </ActionButton>
          <form action={createSecurityEventDataQualityIssue}>
            <Button type="submit" variant="ghost">
              Create from security event
            </Button>
          </form>
        </div>
      </div>

      {lists.dataQualityIssues.error ? (
        <ErrorState error={lists.dataQualityIssues.error} />
      ) : null}

      <div className="grid gap-4 md:grid-cols-4">
        <KpiCard
          label="Confirmed issues"
          value={issues.length}
          hint="Linked to import batches"
        />
        <KpiCard
          label="Critical"
          value={critical}
          trend={critical > 0 ? "bad" : "flat"}
          trendLabel={critical > 0 ? String(critical) : undefined}
          hint="Blocks graph promotion"
        />
        <KpiCard
          label="High"
          value={high}
          trend={high > 0 ? "warn" : "flat"}
          trendLabel={high > 0 ? String(high) : undefined}
          hint="Require override or review"
        />
        <KpiCard
          label="Trust penalty avg"
          value={avgPenalty === 0 ? "0" : `-${Math.abs(avgPenalty)}`}
          trend={avgPenalty !== 0 ? "bad" : "flat"}
          trendLabel={avgPenalty !== 0 ? String(-Math.abs(avgPenalty)) : undefined}
          hint="Applied to affected evidence"
        />
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Issue queue</CardTitle>
          </CardHeader>
          <CardContent>
            <DataTable
              rows={issues}
              rowKey={(row) => row.id}
              emptyMessage="No data quality issues yet."
              columns={[
                {
                  key: "issue",
                  header: "Issue",
                  render: (row) => (
                    <div>
                      <p>
                        {row.issueCode} {row.title}
                      </p>
                    </div>
                  ),
                },
                {
                  key: "object",
                  header: "Affected object",
                  render: (row) => row.affectedEntityType,
                },
                {
                  key: "severity",
                  header: "Severity",
                  render: (row) => <StatusBadge status={row.severity} />,
                },
                {
                  key: "status",
                  header: "Status",
                  render: (row) => <StatusBadge status={row.status} />,
                },
              ]}
            />
            <div className="mt-4">
              <ActionButton action={generateDataQualityIssues}>
                Generate quality issues
              </ActionButton>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Selected issue detail</CardTitle>
          </CardHeader>
          <CardContent>
            {selected ? (
              <>
                <div className="flex items-center gap-3 rounded-[14px] border border-etos-border-soft bg-etos-panel-muted p-3">
                  <div className="grid h-[42px] w-[42px] place-items-center rounded-[14px] bg-gradient-to-br from-orange-500 to-red-500 text-lg font-black text-white">
                    !
                  </div>
                  <div>
                    <p className="text-sm font-extrabold text-etos-ink">
                      {selected.severity} — {selected.title}
                    </p>
                    <p className="text-xs text-etos-ink-muted">
                      {selected.evidenceSummary ||
                        selected.rationale ||
                        "Cannot enter trusted graph until resolved."}
                    </p>
                  </div>
                </div>
                <div className="my-3.5 h-px bg-etos-border" />
                <PillStack
                  items={[
                    {
                      label: "Creates review task",
                      value: selected.reviewTaskReady ? "Yes" : "No",
                      variant: selected.reviewTaskReady ? "success" : "neutral",
                    },
                    {
                      label: "Trust penalty",
                      value: String(selected.trustImpactPenalty),
                      variant: "danger",
                    },
                    {
                      label: "Excluded from trusted recommendations",
                      value: selected.excludedFromTrustedRecommendations
                        ? "Yes"
                        : "No",
                      variant: selected.excludedFromTrustedRecommendations
                        ? "danger"
                        : "success",
                    },
                  ]}
                />
                <Badge variant="info" className="mt-3">
                  Priority: {selected.reviewPriority}
                </Badge>
              </>
            ) : (
              <p className="text-sm text-etos-ink-muted">
                No issue selected. Generate or create an issue to inspect detail.
              </p>
            )}
            <div className="mt-4">
              <Link
                href="/imports"
                className="text-sm font-extrabold text-etos-accent-cyan underline-offset-2 hover:underline"
              >
                ← Back to import hub
              </Link>
            </div>
          </CardContent>
        </Card>
      </div>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4">
        <summary className="cursor-pointer text-sm font-extrabold text-etos-ink">
          Advanced / Debug
        </summary>
        <div className="mt-4">
          <DataQualityPanel
            issues={lists.dataQualityIssues}
            monitoringPlaceholders={lists.monitoringPlaceholders}
          />
        </div>
      </details>
    </main>
  );
}
