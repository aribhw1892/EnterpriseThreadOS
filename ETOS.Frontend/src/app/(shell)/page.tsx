import Link from "next/link";
import {
  adminUserId,
  getAgentRuns,
  getDecisionExplorerList,
  getDigitalThreadEvents,
  getDigitalThreadSummary,
  getDigitalThreadSystems,
  getImportLists,
  getPlatformHealth,
  getRecommendationArtifacts,
  resolveSelectedTenantId,
} from "@/lib/etos-api";
import {
  buildHeatmapGrid,
  mapDigitalThreadEventsToTimeline,
  systemsConnectedLabel,
} from "@/lib/digital-thread-map";
import { DigitalThreadTimeline } from "@/components/mission-control/DigitalThreadTimeline";
import {
  MissionControlLiveButton,
  MissionControlLiveProvider,
  MissionControlLiveStreamPanel,
  MissionControlMasterScrubber,
} from "@/components/mission-control/MissionControlLiveChrome";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { aiInsightsFixture } from "@/lib/ui-fixtures/mission-control";

export const dynamic = "force-dynamic";

const heatmapIntensityClasses = [
  "bg-etos-ops-heat-0",
  "bg-etos-ops-heat-1",
  "bg-etos-ops-heat-2",
  "bg-etos-ops-heat-3",
  "bg-etos-ops-heat-4",
];

const alertLevelClasses: Record<string, string> = {
  high: "border-etos-danger-border bg-etos-danger-bg text-etos-danger-fg",
  medium: "border-etos-warning-border bg-etos-warning-bg text-etos-warning-fg",
  info: "border-etos-info-border bg-etos-info-bg text-etos-info-fg",
};

function PreviewTag({ label = "Preview" }: { label?: string }) {
  return (
    <span className="rounded-full border border-etos-ops-border px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-etos-ops-ink-muted">
      {label}
    </span>
  );
}

function OpsPanel({
  title,
  action,
  preview = false,
  previewLabel,
  children,
}: {
  title: string;
  action?: React.ReactNode;
  preview?: boolean;
  previewLabel?: string;
  children: React.ReactNode;
}) {
  return (
    <section
      className="flex flex-col rounded-etos-card border border-etos-ops-border bg-etos-ops-panel p-4"
      data-ui-preview={preview ? "true" : undefined}
    >
      <div className="mb-3 flex items-center justify-between gap-2">
        <h2 className="text-xs font-bold uppercase tracking-[0.15em] text-etos-ops-ink-muted">
          {title}
        </h2>
        <div className="flex items-center gap-2">
          {preview ? <PreviewTag label={previewLabel} /> : null}
          {action}
        </div>
      </div>
      {children}
    </section>
  );
}

