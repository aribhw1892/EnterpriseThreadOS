import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { ReviewTaskCreateDebugPanel } from "@/components/review-tasks/ReviewTaskCreateDebugPanel";
import { getReviewTaskArtifacts } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function TasksPage() {
  const tasks = await getReviewTaskArtifacts();

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 19</p>
              <h1 className="mt-2 text-4xl font-semibold">Review tasks</h1>
              <p className="mt-3 max-w-3xl text-slate-400">
                Governed review task inbox with debug harness for all factory endpoints, chains, and escalation placeholders.
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/recommendations">Recommendations</ExplorerNavLink>
              <ExplorerNavLink href="/decisions">Decisions</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <ExplorerNavLink href="/">Home</ExplorerNavLink>
            </div>
          </div>
        </section>

        <ReviewTaskCreateDebugPanel />

        {tasks.error ? (
          <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
            {tasks.error}
          </div>
        ) : null}

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Task inbox</h2>
          {tasks.data && tasks.data.length > 0 ? (
            <ul className="mt-6 space-y-3">
              {tasks.data.map((task) => (
                <li key={task.id}>
                  <Link
                    href={`/tasks/${task.id}`}
                    className="block rounded-2xl border border-slate-800 bg-slate-950 p-4 transition hover:border-cyan-300/40"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <p className="font-semibold">{task.name}</p>
                        <p className="text-sm text-slate-400">
                          {task.status ?? "Unknown"} · {task.priority ?? "normal"}
                          {task.sourceType ? ` · ${task.sourceType}` : ""}
                          {task.primaryOwnerUserId ? ` · owner ${task.primaryOwnerUserId.slice(0, 8)}…` : ""}
                        </p>
                      </div>
                      <div className="flex items-center gap-2 text-right text-sm text-slate-400">
                        {task.isBlocked ? (
                          <span className="rounded-full bg-amber-500/20 px-2 py-1 text-xs uppercase text-amber-200">
                            Blocked
                          </span>
                        ) : null}
                        <span>{task.latestVersionLabel ?? "No version"}</span>
                      </div>
                    </div>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-500">
              No review tasks yet. Use the debug harness above or create from a recommendation suggested action.
            </p>
          )}
        </section>
      </div>
    </main>
  );
}
