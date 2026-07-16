import Link from "next/link";
import type {
  ContextView360 as ContextView360Model,
  GraphExplorerNodeDetail,
  GraphExplorerRelationship,
} from "@/lib/etos-api";
import { GraphNeighborhoodCanvas } from "./GraphNeighborhoodCanvas";
import { SectionVisibilityBadge } from "./SectionVisibilityBadge";
import { ListStack } from "@/components/ui/ListItem";
import { SidePanel } from "@/components/ui/SidePanel";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";

export function ContextView360({
  view,
  graphCenter,
  relationships,
  relationshipsError,
}: {
  view: ContextView360Model;
  graphCenter?: GraphExplorerNodeDetail | null;
  relationships?: GraphExplorerRelationship[] | null;
  relationshipsError?: string | null;
}) {
  const firstItems = view.sections.flatMap((s) =>
    s.items.slice(0, 2).map((item) => ({
      ...item,
      sectionTitle: s.title,
    })),
  );

  const showNeighborhood =
    Boolean(graphCenter) && view.anchorKind === "GraphNode";

  return (
    <div className="grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
      <Card>
        <CardHeader>
          <CardTitle>
            360° context: {view.title}
          </CardTitle>
        </CardHeader>
        <CardContent>
          {showNeighborhood && graphCenter ? (
            <>
              {relationshipsError ? (
                <p className="mb-3 text-xs text-etos-danger-fg">
                  Neighborhood unavailable: {relationshipsError}
                </p>
              ) : null}
              <GraphNeighborhoodCanvas
                center={graphCenter}
                relationships={relationships ?? []}
                filterSummary={view.filterSummary}
              />
            </>
          ) : (
            <div className="relative min-h-[340px] overflow-hidden rounded-etos-card border border-etos-border bg-[radial-gradient(circle_at_24px_24px,var(--etos-info-border)_1px,transparent_1px)] bg-[length:24px_24px] bg-etos-panel-muted">
              <Node
                className="left-[38%] top-[40%] border-etos-info-border bg-etos-info-bg"
                title={view.title}
                subtitle={view.anchorKind}
              />
              {view.sections.slice(0, 4).map((section, index) => {
                const positions = [
                  "left-4 top-8 border-etos-success-border bg-etos-success-bg",
                  "left-4 bottom-16 border-etos-warning-border bg-etos-warning-bg",
                  "right-4 top-8 border-etos-purple-border bg-etos-purple-bg",
                  "right-4 bottom-16 border-etos-info-border bg-etos-info-bg",
                ];
                const first = section.items[0];
                return (
                  <Node
                    key={section.sectionKey}
                    className={positions[index] ?? positions[0]}
                    title={first?.title ?? section.title}
                    subtitle={section.title}
                  />
                );
              })}
              <div className="absolute bottom-4 left-4 rounded-xl border border-etos-border bg-etos-panel/90 px-3 py-2 text-[11px] text-etos-ink-muted">
                Permission-filtered · Trust-aware · Tenant scoped · Visible{" "}
                {view.filterSummary.visibleSectionCount} · Denied{" "}
                {view.filterSummary.deniedSectionCount}
              </div>
            </div>
          )}
          <p className="mt-3 text-sm text-etos-ink-muted">{view.safeSummary}</p>
        </CardContent>
      </Card>

      <SidePanel title="Context panels">
        <div className="mb-3 grid grid-cols-2 gap-2">
          {view.sections.slice(0, 6).map((section, index) => (
            <div
              key={section.sectionKey}
              className={`rounded-xl border px-2.5 py-2 text-xs font-extrabold ${
                index === 0
                  ? "border-etos-info-border bg-etos-info-bg text-etos-info-fg"
                  : "border-etos-border-soft bg-etos-panel-muted text-etos-ink"
              }`}
            >
              {section.title}
            </div>
          ))}
        </div>
        <div className="my-3 h-px bg-etos-border" />
        <ListStack>
          {firstItems.slice(0, 3).map((item, index) => (
            <div
              key={`${item.itemId}-${index}`}
              className="flex items-start gap-3 rounded-[14px] border border-etos-border-soft bg-etos-panel-muted p-3"
            >
              <div className="flex h-[34px] w-[34px] shrink-0 items-center justify-center rounded-xl bg-etos-info-bg text-sm font-black text-etos-info-fg">
                {item.sectionTitle.slice(0, 1)}
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-[13px] font-extrabold text-etos-ink">
                  {item.title}
                </p>
                <p className="mt-1 text-xs text-etos-ink-muted">
                  {item.safeSummary}
                </p>
                {item.linkRoute ? (
                  <Link
                    href={item.linkRoute}
                    className="mt-1 inline-block text-xs font-extrabold text-etos-accent-cyan underline-offset-2 hover:underline"
                  >
                    Open
                  </Link>
                ) : null}
              </div>
            </div>
          ))}
        </ListStack>

        <details className="mt-4">
          <summary className="cursor-pointer text-xs font-extrabold text-etos-ink-muted">
            All sections
          </summary>
          <div className="mt-3 space-y-3">
            {view.sections.map((section) => (
              <div
                key={section.sectionKey}
                className="rounded-xl border border-etos-border-soft p-3"
              >
                <div className="mb-2 flex items-center justify-between gap-2">
                  <p className="text-sm font-extrabold text-etos-ink">
                    {section.title}
                  </p>
                  <SectionVisibilityBadge visibility={section.visibility} />
                </div>
                {section.deniedReason ? (
                  <p className="text-xs text-etos-danger-fg">
                    {section.deniedReason}
                  </p>
                ) : section.items.length === 0 ? (
                  <p className="text-xs text-etos-ink-muted">No items.</p>
                ) : (
                  <ul className="space-y-1 text-xs text-etos-ink-muted">
                    {section.items.slice(0, 4).map((item) => (
                      <li key={item.itemId}>{item.title}</li>
                    ))}
                  </ul>
                )}
              </div>
            ))}
          </div>
        </details>
      </SidePanel>
    </div>
  );
}

function Node({
  title,
  subtitle,
  className,
}: {
  title: string;
  subtitle: string;
  className: string;
}) {
  return (
    <div
      className={`absolute min-w-[140px] rounded-2xl border bg-etos-panel px-3.5 py-3 shadow-etos ${className}`}
    >
      <p className="text-[13px] font-extrabold text-etos-ink">{title}</p>
      <p className="mt-1 text-[11px] text-etos-ink-muted">{subtitle}</p>
    </div>
  );
}
