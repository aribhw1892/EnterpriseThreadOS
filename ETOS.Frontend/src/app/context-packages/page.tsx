import Link from "next/link";
import { ExplorerListShell, ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { ContextPackageExplorerSummary, getContextPackageExplorerList } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

function ContextPackageCard(item: ContextPackageExplorerSummary) {
  return (
    <article className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h3 className="font-semibold">{item.intentKey}</h3>
          <p className="mt-1 text-sm text-slate-400">{item.safeSummary}</p>
          <p className="mt-2 text-xs text-slate-500">
            {item.strategyKey} · allowed {item.filteredCount} · denied {item.deniedCount}
          </p>
        </div>
        <Link href={`/context-packages/${item.packageId}`} className="text-sm font-semibold text-cyan-300 hover:text-cyan-200">
          Open
        </Link>
      </div>
    </article>
  );
}

export default async function ContextPackagesExplorerPage() {
  const packages = await getContextPackageExplorerList();

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h1 className="text-4xl font-semibold">Context packages</h1>
              <p className="mt-3 text-slate-400">Governed retrieval runs with package ids, counts, and trace links.</p>
            </div>
            <div className="flex flex-wrap gap-2">
              <ExplorerNavLink href="/explorers">Explorers</ExplorerNavLink>
              <ExplorerNavLink href="/">Home</ExplorerNavLink>
            </div>
          </div>
        </section>

        <ExplorerListShell
          title="Packages"
          description="Recent governed query runs in the selected tenant."
          result={packages}
          emptyMessage="No context packages are available yet."
          renderItem={ContextPackageCard}
        />
      </div>
    </main>
  );
}
