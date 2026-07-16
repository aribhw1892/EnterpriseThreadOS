import Link from "next/link";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { ListItem, ListStack } from "@/components/ui/ListItem";
import { PageHeader } from "@/components/ui/PageHeader";
import { StatusBadge } from "@/components/ui/Badge";
import { getGraphExplorerNodes } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function GraphExplorerPage() {
  const nodes = await getGraphExplorerNodes({ limit: 25 });
  const list = nodes.data?.nodes ?? [];
  const first = list[0];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Graph explorer"
        description="Lightweight hub into 360° context views and trusted graph promotion. Full Bloom canvas lives under Explorers."
        actions={
          <>
            <Link href="/explorers/graph">
              <Button variant="primary">Open Bloom canvas</Button>
            </Link>
            <Link href="/graph/promote">
              <Button variant="ghost">Promotion</Button>
            </Link>
            <Link href="/explorers">
              <Button variant="ghost">Explorers</Button>
            </Link>
          </>
        }
      />

      {nodes.error ? <ErrorState error={nodes.error} /> : null}

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Open 360° context</CardTitle>
          </CardHeader>
          <CardContent>
            {first ? (
              <ListStack>
                {list.slice(0, 5).map((node, index) => (
                  <div
                    key={node.nodeId}
                    className="flex items-start justify-between gap-3 rounded-[14px] border border-etos-border-soft bg-etos-panel-muted p-3"
                  >
                    <div className="flex items-start gap-3">
                      <div className="flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-xl bg-etos-info-bg text-sm font-black text-etos-info-fg">
                        {index + 1}
                      </div>
                      <div>
                        <p className="text-[13px] font-extrabold text-etos-ink">
                          {node.objectType}
                        </p>
                        <p className="mt-1 text-xs text-etos-ink-muted">
                          {node.safeSummary}
                        </p>
                        <p className="mt-1 text-[11px] text-etos-ink-subtle">
                          {node.graphSpace} ·{" "}
                          <StatusBadge status={String(node.trustState)} />
                        </p>
                      </div>
                    </div>
                    <Link href={`/graph/${node.nodeId}`}>
                      <Button variant="ghost">360°</Button>
                    </Link>
                  </div>
                ))}
              </ListStack>
            ) : (
              <EmptyState message="No graph nodes yet. Promote a staged import first." />
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Promotion & explorers</CardTitle>
          </CardHeader>
          <CardContent>
            <ListStack>
              <ListItem
                index={1}
                title="Trusted graph promotion"
                description="Gate ring, snapshot diff, and CAD vs EBOM heat comparison before promote."
              />
              <div className="flex justify-end">
                <Link href="/graph/promote">
                  <Button variant="primary">Open promotion</Button>
                </Link>
              </div>
              <ListItem
                index={2}
                title="360° explorers hub"
                description="Artifacts, documents, context packages, and cross-links into AI Trace."
              />
              <div className="flex justify-end">
                <Link href="/explorers">
                  <Button variant="ghost">Open explorers</Button>
                </Link>
              </div>
            </ListStack>
          </CardContent>
        </Card>
      </div>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4">
        <summary className="cursor-pointer text-sm font-extrabold text-etos-ink">
          Advanced / Debug — all nodes ({list.length})
        </summary>
        <ul className="mt-3 space-y-2 text-sm">
          {list.map((node) => (
            <li
              key={node.nodeId}
              className="flex items-center justify-between rounded-xl border border-etos-border-soft bg-etos-panel px-3 py-2"
            >
              <span>
                {node.objectType} · {node.nodeId.slice(0, 8)}
              </span>
              <Link
                href={`/graph/${node.nodeId}`}
                className="font-extrabold text-etos-accent-cyan underline-offset-2 hover:underline"
              >
                Open
              </Link>
            </li>
          ))}
        </ul>
      </details>
    </main>
  );
}
