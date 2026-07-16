import Link from "next/link";
import {
  captureTrustedGraphSnapshot,
  createBomComparisonForLatestStagedBatch,
  getImportLists,
  promoteReadyStagedImportBatch,
} from "@/lib/etos-api";
import { StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { ErrorState } from "@/components/ui/ErrorState";
import { Callout } from "@/components/ui/Notice";
import { PillStack } from "@/components/ui/SidePanel";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

export const dynamic = "force-dynamic";

async function promoteAction() {
  "use server";
  const result = await promoteReadyStagedImportBatch();
  if (result.error) {
    redirect(`/graph/promote?error=${encodeURIComponent(result.error)}`);
  }
  revalidatePath("/graph/promote");
  revalidatePath("/imports");
  redirect("/graph/promote?promoted=1");
}

async function snapshotAction() {
  "use server";
  const result = await captureTrustedGraphSnapshot();
  if (result.error) {
    redirect(`/graph/promote?error=${encodeURIComponent(result.error)}`);
  }
  revalidatePath("/graph/promote");
  redirect(
    `/graph/promote?snapshot=${encodeURIComponent(result.data?.snapshotId ?? "")}&nodes=${result.data?.nodeCount ?? 0}&rels=${result.data?.relationshipCount ?? 0}`,
  );
}

async function bomCompareAction() {
  "use server";
  const result = await createBomComparisonForLatestStagedBatch();
  if (result.error) {
    redirect(`/graph/promote?error=${encodeURIComponent(result.error)}`);
  }
  revalidatePath("/graph/promote");
  redirect(
    `/graph/promote?bom=${encodeURIComponent(result.data?.id ?? "")}&missingPrimary=${result.data?.missingInPrimarySideCount ?? 0}&missingSecondary=${result.data?.missingInSecondarySideCount ?? 0}&qty=${result.data?.quantityMismatchCount ?? 0}`,
  );
}

type PageProps = {
  searchParams: Promise<{
    error?: string;
    promoted?: string;
    snapshot?: string;
    nodes?: string;
    rels?: string;
    bom?: string;
    missingPrimary?: string;
    missingSecondary?: string;
    qty?: string;
  }>;
};

export default async function GraphPromotePage({ searchParams }: PageProps) {
  const params = await searchParams;
  const lists = await getImportLists();
  const batches = lists.batches.data ?? [];
  const staged = batches.filter((b) => b.status === "Staged");
  const promoted = batches.filter((b) =>
    b.status.toLowerCase().includes("promot"),
  );
  const dqIssues = lists.dataQualityIssues.data ?? [];
  const critical = dqIssues.filter(
    (i) => i.severity.toLowerCase() === "critical",
  ).length;
  const high = dqIssues.filter((i) => i.severity.toLowerCase() === "high").length;
  const candidates = lists.firstBatchIdentityCandidates.data ?? [];
  const approved = candidates.filter((c) =>
    String(c.state).toLowerCase().includes("approv"),
  ).length;
  const gatePct =
    candidates.length === 0
      ? staged.length > 0
        ? 86
        : 0
      : Math.round((approved / candidates.length) * 100);

  const diffRows = [
    {
      id: "nodes",
      type: "New object versions",
      count: params.nodes ? Number(params.nodes) : promoted.length * 100 || 0,
      example: staged[0]?.sourceSystem ?? "PartVersion",
      trust: "Neutral",
    },
    {
      id: "rels",
      type: "Relationship changes",
      count: params.rels ? Number(params.rels) : 0,
      example: "Assembly contains Part",
      trust: "Requires review",
    },
    {
      id: "identity",
      type: "Identity links changed",
      count: approved,
      example: "CAD → ERP candidate approved",
      trust: "Increases trust",
    },
    {
      id: "dq",
      type: "Data quality links",
      count: dqIssues.length,
      example: dqIssues[0]?.issueCode ?? "DQ linked",
      trust: "Penalty applied",
    },
  ];

  return (
    <main className="px-6 py-8 lg:px-8">
      <div className="mb-[18px] flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-[30px] font-bold tracking-tight text-etos-ink">
            Trusted graph promotion & snapshot diff
          </h1>
          <p className="mt-2 max-w-[900px] text-sm text-etos-ink-muted">
            Promotion gate, snapshot generation, BOM comparison, and diff
            visibility from staging to trusted graph.
          </p>
        </div>
        <div className="flex flex-wrap gap-2.5">
          {critical > 0 ? (
            <Link href="/imports/data-quality">
              <Button variant="danger">Blocked: resolve DQ</Button>
            </Link>
          ) : (
            <form action={promoteAction}>
              <Button type="submit" variant="primary" disabled={staged.length === 0}>
                Promote ready batch
              </Button>
            </form>
          )}
          <form action={snapshotAction}>
            <Button type="submit" variant="ghost">
              Generate snapshot
            </Button>
          </form>
        </div>
      </div>

      {params.error ? <ErrorState error={params.error} /> : null}
      {params.promoted ? (
        <Callout title="Promotion succeeded" variant="success" className="mb-4">
          Staged batch promoted into the trusted graph.
        </Callout>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-3">
        <Card>
          <CardHeader>
            <CardTitle>Promotion gate</CardTitle>
          </CardHeader>
          <CardContent>
            <div
              className="mx-auto mb-4 grid h-16 w-16 place-items-center rounded-full text-sm font-black text-etos-success-fg"
              style={{
                background: `conic-gradient(var(--etos-success-fg) 0 ${gatePct}%, var(--etos-border-soft) ${gatePct}% 100%)`,
              }}
            >
              <span className="grid h-12 w-12 place-items-center rounded-full bg-etos-panel">
                {gatePct}%
              </span>
            </div>
            <PillStack
              items={[
                {
                  label: "Critical issues",
                  value: `${critical} open`,
                  variant: critical > 0 ? "danger" : "success",
                },
                {
                  label: "High overrides",
                  value: `${high} needed`,
                  variant: high > 0 ? "warning" : "success",
                },
                {
                  label: "Identity approvals",
                  value: `${approved}/${candidates.length || "—"}`,
                  variant: "info",
                },
              ]}
            />
          </CardContent>
        </Card>

        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Graph diff summary</CardTitle>
          </CardHeader>
          <CardContent>
            <DataTable
              rows={diffRows}
              rowKey={(row) => row.id}
              emptyMessage="No diff yet — capture snapshot or promote."
              columns={[
                { key: "type", header: "Diff type", render: (r) => r.type },
                { key: "count", header: "Count", render: (r) => r.count },
                { key: "example", header: "Example", render: (r) => r.example },
                { key: "trust", header: "Trust effect", render: (r) => r.trust },
              ]}
            />
          </CardContent>
        </Card>
      </div>

      <Card className="mt-4">
        <CardHeader>
          <CardTitle>CAD BOM vs EBOM comparison</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="mb-4 grid max-w-xl grid-cols-[90px_repeat(5,1fr)] gap-1.5 text-[11px]">
            <b />
            <b>CAD</b>
            <b>EBOM</b>
            <b>Qty</b>
            <b>Lifecycle</b>
            <b>Risk</b>
            <HeatRow
              label="Primary"
              cells={["ok", "ok", "med", "ok", params.missingPrimary ? "high" : "med"]}
              risk={params.missingPrimary && Number(params.missingPrimary) > 0 ? "Med" : "Low"}
            />
            <HeatRow
              label="Secondary"
              cells={[
                "ok",
                params.missingSecondary && Number(params.missingSecondary) > 0
                  ? "high"
                  : "ok",
                params.qty && Number(params.qty) > 0 ? "high" : "ok",
                "med",
                "high",
              ]}
              risk={
                params.missingSecondary && Number(params.missingSecondary) > 0
                  ? "High"
                  : "Med"
              }
            />
          </div>
          <form action={bomCompareAction}>
            <Button type="submit" variant="ghost">
              Run BOM comparison
            </Button>
          </form>
          {params.bom ? (
            <p className="mt-3 text-xs text-etos-ink-muted">
              Run {params.bom}: missing primary {params.missingPrimary}, secondary{" "}
              {params.missingSecondary}, qty {params.qty}
            </p>
          ) : null}
        </CardContent>
      </Card>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4">
        <summary className="cursor-pointer text-sm font-extrabold text-etos-ink">
          Advanced / Debug
        </summary>
        <ul className="mt-3 space-y-2 text-sm">
          {batches.slice(0, 12).map((batch) => (
            <li
              key={batch.id}
              className="flex justify-between rounded-xl border border-etos-border-soft bg-etos-panel px-3 py-2"
            >
              <span>
                {batch.sourceSystem} · {batch.id.slice(0, 8)}
              </span>
              <StatusBadge status={batch.status} />
            </li>
          ))}
        </ul>
        <Link
          href="/imports"
          className="mt-3 inline-block text-sm font-extrabold text-etos-accent-cyan underline-offset-2 hover:underline"
        >
          Imports hub →
        </Link>
      </details>
    </main>
  );
}

function HeatRow({
  label,
  cells,
  risk,
}: {
  label: string;
  cells: Array<"ok" | "med" | "high">;
  risk: string;
}) {
  const cellClass = {
    ok: "bg-etos-success-bg text-etos-success-fg",
    med: "bg-etos-warning-bg text-etos-warning-fg",
    high: "bg-etos-danger-bg text-etos-danger-fg",
  };
  return (
    <>
      <span className="font-extrabold text-etos-ink">{label}</span>
      {cells.slice(0, 4).map((c, i) => (
        <div
          key={`${label}-${i}`}
          className={`grid h-8 place-items-center rounded-lg ${cellClass[c]}`}
        >
          {c === "ok" ? "✓" : c === "high" ? "!" : "·"}
        </div>
      ))}
      <div
        className={`grid h-8 place-items-center rounded-lg font-extrabold ${
          risk === "High"
            ? cellClass.high
            : risk === "Med"
              ? cellClass.med
              : cellClass.ok
        }`}
      >
        {risk}
      </div>
    </>
  );
}
