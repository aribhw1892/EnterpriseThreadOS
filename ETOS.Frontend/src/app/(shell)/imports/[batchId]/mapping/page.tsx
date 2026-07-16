import Link from "next/link";
import {
  ActionButton,
  ImportStepper,
} from "@/components/imports/ImportHubShared";
import { Badge, StatusBadge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { ErrorState } from "@/components/ui/ErrorState";
import { Quote, SidePanel } from "@/components/ui/SidePanel";
import {
  approveDraftMapping,
  validateBatch,
} from "@/app/(shell)/imports/actions";
import { getImportBatchDetail, getImportLists } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function ImportMappingPage({
  params,
}: {
  params: Promise<{ batchId: string }>;
}) {
  const { batchId } = await params;
  const detail = await getImportBatchDetail(batchId);
  const lists = await getImportLists();
  const batch =
    detail.data?.batch ??
    lists.batches.data?.find((item) => item.id === batchId) ??
    null;
  const mappings = detail.data?.mappingVersions ?? [];
  const primary = mappings[0];
  const columns = primary?.columnMappings ?? [];
  const lifecycle = primary?.lifecycleMappings?.[0];

  const rows = columns.map((column) => {
    const target = column.canonicalAttributeKey
      ? `${column.canonicalObjectType}.${column.canonicalAttributeKey}`
      : column.isIdentityField
        ? `${column.canonicalObjectType} identity`
        : `${column.canonicalObjectType} unmapped`;
    const state = String(primary?.state ?? "Suggested");
    return {
      id: column.id,
      source: column.sourceColumn,
      target,
      confidence: column.isRequired ? "High" : "Review",
      decision: state,
    };
  });

  const rationale =
    lifecycle != null
      ? `"${lifecycle.sourceValue}" matched approved lifecycle vocabulary aliases toward ${lifecycle.canonicalLifecycleKey}. Review remaining low-confidence columns before approve.`
      : primary?.summary ??
        "Column, lifecycle, and object mapping suggestions are derived from ontology metadata and require user approval.";

  return (
    <main className="px-6 py-8 lg:px-8">
      <div className="mb-[18px] flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-[30px] font-bold tracking-tight text-etos-ink">
            Mapping review & AI suggestions
          </h1>
          <p className="mt-2 max-w-[900px] text-sm text-etos-ink-muted">
            Column, lifecycle, and object mapping suggestions are derived from
            ontology metadata and require user approval.
          </p>
        </div>
        <div className="flex flex-wrap gap-2.5">
          <Link href={`/imports/${batchId}/staging`}>
            <Button variant="ghost">Reject selected</Button>
          </Link>
          <ActionButton action={approveDraftMapping}>Approve mapping</ActionButton>
        </div>
      </div>

      <ImportStepper batch={batch} currentStepId="mapping" />
      {detail.error ? <ErrorState error={detail.error} /> : null}

      <div className="mt-2 grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Column mapping preview</CardTitle>
          </CardHeader>
          <CardContent>
            <DataTable
              rows={rows}
              rowKey={(row) => row.id}
              emptyMessage="No column mappings for this batch yet."
              columns={[
                {
                  key: "source",
                  header: "Source column",
                  render: (row) => row.source,
                },
                {
                  key: "target",
                  header: "Suggested target",
                  render: (row) => (
                    <span className="font-mono text-xs">{row.target}</span>
                  ),
                },
                {
                  key: "confidence",
                  header: "Confidence",
                  render: (row) => (
                    <Badge
                      variant={
                        row.confidence === "High" ? "success" : "warning"
                      }
                    >
                      {row.confidence}
                    </Badge>
                  ),
                },
                {
                  key: "decision",
                  header: "Decision",
                  render: (row) => <StatusBadge status={row.decision} />,
                },
              ]}
            />
            <div className="mt-4 flex flex-wrap gap-3">
              <ActionButton action={approveDraftMapping}>
                Approve latest draft mapping
              </ActionButton>
              <ActionButton action={validateBatch}>Validate latest batch</ActionButton>
            </div>
          </CardContent>
        </Card>

        <SidePanel title="Suggestion rationale">
          <Quote>{rationale}</Quote>
          <div className="my-3.5 h-px bg-etos-border" />
          <div className="flex flex-col gap-2">
            <Pill
              label="Provider"
              value={primary?.suggestionProvider ?? "RuleBasedMappingProvider"}
              variant="info"
            />
            <Pill label="Future provider" value="PydanticAI" variant="neutral" />
            <Pill
              label="Learning evidence"
              value="On correction"
              variant="teal"
            />
          </div>
        </SidePanel>
      </div>
    </main>
  );
}

function Pill({
  label,
  value,
  variant,
}: {
  label: string;
  value: string;
  variant: "info" | "neutral" | "teal";
}) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-xl border border-etos-border-soft bg-etos-panel-muted px-2.5 py-2 text-xs">
      <span className="font-semibold text-etos-ink-muted">{label}</span>
      <Badge variant={variant}>{value}</Badge>
    </div>
  );
}
