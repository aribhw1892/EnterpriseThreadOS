"use client";

import { useState } from "react";
import { MappingCompare } from "@/components/import-wizard/MappingCompare";
import type { ImportPreview } from "@/lib/etos-api";
import type { ImportFileProfile } from "@/lib/import-wizard/import-profile-types";

type WizardServerAction = (formData: FormData) => void | Promise<void>;

type GuidedBatchPanelProps = {
  profile: ImportFileProfile;
  batchId: string;
  evidenceId: string;
  batches: string;
  mappingApproved: boolean;
  staged: boolean;
  uploadBatch: WizardServerAction;
  stageBatch: WizardServerAction;
  approveMapping: WizardServerAction;
  loadAiPreview: (input: {
    batchId: string;
    evidenceId: string;
  }) => Promise<{ preview: ImportPreview | null; error: string | null }>;
};

export function GuidedBatchPanel({
  profile,
  batchId,
  evidenceId,
  batches,
  mappingApproved,
  staged,
  uploadBatch,
  stageBatch,
  approveMapping,
  loadAiPreview,
}: GuidedBatchPanelProps) {
  const [aiPreview, setAiPreview] = useState<ImportPreview | null>(null);
  const [aiError, setAiError] = useState<string | null>(null);
  const [aiLoading, setAiLoading] = useState(false);

  async function requestAiPreview() {
    if (!batchId || !evidenceId || aiLoading) {
      return;
    }

    setAiLoading(true);
    setAiError(null);

    const result = await loadAiPreview({ batchId, evidenceId });
    setAiPreview(result.preview);
    setAiError(result.error);
    setAiLoading(false);
  }

  if (!batchId) {
    return (
      <div className="grid gap-4 rounded-2xl border border-slate-800 bg-slate-950 p-4">
        <p className="text-sm font-semibold text-slate-200">{profile.fileName}</p>

        <form action={uploadBatch} className="grid gap-3">
          <input type="hidden" name="profileKey" value={profile.key} />
          <input type="hidden" name="step" value="4" />
          <input type="hidden" name="batches" value={batches} />
          <label className="grid gap-2 text-sm text-slate-300">
            Upload {profile.fileName}
            <input type="file" name="file" accept=".csv,text/csv" className="text-xs text-slate-400" required />
          </label>
          <button
            type="submit"
            className="w-fit rounded-2xl bg-cyan-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-cyan-200"
          >
            Upload CSV
          </button>
        </form>

        <form action={uploadBatch} className="grid gap-2">
          <input type="hidden" name="profileKey" value={profile.key} />
          <input type="hidden" name="step" value="4" />
          <input type="hidden" name="batches" value={batches} />
          <input type="hidden" name="useDemoFixture" value="true" />
          <button
            type="submit"
            className="w-fit rounded-2xl border border-cyan-400/40 px-4 py-2 text-sm font-semibold text-cyan-200 hover:bg-cyan-400/10"
          >
            Use demo fixture
          </button>
        </form>
      </div>
    );
  }

  return (
    <div className="grid gap-4 rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <p className="text-xs text-slate-500">
        Batch {batchId.slice(0, 8)}… · evidence {evidenceId.slice(0, 8)}…
      </p>

      {!mappingApproved ? (
        <MappingCompare
          profile={profile}
          aiPreview={aiPreview}
          aiError={aiError}
          aiLoading={aiLoading}
          onRequestAiPreview={requestAiPreview}
          batchId={batchId}
          evidenceId={evidenceId}
          batches={batches}
          approveMapping={approveMapping}
        />
      ) : (
        <p className="text-sm text-emerald-300">Mapping approved for {profile.fileName}.</p>
      )}

      {mappingApproved && !staged ? (
        <form action={stageBatch}>
          <input type="hidden" name="batchId" value={batchId} />
          <input type="hidden" name="profileKey" value={profile.key} />
          <input type="hidden" name="batches" value={batches} />
          <button
            type="submit"
            className="rounded-2xl bg-cyan-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-cyan-200"
          >
            Stage {profile.fileName}
          </button>
        </form>
      ) : null}

      {staged ? <p className="text-sm text-emerald-300">Staged {profile.fileName}.</p> : null}
    </div>
  );
}
