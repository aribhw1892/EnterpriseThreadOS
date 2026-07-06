import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import {
  getGovernanceDashboard,
  getGovernanceKpiTrends,
  getGovernanceLists,
  GovernanceKpiValue,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

const trendKpiKeys = [
  "decision_throughput",
  "blocked_decisions",
  "outcome_verification_rate",
  "learning_signal_rate",
] as const;

function KpiCard({ kpi }: { kpi: GovernanceKpiValue }) {
  return (
    <article className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <p className="text-xs uppercase tracking-wide text-slate-500">{kpi.kpiKey}</p>
      <h3 className="mt-1 text-lg font-semibold">{kpi.title}</h3>
      <p className="mt-2 text-3xl font-semibold text-cyan-300">
        {kpi.status === "deferred" ? "Deferred" : (kpi.formattedValue ?? kpi.value ?? "—")}
      </p>
      <p className="mt-1 text-xs text-slate-500">{kpi.source}</p>
    </article>
  );
}

export default async function GovernanceDashboardPage() {
  const [dashboard, governance, throughputTrend] = await Promise.all([
    getGovernanceDashboard(),
    getGovernanceLists(),
    getGovernanceKpiTrends("decision_throughput", 14),
  ]);

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 21</p>
              <h1 className="mt-2 text-4xl font-semibold">Governance dashboard</h1>
              <p className="mt-3 max-w-3xl text-slate-400">
                Platform-defined governance KPIs, high-risk recommendations, audit visibility, and trend analytics
                derived from governed review, decision, outcome, and learning records.
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <ExplorerNavLink href="/decisions">Decisions</ExplorerNavLink>
              <ExplorerNavLink href="/tasks">Review tasks</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <ExplorerNavLink href="/">Home</ExplorerNavLink>
            </div>
          </div>
        </section>

        {dashboard.error ? (
          <section className="rounded-2xl border border-rose-900/60 bg-rose-950/30 p-4 text-rose-200">
            {dashboard.error}
          </section>
        ) : null}

        {dashboard.data ? (
          <>
            <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              {dashboard.data.kpis.map((kpi) => (
                <KpiCard key={kpi.kpiKey} kpi={kpi} />
              ))}
            </section>

            {dashboard.data.graphSupplements ? (
              <section className="rounded-2xl border border-slate-800 bg-slate-900 p-6">
                <h2 className="text-xl font-semibold">Graph supplements</h2>
                <p className="mt-2 text-sm text-slate-400">
                  Max decision chain depth: {dashboard.data.graphSupplements.maxDecisionChainDepth} · Unresolved upstream
                  reviews: {dashboard.data.graphSupplements.unresolvedUpstreamReviewCount}
                </p>
              </section>
            ) : null}

            <section className="rounded-2xl border border-slate-800 bg-slate-900 p-6">
              <h2 className="text-xl font-semibold">High-risk recommendations</h2>
              {dashboard.data.highRiskRecommendations.length === 0 ? (
                <p className="mt-3 text-sm text-slate-400">No actionable high-risk recommendations.</p>
              ) : (
                <ul className="mt-4 space-y-3">
                  {dashboard.data.highRiskRecommendations.map((item) => (
                    <li key={item.artifactId} className="rounded-xl border border-slate-800 bg-slate-950 p-4">
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <p className="font-semibold">{item.title}</p>
                          <p className="mt-1 text-xs text-slate-500">
                            {item.riskState} · {item.lifecycleStatus}
                          </p>
                        </div>
                        <Link href={item.contextViewRoute} className="text-sm font-semibold text-cyan-300 hover:text-cyan-200">
                          Open
                        </Link>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </>
        ) : null}

        <section className="rounded-2xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-xl font-semibold">Decision throughput trend (14 days)</h2>
          {throughputTrend.error ? (
            <p className="mt-3 text-sm text-rose-300">{throughputTrend.error}</p>
          ) : throughputTrend.data ? (
            <div className="mt-4 overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="text-slate-400">
                  <tr>
                    <th className="pb-2 pr-4">Day</th>
                    <th className="pb-2">Throughput</th>
                  </tr>
                </thead>
                <tbody>
                  {throughputTrend.data.points.map((point) => (
                    <tr key={point.bucketStart} className="border-t border-slate-800">
                      <td className="py-2 pr-4">{new Date(point.bucketStart).toLocaleDateString()}</td>
                      <td className="py-2">{point.value}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
          <p className="mt-3 text-xs text-slate-500">
            Additional trend keys: {trendKpiKeys.join(", ")}
          </p>
        </section>

        <section className="grid gap-6 lg:grid-cols-2">
          <article className="rounded-2xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-xl font-semibold">Recent audit records</h2>
            {governance.auditRecords.error ? (
              <p className="mt-3 text-sm text-rose-300">{governance.auditRecords.error}</p>
            ) : (
              <ul className="mt-4 space-y-2 text-sm text-slate-300">
                {(governance.auditRecords.data ?? []).map((record) => (
                  <li key={record.id} className="rounded-lg border border-slate-800 bg-slate-950 p-3">
                    {record.action} · {record.result}
                  </li>
                ))}
              </ul>
            )}
          </article>
          <article className="rounded-2xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-xl font-semibold">Recent security events</h2>
            {governance.securityEvents.error ? (
              <p className="mt-3 text-sm text-rose-300">{governance.securityEvents.error}</p>
            ) : (
              <ul className="mt-4 space-y-2 text-sm text-slate-300">
                {(governance.securityEvents.data ?? []).map((event) => (
                  <li key={event.id} className="rounded-lg border border-slate-800 bg-slate-950 p-3">
                    {event.eventType} · {event.severity}
                  </li>
                ))}
              </ul>
            )}
          </article>
        </section>
      </div>
    </main>
  );
}
