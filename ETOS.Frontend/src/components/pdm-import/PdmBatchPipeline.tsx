"use client";

import type { PdmWizardBatchState } from "@/app/imports/pdm/actions";
import type { PdmImportFileProfile } from "@/lib/pdm-import-types";

function StatusBadge({ status }: { status: string }) {
  const normalized = status.toLowerCase();
  const className =
    normalized === "staged" || normalized === "completed" || normalized === "approved"
      ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200"
      : normalized === "failed" || normalized === "error"
        ? "bg-rose-100 text-rose-800 dark:bg-rose-950 dark:text-rose-200"
        : "bg-cyan-100 text-cyan-800 dark:bg-cyan-950 dark:text-cyan-200";

  return (
    <span className={`rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-wide ${className}`}>
      {status}
    </span>
  );
}

type PdmBatchPipelineProps = {
  profiles: PdmImportFileProfile[];
  batchStates: PdmWizardBatchState[];
};

export function PdmBatchPipeline({ profiles, batchStates }: PdmBatchPipelineProps) {
  return (
    <div className="grid gap-3">
      {profiles.map((profile, index) => {
        const batchState = batchStates[index];
        const detail = batchState?.detail;
        const batch = detail?.batch;
        const latestStaging = detail?.stagingRuns[0];
        const approvedMapping = detail?.mappingVersions.find((item) => item.state === "Approved");

        return (
          <article key={profile.key} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h3 className="font-semibold">{profile.fileName}</h3>
                <p className="mt-1 text-xs text-slate-500">
                  {profile.kind}
                  {profile.structuralRelationshipType ? ` · ${profile.structuralRelationshipType}` : ""}
                </p>
              </div>
              <StatusBadge status={batch?.status ?? "pending"} />
            </div>
            <div className="mt-3 grid gap-1 text-xs text-slate-500 md:grid-cols-2">
              <p>Batch: {batch?.id ?? "not created"}</p>
              <p>Mapping: {approvedMapping ? "approved" : detail?.mappingVersions[0]?.state ?? "none"}</p>
              <p>Staging nodes: {latestStaging?.nodeCount ?? 0}</p>
              <p>Staging relationships: {latestStaging?.relationshipCount ?? 0}</p>
            </div>
            {batch?.id ? (
              <p className="mt-2 text-xs text-slate-600">
                Linked batch id {batch.id.slice(0, 8)}… at pipeline index {index + 1}
              </p>
            ) : null}
          </article>
        );
      })}
    </div>
  );
}
