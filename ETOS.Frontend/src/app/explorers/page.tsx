import Link from "next/link";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";

const explorerCards = [
  { href: "/artifacts", title: "Artifacts", description: "Browse governed BaseArtifact records and open 360° views." },
  { href: "/graph", title: "Graph", description: "Explore trusted graph nodes with policy-filtered summaries." },
  { href: "/documents", title: "Documents", description: "Document artifacts with links into graph and context views." },
  { href: "/context-packages", title: "Context packages", description: "Retrieval runs and assembled governed context packages." },
  { href: "/ai-traces", title: "AI traces", description: "Existing AI Trace explorer with cross-links from 360 views." },
  { href: "/decisions", title: "Decisions", description: "Decision explorer foundation until Milestone 4 workflow lands." },
];

export default function ExplorersHubPage() {
  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Issue 16</p>
              <h1 className="mt-2 text-4xl font-semibold">Explorers</h1>
              <p className="mt-3 max-w-3xl text-slate-400">
                Tenant-filtered explorer hub for artifacts, graph nodes, documents, context packages, AI traces, and
                decision foundation records.
              </p>
            </div>
            <ExplorerNavLink href="/">Home</ExplorerNavLink>
          </div>
        </section>

        <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {explorerCards.map((card) => (
            <Link
              key={card.href}
              href={card.href}
              className="rounded-3xl border border-slate-800 bg-slate-900 p-6 transition hover:border-cyan-300/40"
            >
              <h2 className="text-xl font-semibold">{card.title}</h2>
              <p className="mt-2 text-sm text-slate-400">{card.description}</p>
            </Link>
          ))}
        </section>
      </div>
    </main>
  );
}
