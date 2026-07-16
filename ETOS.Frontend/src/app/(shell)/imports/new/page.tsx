import Link from "next/link";
import {
  ActionButton,
  ImportStepper,
} from "@/components/imports/ImportHubShared";
import { Button } from "@/components/ui/Button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/Card";
import { PillStack, SidePanel } from "@/components/ui/SidePanel";
import {
  createComparisonImport,
  createDemoImport,
} from "@/app/(shell)/imports/actions";

export const dynamic = "force-dynamic";

export default function ImportsNewPage() {
  return (
    <main className="px-6 py-8 lg:px-8">
      <div className="mb-[18px] flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-[30px] font-bold tracking-tight text-etos-ink">
            Import wizard — upload
          </h1>
          <p className="mt-2 max-w-[900px] text-sm text-etos-ink-muted">
            Step-by-step enterprise import flow with package binding, evidence
            storage, and source-system read-only status.
          </p>
        </div>
        <div className="flex flex-wrap gap-2.5">
          <Link href="/imports">
            <Button variant="ghost">Cancel</Button>
          </Link>
          <ActionButton action={createDemoImport}>Upload & continue</ActionButton>
        </div>
      </div>

      <ImportStepper currentStepId="source" />

      <div className="mt-2 grid gap-4 lg:grid-cols-[1.2fr_0.8fr]">
        <Card>
          <CardHeader>
            <CardTitle>Upload source-owned file</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid gap-3.5 sm:grid-cols-2">
              <Field label="Source system" value="Mock PDM / CAD export" />
              <Field
                label="Active model package"
                value="etos-manufacturing-reference v1.0.0"
              />
              <Field label="Graph space" value="Staging" />
              <Field label="Evidence classification" value="Internal engineering" />
            </div>
            <div className="mt-4 rounded-etos-card border border-dashed border-etos-border bg-etos-panel-muted p-8 text-center">
              <p className="text-sm font-extrabold text-etos-ink">
                Drop CAD/PDM CSV or Excel file here
              </p>
              <p className="mt-2 text-xs text-etos-ink-muted">
                File becomes immutable import evidence linked to batch and future
                AI Trace. Demo actions below seed a draft batch.
              </p>
            </div>
            <div className="mt-4 flex flex-wrap gap-3">
              <ActionButton action={createDemoImport}>
                Create CAD/PDM draft batch
              </ActionButton>
              <ActionButton action={createComparisonImport}>
                Create ERP draft batch
              </ActionButton>
              <Link
                href="/imports/pdm"
                className="text-sm font-extrabold text-etos-accent-cyan underline-offset-2 hover:underline"
              >
                PDM wizard
              </Link>
              <Link
                href="/imports/odoo"
                className="text-sm font-extrabold text-etos-accent-cyan underline-offset-2 hover:underline"
              >
                Odoo wizard
              </Link>
            </div>
          </CardContent>
        </Card>

        <SidePanel title="Import guardrails">
          <PillStack
            items={[
              { label: "Source data", value: "Read-only", variant: "success" },
              { label: "Model package", value: "Pinned", variant: "info" },
              {
                label: "LLM access",
                value: "None at upload",
                variant: "warning",
              },
              { label: "Audit", value: "Enabled", variant: "success" },
            ]}
          />
          <div className="my-3.5 h-px bg-etos-border" />
          <p className="text-xs text-etos-ink-muted">
            No source system mutation is possible in the MVP import flow.
          </p>
        </SidePanel>
      </div>
    </main>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <label className="mb-1.5 block text-[11px] font-extrabold uppercase tracking-[0.06em] text-etos-ink-muted">
        {label}
      </label>
      <div className="rounded-xl border border-etos-border bg-etos-panel px-3 py-2.5 text-[13px] text-etos-ink">
        {value}
      </div>
    </div>
  );
}