export default async function MissionControlPage() {
  const [
    health,
    recommendations,
    decisions,
    agentRuns,
    importLists,
    threadSummary,
    threadSystems,
    threadEvents,
    tenantId,
  ] = await Promise.all([
    getPlatformHealth(),
    getRecommendationArtifacts(),
    getDecisionExplorerList(),
    getAgentRuns(),
    getImportLists(),
    getDigitalThreadSummary(24),
    getDigitalThreadSystems(),
    getDigitalThreadEvents({ limit: 50 }),
    resolveSelectedTenantId(),
  ]);

  const healthyComponents =
    health?.components.filter(
      (component) => component.status.toLowerCase() === "healthy",
    ).length ?? 0;
  const totalComponents = health?.components.length ?? 0;
  const threadHealthValue =
    health && totalComponents > 0
      ? `${Math.round((healthyComponents / totalComponents) * 100)}%`
      : "—";

  const recommendationCount = recommendations.data?.length;
  const pendingRecommendations =
    recommendations.data?.filter(
      (item) => (item.lifecycleStatus ?? "").toLowerCase() !== "closed",
    ).length ?? 0;

  const openDecisions = decisions.data?.filter((item) => !item.hasOutcome);
  const runningAgentRuns = agentRuns.data?.filter(
    (run) => run.status.toLowerCase() === "running",
  );

  const dataQualityIssues = importLists.dataQualityIssues;
  const openDataQualityIssues =
    dataQualityIssues.data?.filter(
      (issue) =>
        !["resolved", "closed"].includes((issue.status ?? "").toLowerCase()),
    ) ?? [];

  const timelineEvents = threadEvents.data
    ? mapDigitalThreadEventsToTimeline(threadEvents.data)
    : [];
  const heatmap = buildHeatmapGrid(
    threadSummary.data?.heatmapBuckets ?? [],
    threadSystems.data ?? [],
    threadSummary.data?.windowHours ?? 24,
  );
  const topThreads = threadSummary.data?.topThreads ?? [];
  const alertCounts = threadSummary.data?.openAlertCounts;
  const threadAlerts = alertCounts
    ? [
        {
          id: "dq",
          label: "Data quality open",
          count: alertCounts.dataQualityOpen,
          level: alertCounts.dataQualityOpen > 0 ? "high" : "info",
        },
        {
          id: "security",
          label: "Security high/critical",
          count: alertCounts.securityHighOrCritical,
          level: alertCounts.securityHighOrCritical > 0 ? "high" : "info",
        },
        {
          id: "failed",
          label: "Failed / blocked runs",
          count: alertCounts.failedRuns,
          level: alertCounts.failedRuns > 0 ? "medium" : "info",
        },
      ]
    : [];

  const systemsValue = threadSystems.data
    ? systemsConnectedLabel(threadSystems.data)
    : "—";
  const eventsPerMin =
    threadSummary.data != null
      ? threadSummary.data.eventsLastMinute.toFixed(
          threadSummary.data.eventsLastMinute >= 10 ? 0 : 2,
        )
      : "—";

  return (
    <MissionControlLiveProvider
      initialEvents={threadEvents.data ?? []}
      streamError={threadEvents.error}
      auth={{ userId: adminUserId, tenantId }}
    >
    <main className="min-h-full bg-etos-ops-canvas px-5 py-6 text-etos-ops-ink">
      <div className="mx-auto flex max-w-[1600px] flex-col gap-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-black tracking-tight">
              Mission Control Timeline
            </h1>
            <p className="text-sm text-etos-ops-ink-muted">
              Real-time command center for the Digital Thread. Monitor, analyze,
              act.
            </p>
          </div>
          <MissionControlLiveButton />
        </div>

        <section aria-label="KPI strip" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
          <KpiCard
            ops
            label="Thread health"
            value={threadHealthValue}
            trend={healthyComponents === totalComponents && health ? "up" : "warn"}
            trendLabel={
              health ? `${healthyComponents}/${totalComponents} healthy` : "Backend unavailable"
            }
          />
          <KpiCard
            ops
            label="Systems connected"
            value={systemsValue}
            hint={
              threadSystems.error
                ? threadSystems.error
                : "Connector + import source systems"
            }
            trend={
              threadSystems.data && threadSystems.data.some((s) => s.connectionStatus !== "Healthy")
                ? "warn"
                : "flat"
            }
          />
          <KpiCard
            ops
            label="Events / min"
            value={eventsPerMin}
            hint={
              threadSummary.error
                ? threadSummary.error
                : "Rate over last 5 minutes"
            }
          />
          <KpiCard
            ops
            label="Recommendations"
            value={recommendationCount ?? "—"}
            trend={pendingRecommendations > 0 ? "warn" : "flat"}
            trendLabel={
              recommendations.data
                ? `${pendingRecommendations} pending review`
                : recommendations.error ?? undefined
            }
          />
          <KpiCard
            ops
            label="Agent runs"
            value={agentRuns.data?.length ?? "—"}
            trend={runningAgentRuns && runningAgentRuns.length > 0 ? "up" : "flat"}
            trendLabel={
              agentRuns.data ? `${runningAgentRuns?.length ?? 0} running` : undefined
            }
          />
          <KpiCard
            ops
            label="Open decisions"
            value={openDecisions?.length ?? "—"}
            trend={openDecisions && openDecisions.length > 0 ? "warn" : "flat"}
            trendLabel={decisions.data ? `${decisions.data.length} total` : undefined}
          />
        </section>

        <div className="grid gap-4 xl:grid-cols-[1fr_320px]">
          <div className="flex flex-col gap-4">
            {threadEvents.error ? (
              <ErrorState error={threadEvents.error} />
            ) : timelineEvents.length > 0 ? (
              <DigitalThreadTimeline
                events={timelineEvents}
                activeEventId={timelineEvents[timelineEvents.length - 1]?.id}
              />
            ) : (
              <OpsPanel title="Digital thread timeline">
                <p className="text-xs text-etos-ops-ink-muted">
                  No digital-thread events in the selected window for this tenant.
                </p>
              </OpsPanel>
            )}

            <div className="grid gap-4 lg:grid-cols-[1fr_280px]">
              <OpsPanel title="Thread activity heatmap (last 24 hours)">
                {threadSummary.error ? (
                  <p className="text-xs text-etos-warning-fg">{threadSummary.error}</p>
                ) : heatmap.rows.length > 0 ? (
                  <div className="grid gap-1">
                    {heatmap.rows.map((row, rowIndex) => (
                      <div
                        key={heatmap.systemLabels[rowIndex]}
                        className="grid grid-cols-[110px_1fr] items-center gap-2"
                      >
                        <span className="truncate text-[10px] text-etos-ops-ink-muted">
                          {heatmap.systemLabels[rowIndex]}
                        </span>
                        <div
                          className="grid gap-1"
                          style={{
                            gridTemplateColumns: `repeat(${row.length}, minmax(0, 1fr))`,
                          }}
                        >
                          {row.map((value, cellIndex) => (
                            <span
                              key={cellIndex}
                              aria-hidden
                              className={`h-4 rounded-sm ${heatmapIntensityClasses[value]}`}
                            />
                          ))}
                        </div>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="text-xs text-etos-ops-ink-muted">
                    No heatmap activity in the last 24 hours.
                  </p>
                )}
              </OpsPanel>

              <OpsPanel title="Top active threads">
                {threadSummary.error ? (
                  <p className="text-xs text-etos-warning-fg">{threadSummary.error}</p>
                ) : topThreads.length > 0 ? (
                  <ul className="grid gap-2">
                    {topThreads.map((thread) => (
                      <li
                        key={thread.id}
                        className="flex items-center justify-between gap-2 text-sm"
                      >
                        <span className="truncate">{thread.label}</span>
                        <span className="font-mono text-xs text-etos-ops-ink-muted">
                          {thread.eventCount}
                        </span>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-xs text-etos-ops-ink-muted">No active threads yet.</p>
                )}
              </OpsPanel>
            </div>

            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
              <OpsPanel
                title="Recommendations"
                action={
                  <Link
                    href="/recommendations"
                    className="text-xs font-semibold text-etos-accent-cyan hover:underline"
                  >
                    View all →
                  </Link>
                }
              >
                {recommendations.error ? (
                  <p className="text-xs text-etos-warning-fg">{recommendations.error}</p>
                ) : recommendations.data && recommendations.data.length > 0 ? (
                  <ul className="grid gap-2">
                    {recommendations.data.slice(0, 5).map((item) => (
                      <li key={item.id} className="text-xs leading-5">
                        <Link
                          href={`/recommendations/${item.id}`}
                          className="hover:text-etos-accent-cyan"
                        >
                          {item.name}
                        </Link>
                        <span className="block text-[10px] text-etos-ops-ink-muted">
                          {item.recommendationType ?? "recommendation"} ·{" "}
                          {item.lifecycleStatus ?? "unknown"}
                        </span>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-xs text-etos-ops-ink-muted">
                    No recommendations for the selected tenant.
                  </p>
                )}
              </OpsPanel>

              <OpsPanel
                title="Decisions"
                action={
                  <Link
                    href="/decisions"
                    className="text-xs font-semibold text-etos-accent-cyan hover:underline"
                  >
                    View all →
                  </Link>
                }
              >
                {decisions.error ? (
                  <p className="text-xs text-etos-warning-fg">{decisions.error}</p>
                ) : decisions.data && decisions.data.length > 0 ? (
                  <ul className="grid gap-2">
                    {decisions.data.slice(0, 5).map((item) => (
                      <li key={item.artifactId} className="text-xs leading-5">
                        <Link
                          href={`/decisions/${item.artifactId}`}
                          className="hover:text-etos-accent-cyan"
                        >
                          {item.title}
                        </Link>
                        <span className="block text-[10px] text-etos-ops-ink-muted">
                          {item.status} · {item.hasOutcome ? "outcome recorded" : "open"}
                        </span>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="text-xs text-etos-ops-ink-muted">
                    No decisions for the selected tenant.
                  </p>
                )}
              </OpsPanel>

              <OpsPanel
                title="Data quality"
                action={
                  <Link
                    href="/imports"
                    className="text-xs font-semibold text-etos-accent-cyan hover:underline"
                  >
                    Imports →
                  </Link>
                }
              >
                {dataQualityIssues.error ? (
                  <p className="text-xs text-etos-warning-fg">{dataQualityIssues.error}</p>
                ) : dataQualityIssues.data ? (
                  <div>
                    <p className="text-2xl font-black">
                      {openDataQualityIssues.length}
                      <span className="ml-2 text-xs font-semibold text-etos-ops-ink-muted">
                        open issues
                      </span>
                    </p>
                    <ul className="mt-2 grid gap-1">
                      {openDataQualityIssues.slice(0, 4).map((issue) => (
                        <li
                          key={issue.id}
                          className="truncate text-[11px] text-etos-ops-ink-muted"
                        >
                          {issue.issueCode}: {issue.title}
                        </li>
                      ))}
                    </ul>
                  </div>
                ) : (
                  <p className="text-xs text-etos-ops-ink-muted">
                    No data quality signals available.
                  </p>
                )}
              </OpsPanel>

              <OpsPanel title="AI insights" preview previewLabel="Preview — no insights API">
                <ul className="grid gap-2">
                  {aiInsightsFixture.map((insight) => (
                    <li key={insight} className="flex items-start gap-2 text-xs leading-5">
                      <span
                        aria-hidden
                        className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-etos-accent-cyan"
                      />
                      {insight}
                    </li>
                  ))}
                </ul>
              </OpsPanel>
            </div>
          </div>

          <div className="flex flex-col gap-4">
            <MissionControlLiveStreamPanel />

            <OpsPanel title="Thread alerts">
              {threadSummary.error ? (
                <p className="text-xs text-etos-warning-fg">{threadSummary.error}</p>
              ) : threadAlerts.length > 0 ? (
                <ul className="grid gap-2">
                  {threadAlerts.map((alert) => (
                    <li
                      key={alert.id}
                      className={`flex items-center justify-between gap-2 rounded-xl border px-3 py-2 text-xs font-semibold ${alertLevelClasses[alert.level]}`}
                    >
                      <span>{alert.label}</span>
                      <span className="font-mono">{alert.count}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="text-xs text-etos-ops-ink-muted">No alert summary available.</p>
              )}
            </OpsPanel>
          </div>
        </div>

        <MissionControlMasterScrubber />
      </div>
    </main>
    </MissionControlLiveProvider>
  );
}
