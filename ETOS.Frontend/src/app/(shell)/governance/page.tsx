import Link from "next/link";
import {
  GovernanceTrendCharts,
  type GovernanceTrendSeries,
} from "@/components/governance/GovernanceTrendCharts";
import { Badge, badgeVariantForStatus } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable, type DataTableColumn } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { Notice } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import {
  getConnectorDefinitionArtifacts,
  getGovernanceDashboard,
  getGovernanceKpiTrends,
  getGovernanceLists,
  type AuditRecord,
  type GovernanceKpiValue,
  type HighRiskRecommendationSummary,
  type SecurityEvent,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

const TREND_SUPPORTED_KEYS = [
  "open_reviews",
  "blocked_decisions",
  "decision_throughput",
  "outcome_verification_rate",
  "learning_signal_rate",
] as const;

const TREND_TITLES: Record<(typeof TREND_SUPPORTED_KEYS)[number], string> = {
  open_reviews: "Open reviews",
  blocked_decisions: "Blocked decisions",
  decision_throughput: "Decision throughput",
  outcome_verification_rate: "Outcome verification",
  learning_signal_rate: "Learning signals",
};

const AUDIT_DESIGN_CHECKS = [
  { label: "Tenant filtering", value: "Enforced", variant: "success" as const },
  { label: "Classification filtering", value: "Before LLM", variant: "success" as const },
  { label: "Artifact immutability", value: "Published versions", variant: "success" as const },
  { label: "Execution records", value: "Safe summaries", variant: "info" as const },
  { label: "Raw secrets", value: "Never stored", variant: "danger" as const },
];

type GovernanceEventRow = {
  id: string;
  kind: "audit" | "security" | "recommendation";
  event: string;
  actor: string;
  scope: string;
  result: string;
  resultVariant: ReturnType<typeof badgeVariantForStatus>;
  href?: string | null;
};

function kpiDisplayValue(kpi: GovernanceKpiValue) {
  if (kpi.status === "deferred") {
    return "Deferred";
  }
  return kpi.formattedValue ?? kpi.value ?? "—";
}

function buildEventRows(
  auditRecords: AuditRecord[],
  securityEvents: SecurityEvent[],
  highRisk: HighRiskRecommendationSummary[],
): GovernanceEventRow[] {
  const auditRows: GovernanceEventRow[] = auditRecords.map((record) => ({
    id: `audit-${record.id}`,
    kind: "audit",
    event: record.action,
    actor: record.userId ?? "System",
    scope: record.sourceObjectType
      ? `${record.sourceObjectType}${record.sourceObjectId ? ` ${record.sourceObjectId.slice(0, 8)}` : ""}`
      : record.policyName ?? "Audit",
    result: record.result,
    resultVariant: badgeVariantForStatus(record.result),
    href: null,
  }));

  const securityRows: GovernanceEventRow[] = securityEvents.map((event) => ({
    id: `security-${event.id}`,
    kind: "security",
    event: event.eventType,
    actor: event.userId ?? "Unknown",
    scope: event.sourceAction || event.safeSummary.slice(0, 48) || "Security",
    result: event.severity,
    resultVariant: badgeVariantForStatus(event.severity),
    href: event.reviewTaskReady ? "/tasks" : null,
  }));

  const recRows: GovernanceEventRow[] = highRisk.map((item) => ({
    id: `rec-${item.artifactId}`,
    kind: "recommendation",
    event: item.title,
    actor: "Recommendation",
    scope: item.lifecycleStatus,
    result: item.riskState,
    resultVariant: badgeVariantForStatus(item.riskState),
    href: item.contextViewRoute,
  }));

  return [...securityRows, ...auditRows, ...recRows].slice(0, 12);
}

const eventColumns: DataTableColumn<GovernanceEventRow>[] = [
  {
    key: "event",
    header: "Event",
    render: (row) =>
      row.href ? (
        <Link href={row.href} className="text-etos-accent hover:underline">
          {row.event}
        </Link>
      ) : (
        row.event
      ),
  },
  {
    key: "actor",
    header: "Actor",
    render: (row) => <span className="font-normal text-etos-ink-muted">{row.actor}</span>,
  },
  {
    key: "scope",
    header: "Scope",
    render: (row) => <span className="font-normal text-etos-ink-muted">{row.scope}</span>,
  },
  {
    key: "result",
    header: "Result",
    render: (row) => (
      <Badge variant={row.resultVariant} className="normal-case tracking-normal">
        {row.result}
      </Badge>
    ),
  },
];

export default async function GovernanceDashboardPage() {
  const [dashboard, governance, connectors, ...trendResults] = await Promise.all([
    getGovernanceDashboard(14),
    getGovernanceLists(),
    getConnectorDefinitionArtifacts(),
    ...TREND_SUPPORTED_KEYS.map((kpiKey) => getGovernanceKpiTrends(kpiKey, 14)),
  ]);

  const kpis = dashboard.data?.kpis ?? [];
  const highRisk = dashboard.data?.highRiskRecommendations ?? [];
  const graphSupplements = dashboard.data?.graphSupplements;
  const auditList = governance.auditRecords.data ?? [];
  const securityList = governance.securityEvents.data ?? [];
  const connectorList = connectors.data ?? [];

  const writeDisabledCount = connectorList.filter((c) => c.executionEnabled === false).length;
  const eventRows = buildEventRows(auditList, securityList, highRisk);

  const trendSeries: GovernanceTrendSeries[] = TREND_SUPPORTED_KEYS.map((kpiKey, index) => {
    const result = trendResults[index];
    const fromDashboard = kpis.find((kpi) => kpi.kpiKey === kpiKey);
    return {
      kpiKey,
      title: fromDashboard?.title ?? TREND_TITLES[kpiKey],
      points: result?.data?.points ?? [],
      error: result?.error ?? null,
    };
  });

  const visibleKpis = kpis.filter((kpi) => kpi.kpiKey !== "tenant_custom_kpi" || kpi.status === "deferred");

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Governance & audit dashboard"
        description="Cross-cutting enterprise dashboard for approvals, audit, security events, runtime records, and read-only boundary verification."
        actions={
          <>
            <Button
              type="button"
              disabled
              title="No export audit summary endpoint in MVP — use Advanced JSON or AI Trace explorer."
            >
              Export audit summary
            </Button>
            <Link
              href="#security-events"
              className="inline-flex items-center gap-2 rounded-etos-button border border-etos-border bg-etos-panel-muted px-3.5 py-2.5 text-[13px] font-extrabold text-etos-ink hover:bg-etos-panel"
            >
              View security events
            </Link>
          </>
        }
      />

      {dashboard.error ? (
        <div className="mb-4">
          <ErrorState error={dashboard.error} />
        </div>
      ) : null}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-5">
        {visibleKpis.map((kpi) => (
          <KpiCard
            key={kpi.kpiKey}
            label={kpi.title}
            value={kpiDisplayValue(kpi)}
            hint={kpi.status === "deferred" ? "Deferred platform KPI" : kpi.source}
          />
        ))}
        <KpiCard
          label="Write actions"
          value={0}
          trend="up"
          trendLabel="SAFE"
          hint="MVP boundary — source writes disabled"
        />
      </div>

      <Notice variant="info" className="mt-4">
        Trace exports: use{" "}
        <Link href="/ai-traces" className="font-semibold underline">
          AI Trace explorer
        </Link>{" "}
        — no live export-count KPI in Issue 21.
      </Notice>

      <div className="mt-6 grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Governance events</CardTitle>
          </CardHeader>
          <CardContent>
            {governance.auditRecords.error || governance.securityEvents.error ? (
              <ErrorState
                error={
                  governance.auditRecords.error ??
                  governance.securityEvents.error ??
                  "Failed to load governance events."
                }
              />
            ) : eventRows.length === 0 ? (
              <EmptyState message="No recent audit, security, or high-risk recommendation events." />
            ) : (
              <DataTable
                columns={eventColumns}
                rows={eventRows}
                rowKey={(row) => row.id}
                emptyMessage="No governance events."
              />
            )}
          </CardContent>
        </Card>

        <div className="flex flex-col gap-4">
          <SidePanel title="Audit design checks">
            <PillStack items={AUDIT_DESIGN_CHECKS} />
          </SidePanel>
          <SidePanel title="Read-only boundary">
            <PillStack
              items={[
                { label: "Source writes", value: "0 SAFE", variant: "success" },
                {
                  label: "Write-disabled connectors",
                  value: connectors.error
                    ? "Unavailable"
                    : `${writeDisabledCount} / ${connectorList.length}`,
                  variant: writeDisabledCount > 0 || connectorList.length === 0 ? "success" : "warning",
                },
                {
                  label: "Execution enabled",
                  value: connectors.error
                    ? "—"
                    : String(connectorList.filter((c) => c.executionEnabled === true).length),
                  variant: "neutral",
                },
              ]}
            />
            {connectors.error ? (
              <Notice variant="warning" className="mt-3">
                Connector flags unavailable: {connectors.error}
              </Notice>
            ) : null}
          </SidePanel>
        </div>
      </div>

      <Card className="mt-6">
        <CardHeader>
          <CardTitle>Trends (14 days)</CardTitle>
        </CardHeader>
        <CardContent>
          <GovernanceTrendCharts series={trendSeries} />
        </CardContent>
      </Card>

      <div id="security-events" className="mt-6 grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Recent security events</CardTitle>
          </CardHeader>
          <CardContent>
            {governance.securityEvents.error ? (
              <ErrorState error={governance.securityEvents.error} />
            ) : securityList.length === 0 ? (
              <EmptyState message="No recent security events." />
            ) : (
              <DataTable
                columns={[
                  {
                    key: "event",
                    header: "Event",
                    render: (row: SecurityEvent) => row.eventType,
                  },
                  {
                    key: "severity",
                    header: "Severity",
                    render: (row: SecurityEvent) => (
                      <Badge variant={badgeVariantForStatus(row.severity)} className="normal-case tracking-normal">
                        {row.severity}
                      </Badge>
                    ),
                  },
                  {
                    key: "summary",
                    header: "Summary",
                    render: (row: SecurityEvent) => (
                      <span className="font-normal text-etos-ink-muted">{row.safeSummary}</span>
                    ),
                  },
                  {
                    key: "drill",
                    header: "Drill",
                    render: (row: SecurityEvent) =>
                      row.reviewTaskReady ? (
                        <Link href="/tasks" className="text-etos-accent hover:underline">
                          Review tasks
                        </Link>
                      ) : (
                        <span className="font-normal text-etos-ink-subtle">—</span>
                      ),
                  },
                ]}
                rows={securityList}
                rowKey={(row) => row.id}
              />
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>High-risk recommendations</CardTitle>
          </CardHeader>
          <CardContent>
            {highRisk.length === 0 ? (
              <EmptyState message="No actionable high-risk recommendations." />
            ) : (
              <DataTable
                columns={[
                  {
                    key: "title",
                    header: "Recommendation",
                    render: (row: HighRiskRecommendationSummary) => (
                      <Link href={row.contextViewRoute} className="text-etos-accent hover:underline">
                        {row.title}
                      </Link>
                    ),
                  },
                  {
                    key: "risk",
                    header: "Risk",
                    render: (row: HighRiskRecommendationSummary) => (
                      <Badge variant={badgeVariantForStatus(row.riskState)} className="normal-case tracking-normal">
                        {row.riskState}
                      </Badge>
                    ),
                  },
                  {
                    key: "status",
                    header: "Status",
                    render: (row: HighRiskRecommendationSummary) => (
                      <span className="font-normal text-etos-ink-muted">{row.lifecycleStatus}</span>
                    ),
                  },
                ]}
                rows={highRisk}
                rowKey={(row) => row.artifactId}
              />
            )}
          </CardContent>
        </Card>
      </div>

      {graphSupplements ? (
        <Notice variant="info" className="mt-6">
          Graph supplements — max decision chain depth: {graphSupplements.maxDecisionChainDepth} · Unresolved
          upstream reviews: {graphSupplements.unresolvedUpstreamReviewCount}
        </Notice>
      ) : null}

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4 text-sm text-etos-ink-muted">
        <summary className="cursor-pointer font-extrabold text-etos-ink">Advanced / Debug</summary>
        <div className="mt-4 space-y-4">
          <div className="flex flex-wrap gap-3">
            <Link href="/decisions" className="text-etos-accent hover:underline">
              Decisions
            </Link>
            <Link href="/tasks" className="text-etos-accent hover:underline">
              Review tasks
            </Link>
            <Link href="/learning-signals" className="text-etos-accent hover:underline">
              Learning signals
            </Link>
            <Link href="/ai-traces" className="text-etos-accent hover:underline">
              AI traces
            </Link>
            <Link href="/explorers" className="text-etos-accent hover:underline">
              Explorers
            </Link>
          </div>
          <div>
            <p className="mb-2 font-semibold text-etos-ink">Dashboard</p>
            <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
              {JSON.stringify(dashboard, null, 2)}
            </pre>
          </div>
          <div>
            <p className="mb-2 font-semibold text-etos-ink">Lists</p>
            <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
              {JSON.stringify(governance, null, 2)}
            </pre>
          </div>
          <div>
            <p className="mb-2 font-semibold text-etos-ink">Trends</p>
            <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
              {JSON.stringify(trendSeries, null, 2)}
            </pre>
          </div>
          <div>
            <p className="mb-2 font-semibold text-etos-ink">Connectors (boundary)</p>
            <pre className="overflow-x-auto rounded-xl border border-etos-border bg-etos-panel p-3 text-xs">
              {JSON.stringify(connectors, null, 2)}
            </pre>
          </div>
        </div>
      </details>
    </main>
  );
}
