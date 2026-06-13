import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { getDashboardArtifacts } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function DashboardsPage() {
  const artifacts = await getDashboardArtifacts();

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 17</p>
              <h1 className="mt-2 text-4xl font-semibold">Dashboards</h1>
              <p className="mt-3 max-w-3xl text-slate-400">
                Versioned dashboard templates with governed preview, readiness workflow, publish gates, and export.
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/reports">Reports</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <ExplorerNavLink href="/">Home</ExplorerNavLink>
            </div>
          </div>
        </section>

        {artifacts.error ? (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
            {artifacts.error}
          </div>
        ) : null}

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">DashboardVersion artifacts</h2>
          {artifacts.data && artifacts.data.length > 0 ? (
            <ul className="mt-6 space-y-3">
              {artifacts.data.map((artifact) => (
                <li key={artifact.id}>
                  <Link
                    href={`/dashboards/${artifact.id}`}
                    className="block rounded-2xl border border-slate-800 bg-slate-950 p-4 transition hover:border-cyan-300/40"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <p className="font-semibold">{artifact.name}</p>
                        <p className="text-sm text-slate-400">{artifact.description ?? artifact.artifactType}</p>
                      </div>
                      <div className="text-right text-sm text-slate-400">
                        <p>{artifact.latestVersionLabel ?? "No version"}</p>
                        <p>{artifact.readinessState ?? "Unknown"}</p>
                      </div>
                    </div>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-500">
              No dashboards yet. Draft one from{" "}
              <Link href="/chat" className="text-cyan-300 underline">
                governed chat
              </Link>
              .
            </p>
          )}
        </section>
      </div>
    </main>
  );
}
