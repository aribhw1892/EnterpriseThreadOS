import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { PageHeader } from "@/components/ui/PageHeader";

const explorerCards = [
  {
    href: "/explorers/graph",
    title: "Full graph explorer",
    description: "Bloom-like Sigma canvas with search, pattern query, filters, and metadata.",
    primary: true,
  },
  {
    href: "/graph",
    title: "Graph & 360°",
    description: "Pick a node for 360° context panels, or open trusted promotion.",
    primary: true,
  },
  {
    href: "/artifacts",
    title: "Artifacts",
    description: "Registry, dependency flowline, and readiness gates.",
    primary: true,
  },
  {
    href: "/graph/promote",
    title: "Graph promote",
    description: "Snapshot, BOM compare, promote staged imports with DQ blockers.",
    primary: true,
  },
  {
    href: "/documents",
    title: "Documents",
    description: "Document memory explorer with side-panel detail.",
  },
  {
    href: "/ai-traces",
    title: "AI traces",
    description: "Retrieval audits with timeline and export packages.",
  },
  {
    href: "/recommendations",
    title: "Recommendations",
    description: "Evidence-backed inbox with risk filters.",
  },
  {
    href: "/learning-signals",
    title: "Learning signals",
    description: "Tenant rollups from repeated decision evidence patterns.",
  },
  {
    href: "/dashboards",
    title: "Dashboards",
    description: "Builder preview with publish readiness rail.",
  },
  {
    href: "/reports",
    title: "Reports",
    description: "Outline + canvas wire preview.",
  },
  {
    href: "/model-artifacts",
    title: "Model packages",
    description: "Active package callout and boundary pills.",
  },
];

export default function ExplorersHubPage() {
  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Explorers"
        description="Tenant-filtered hub into 360° context, artifacts, graph promotion, and Operate surfaces — without inventing a new mockup."
        actions={
          <Link href="/">
            <Button variant="ghost">Mission Control</Button>
          </Link>
        }
      />

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {explorerCards.map((card) => (
          <Link
            key={card.href}
            href={card.href}
            className={`rounded-etos-card border p-5 shadow-etos transition hover:border-etos-accent ${
              card.primary
                ? "border-etos-info-border bg-gradient-to-br from-etos-info-bg/40 to-etos-panel-elevated"
                : "border-etos-border-panel bg-etos-panel-elevated"
            }`}
          >
            <h2 className="text-lg font-extrabold text-etos-ink">{card.title}</h2>
            <p className="mt-2 text-sm text-etos-ink-muted">{card.description}</p>
          </Link>
        ))}
      </div>
    </main>
  );
}
