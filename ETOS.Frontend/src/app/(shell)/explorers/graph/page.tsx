import Link from "next/link";
import { GraphBloomExplorer } from "@/components/explorers/GraphBloomExplorer";
import { Button } from "@/components/ui/Button";
import { ErrorState } from "@/components/ui/ErrorState";
import { PageHeader } from "@/components/ui/PageHeader";
import { getGraphExplorerNodes } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function BloomGraphExplorerPage() {
  const initial = await getGraphExplorerNodes({
    graphSpace: "Trusted",
    trustState: "Trusted",
    limit: 50,
  });

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        eyebrow="Explorers"
        title="Graph canvas"
        description="Bloom-like governed explorer: search, typed pattern query, filters, metadata, and hard result limits. No Cypher."
        actions={
          <>
            <Link href="/explorers">
              <Button variant="ghost">Explorers</Button>
            </Link>
            <Link href="/graph">
              <Button variant="ghost">Graph hub</Button>
            </Link>
            <Link href="/graph/promote">
              <Button variant="primary">Promotion</Button>
            </Link>
          </>
        }
      />

      {initial.error ? <ErrorState error={initial.error} /> : null}

      <GraphBloomExplorer
        initialNodes={initial.data?.nodes ?? []}
        initialTruncated={initial.data?.truncated ?? false}
        initialLimit={initial.data?.limit ?? 50}
      />
    </main>
  );
}
