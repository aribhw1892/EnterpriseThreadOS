import Link from "next/link";
import { ExplorerListShell, ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { DecisionExplorerFilters, DecisionExplorerItem, getDecisionExplorerList } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type DecisionsExplorerPageProps = {
  searchParams: Promise<{
    status?: string;
    participant?: string;
    search?: string;
    conflict?: string;
    outcomeKey?: string;
    hasOutcome?: string;
    minEvidenceCount?: string;
  }>;
};

function DecisionCard(item: DecisionExplorerItem) {
  return (
    <article className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h3 className="font-semibold">{item.title}</h3>
          <p className="mt-1 text-sm text-slate-400">{item.outcomeSummary}</p>
          <p className="mt-2 text-xs text-slate-500">
            {item.status} · evidence {item.evidenceCount} · conflict {item.conflictState}
            {item.hasOutcome ? " · outcome recorded" : ""}
          </p>
        </div>
        <Link href={item.contextViewRoute} className="text-sm font-semibold text-cyan-300 hover:text-cyan-200">
          Open
        </Link>
      </div>
    </article>
  );
}

function buildFilters(searchParams: Awaited<DecisionsExplorerPageProps["searchParams"]>): DecisionExplorerFilters {
  const filters: DecisionExplorerFilters = {};
  if (searchParams.status) filters.status = searchParams.status;
  if (searchParams.participant) filters.participant = searchParams.participant;
  if (searchParams.search) filters.search = searchParams.search;
  if (searchParams.conflict) filters.conflict = searchParams.conflict;
  if (searchParams.outcomeKey) filters.outcomeKey = searchParams.outcomeKey;
  if (searchParams.hasOutcome === "true") filters.hasOutcome = true;
  if (searchParams.hasOutcome === "false") filters.hasOutcome = false;
  if (searchParams.minEvidenceCount) {
    const parsed = Number.parseInt(searchParams.minEvidenceCount, 10);
    if (!Number.isNaN(parsed)) {
      filters.minEvidenceCount = parsed;
    }
  }
  return filters;
}

export default async function DecisionsExplorerPage({ searchParams }: DecisionsExplorerPageProps) {
  const resolvedSearchParams = await searchParams;
  const filters = buildFilters(resolvedSearchParams);
  const decisions = await getDecisionExplorerList(filters);

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h1 className="text-4xl font-semibold">Decision explorer</h1>
              <p className="mt-3 text-slate-400">
                Live decisions created from completed review tasks, with votes, outcomes, and learning evidence.
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <ExplorerNavLink href="/governance">Governance</ExplorerNavLink>
              <ExplorerNavLink href="/tasks">Review tasks</ExplorerNavLink>
              <ExplorerNavLink href="/recommendations">Recommendations</ExplorerNavLink>
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <ExplorerNavLink href="/">Home</ExplorerNavLink>
            </div>
          </div>
        </section>

        <section className="rounded-2xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-lg font-semibold">Filters</h2>
          <form className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-slate-400">Status</span>
              <input
                name="status"
                defaultValue={resolvedSearchParams.status ?? ""}
                className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                placeholder="PendingVotes"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-slate-400">Conflict</span>
              <input
                name="conflict"
                defaultValue={resolvedSearchParams.conflict ?? ""}
                className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                placeholder="Blocked"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-slate-400">Outcome key</span>
              <input
                name="outcomeKey"
                defaultValue={resolvedSearchParams.outcomeKey ?? ""}
                className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                placeholder="accept"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-slate-400">Search</span>
              <input
                name="search"
                defaultValue={resolvedSearchParams.search ?? ""}
                className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
                placeholder="Title"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-slate-400">Min evidence count</span>
              <input
                name="minEvidenceCount"
                type="number"
                min={0}
                defaultValue={resolvedSearchParams.minEvidenceCount ?? ""}
                className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
              />
            </label>
            <label className="flex flex-col gap-1 text-sm">
              <span className="text-slate-400">Has outcome</span>
              <select
                name="hasOutcome"
                defaultValue={resolvedSearchParams.hasOutcome ?? ""}
                className="rounded-lg border border-slate-700 bg-slate-950 px-3 py-2"
              >
                <option value="">Any</option>
                <option value="true">Yes</option>
                <option value="false">No</option>
              </select>
            </label>
            <div className="flex items-end">
              <button
                type="submit"
                className="rounded-lg bg-cyan-500 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-cyan-400"
              >
                Apply filters
              </button>
            </div>
          </form>
        </section>

        <ExplorerListShell
          title="Decisions"
          description="Searchable decision records with outcome and conflict state."
          result={decisions}
          emptyMessage="No decision-shaped artifacts are available yet."
          renderItem={DecisionCard}
        />
      </div>
    </main>
  );
}
