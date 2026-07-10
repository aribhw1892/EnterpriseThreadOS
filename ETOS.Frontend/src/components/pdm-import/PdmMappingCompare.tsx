"use client";

import { approvePdmMappingAction } from "@/app/imports/pdm/actions";
import type { ImportPreview } from "@/lib/etos-api";
import type { PdmColumnMapping, PdmImportFileProfile } from "@/lib/pdm-import-types";
import { useFormStatus } from "react-dom";

type PdmMappingCompareProps = {
  profile: PdmImportFileProfile;
  aiPreview: ImportPreview | null;
  aiError: string | null;
  aiLoading: boolean;
  onRequestAiPreview: () => void;
  batchId: string;
  evidenceId: string;
  batches: string;
};

function formatMappingTarget(mapping: PdmColumnMapping) {
  if (mapping.canonicalAttributeKey) {
    return `${mapping.canonicalObjectType}.${mapping.canonicalAttributeKey}`;
  }
  return mapping.isIdentityField ? `${mapping.canonicalObjectType} identity` : `${mapping.canonicalObjectType} unmapped`;
}

function presetKey(mapping: PdmColumnMapping) {
  return `${mapping.sourceColumn}|${mapping.canonicalObjectType}|${mapping.canonicalAttributeKey ?? ""}|${mapping.isIdentityField}`;
}

function aiKey(suggestion: ImportPreview["columnSuggestions"][number]) {
  return `${suggestion.sourceColumn}|${suggestion.canonicalObjectType}|${suggestion.canonicalAttributeKey ?? ""}|${suggestion.isIdentityField}`;
}

function ApproveMappingButton({
  label,
  pendingLabel,
  variant,
  disabled,
}: {
  label: string;
  pendingLabel: string;
  variant: "preset" | "ai";
  disabled?: boolean;
}) {
  const { pending } = useFormStatus();

  const className =
    variant === "preset"
      ? "rounded-2xl bg-cyan-300 px-4 py-2 text-sm font-semibold text-slate-950 hover:bg-cyan-200 disabled:cursor-not-allowed disabled:opacity-60"
      : "rounded-2xl border border-cyan-400/40 px-4 py-2 text-sm font-semibold text-cyan-200 hover:bg-cyan-400/10 disabled:cursor-not-allowed disabled:opacity-50";

  return (
    <button type="submit" disabled={disabled || pending} className={className}>
      {pending ? pendingLabel : label}
    </button>
  );
}

export function PdmMappingCompare({
  profile,
  aiPreview,
  aiError,
  aiLoading,
  onRequestAiPreview,
  batchId,
  evidenceId,
  batches,
}: PdmMappingCompareProps) {
  return (
    <div className="grid gap-4">
      {profile.structuralRelationshipType ? (
        <p className="text-xs text-amber-300">
          Structural relationship: <span className="font-semibold">{profile.structuralRelationshipType}</span> (from
          package preset; applied on approve for both paths)
        </p>
      ) : null}

      <div className="grid gap-4 xl:grid-cols-2">
        <section className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h4 className="text-sm font-semibold text-cyan-300">Package preset</h4>
          <ul className="mt-3 grid gap-2 text-xs text-slate-300">
            {profile.columnMappings.map((mapping) => (
              <li key={presetKey(mapping)} className="rounded-lg border border-slate-800 px-3 py-2">
                <span className="font-mono text-cyan-100">{mapping.sourceColumn}</span>
                <span className="text-slate-500"> → </span>
                <span>{formatMappingTarget(mapping)}</span>
              </li>
            ))}
          </ul>
          <form action={approvePdmMappingAction} className="mt-4">
            <input type="hidden" name="batchId" value={batchId} />
            <input type="hidden" name="profileKey" value={profile.key} />
            <input type="hidden" name="mappingSource" value="preset" />
            <input type="hidden" name="evidenceId" value={evidenceId} />
            <input type="hidden" name="batches" value={batches} />
            <ApproveMappingButton
              label="Approve preset mapping"
              pendingLabel="Approving preset…"
              variant="preset"
            />
          </form>
        </section>

        <section className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h4 className="text-sm font-semibold text-cyan-300">AI suggestion</h4>
          <p className="mt-2 text-xs text-slate-500">
            Optional. Load AI column suggestions only when you want to compare against the package preset.
          </p>
          {aiError ? <p className="mt-3 text-xs text-amber-300">{aiError}</p> : null}
          {!aiPreview && !aiError && !aiLoading ? (
            <button
              type="button"
              onClick={onRequestAiPreview}
              className="mt-3 rounded-2xl border border-cyan-400/40 px-4 py-2 text-sm font-semibold text-cyan-200 hover:bg-cyan-400/10"
            >
              Load AI mapping suggestion
            </button>
          ) : null}
          {aiLoading ? <p className="mt-3 text-xs text-slate-500">Loading AI mapping suggestion…</p> : null}
          <ul className="mt-3 grid gap-2 text-xs text-slate-300">
            {(aiPreview?.columnSuggestions ?? [])
              .filter((suggestion) => suggestion.canonicalAttributeKey || suggestion.isIdentityField)
              .map((suggestion) => {
                const preset = profile.columnMappings.find((item) => item.sourceColumn === suggestion.sourceColumn);
                const differs =
                  !preset ||
                  preset.canonicalObjectType !== suggestion.canonicalObjectType ||
                  (preset.canonicalAttributeKey ?? "") !== (suggestion.canonicalAttributeKey ?? "") ||
                  preset.isIdentityField !== suggestion.isIdentityField;

                return (
                  <li
                    key={aiKey(suggestion)}
                    className={`rounded-lg border px-3 py-2 ${differs ? "border-amber-500/40 bg-amber-500/10" : "border-slate-800"}`}
                  >
                    <span className="font-mono text-cyan-100">{suggestion.sourceColumn}</span>
                    <span className="text-slate-500"> → </span>
                    <span>
                      {suggestion.canonicalAttributeKey
                        ? `${suggestion.canonicalObjectType}.${suggestion.canonicalAttributeKey}`
                        : `${suggestion.canonicalObjectType} identity`}
                    </span>
                    {differs ? <span className="ml-2 text-amber-300">differs from preset</span> : null}
                  </li>
                );
              })}
          </ul>
          <form action={approvePdmMappingAction} className="mt-4">
            <input type="hidden" name="batchId" value={batchId} />
            <input type="hidden" name="profileKey" value={profile.key} />
            <input type="hidden" name="mappingSource" value="ai" />
            <input type="hidden" name="evidenceId" value={evidenceId} />
            <input type="hidden" name="batches" value={batches} />
            {aiPreview ? (
              <input type="hidden" name="aiPreviewJson" value={JSON.stringify(aiPreview)} />
            ) : null}
            <ApproveMappingButton
              label="Approve AI mapping"
              pendingLabel="Approving AI mapping…"
              variant="ai"
              disabled={!aiPreview}
            />
          </form>
        </section>
      </div>
    </div>
  );
}
