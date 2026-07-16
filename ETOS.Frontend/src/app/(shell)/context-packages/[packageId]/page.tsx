import Link from "next/link";
import { ContextView360 } from "@/components/explorers/ContextView360";
import { ExplorerErrorState, ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { getContextPackageExplorerDetail, getContextView360 } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function ContextPackageDetailPage({ params }: { params: Promise<{ packageId: string }> }) {
  const { packageId } = await params;
  const [detail, view] = await Promise.all([
    getContextPackageExplorerDetail(packageId),
    getContextView360("ContextPackage", packageId),
  ]);

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h1 className="text-4xl font-semibold">Context package</h1>
              <p className="mt-3 font-mono text-sm text-cyan-200">{packageId}</p>
              {detail.data ? (
                <p className="mt-2 text-sm text-slate-400">
                  {detail.data.intentKey} · allowed {detail.data.allowedCount} · denied {detail.data.deniedCount}
                </p>
              ) : null}
            </div>
            <div className="flex flex-wrap gap-2">
              <ExplorerNavLink href="/context-packages">Packages</ExplorerNavLink>
              {detail.data?.traceRoute ? (
                <Link href={detail.data.traceRoute} className="rounded-full bg-cyan-300 px-4 py-2 text-sm font-semibold text-slate-950">
                  AI trace
                </Link>
              ) : null}
            </div>
          </div>
        </section>

        {detail.error ? <ExplorerErrorState error={detail.error} /> : null}
        {view.error || !view.data ? <ExplorerErrorState error={view.error ?? "Context view unavailable."} /> : <ContextView360 view={view.data} />}
      </div>
    </main>
  );
}
