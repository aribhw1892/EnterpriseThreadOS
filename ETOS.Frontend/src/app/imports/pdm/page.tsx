import {
  approvePdmIdentityCandidateAction,
  conflictPdmIdentityCandidateAction,
  generatePdmIdentityCandidatesAction,
  loadPdmBatchStates,
  promotePdmBatchesAction,
  runPdmDemoImportAction,
} from "@/app/imports/pdm/actions";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { PdmBatchPipeline } from "@/components/pdm-import/PdmBatchPipeline";
import { PdmGuidedBatchPanel } from "@/components/pdm-import/PdmGuidedBatchPanel";
import { PdmImportWizard } from "@/components/pdm-import/PdmImportWizard";
import { PdmRunbookPanel } from "@/components/pdm-import/PdmRunbookPanel";
import { PdmTransformUpload } from "@/components/pdm-import/PdmTransformUpload";
import {
  adminUserId,
  getIdentityCandidatesForBatch,
  selectedTenantId,
} from "@/lib/etos-api";
import { readPdmDemoManifest } from "@/lib/pdm-demo-fixtures";
import { getPdmImportProfiles } from "@/lib/pdm-import-config.server";
import Link from "next/link";

export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<{
    step?: string;
    batches?: string;
    mode?: string;
    error?: string;
    activeBatch?: string;
    activeProfile?: string;
    evidenceId?: string;
    mappingApproved?: string;
    staged?: string;
  }>;
};

function ErrorState({ error }: { error: string }) {
  return (
    <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
      {error}
    </div>
  );
}

export default async function PdmImportPage({ searchParams }: PageProps) {
  const params = await searchParams;
  const step = params.step ?? "1";
  const batches = params.batches ?? "";
  const batchIds = batches.split(",").map((item) => item.trim()).filter(Boolean);
  const { profiles, sourceSystem, error: profilesError } = await getPdmImportProfiles();
  const manifest = await readPdmDemoManifest();
  const batchStates = batchIds.length > 0 ? await loadPdmBatchStates(batchIds) : [];

  const identityByBatch = await Promise.all(
    batchIds.map(async (batchId) => ({
      batchId,
      candidates: await getIdentityCandidatesForBatch(batchId),
    })),
  );

  const activeBatchDetail = batchStates.find((item) => item.batchId === params.activeBatch)?.detail;
  const mappingApproved =
    Boolean(params.mappingApproved) ||
    Boolean(activeBatchDetail?.mappingVersions.some((item) => item.state === "Approved"));
  const staged =
    Boolean(params.staged) ||
    activeBatchDetail?.batch.status === "Staged" ||
    Boolean(activeBatchDetail?.stagingRuns.length);

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto grid max-w-7xl gap-8">
        <header className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <p className="text-sm font-semibold uppercase tracking-[0.3em] text-cyan-300">EnterpriseThreadOS</p>
          <div className="mt-4 flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <h1 className="text-4xl font-bold tracking-tight">PDM Import Wizard</h1>
              <p className="mt-3 max-w-3xl text-slate-300">
                Extract and transform SolidWorks PDM data locally, then import four governed CSV batches with package
                preset mappings and optional AI mapping suggestions.
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/imports">Import hub</ExplorerNavLink>
              <ExplorerNavLink href="/model-artifacts">Model package</ExplorerNavLink>
              <ExplorerNavLink href="/">Home</ExplorerNavLink>
            </div>
          </div>
          <div className="mt-5 grid gap-2 text-xs text-slate-500 md:grid-cols-2">
            <p>Admin user: {adminUserId}</p>
            <p>Tenant: {selectedTenantId}</p>
            <p>Source system: {sourceSystem}</p>
            <p>Session batches: {batchIds.length}</p>
          </div>
        </header>

        {params.error ? <ErrorState error={params.error} /> : null}
        {profilesError ? <ErrorState error={profilesError} /> : null}

        <PdmImportWizard currentStep={step} batches={batches || undefined} mode={params.mode}>
          {step === "1" ? (
            <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
              <h2 className="text-2xl font-semibold">Prerequisites</h2>
              <ul className="mt-4 grid gap-2 text-sm text-slate-300">
                <li>Backend running with tenant env vars configured.</li>
                <li>
                  Manufacturing reference package published — check{" "}
                  <Link href="/model-artifacts" className="text-cyan-300 hover:underline">
                    /model-artifacts
                  </Link>
                  .
                </li>
                <li>Local PdmExtractor + PdmTransform helpers available under ETOS.Helpers.</li>
                <li>Import mapping assistant agent configured for AI suggestions (optional).</li>
              </ul>
              <p className="mt-4 text-xs text-slate-500">
                Re-import creates duplicate staging rows (CREATE per row). Use a clean tenant for repeatable demos.
              </p>
              <Link
                href={`/imports/pdm?step=2${batches ? `&batches=${batches}` : ""}`}
                className="mt-6 inline-block rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 hover:bg-cyan-200"
              >
                Continue to extract
              </Link>
            </section>
          ) : null}

          {step === "2" ? (
            <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
              <h2 className="text-2xl font-semibold">Extract</h2>
              <div className="mt-4">
                <PdmRunbookPanel phase="extract" />
              </div>
              <Link
                href={`/imports/pdm?step=3${batches ? `&batches=${batches}` : ""}`}
                className="mt-6 inline-block rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 hover:bg-cyan-200"
              >
                Continue to transform
              </Link>
            </section>
          ) : null}

          {step === "3" ? (
            <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
              <h2 className="text-2xl font-semibold">Transform</h2>
              <div className="mt-4 grid gap-6">
                <PdmRunbookPanel phase="transform" />
                <PdmTransformUpload profiles={profiles} manifest={manifest.data} />
              </div>
              <Link
                href={`/imports/pdm?step=4${batches ? `&batches=${batches}` : ""}`}
                className="mt-6 inline-block rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 hover:bg-cyan-200"
              >
                Continue to import
              </Link>
            </section>
          ) : null}

          {step === "4" ? (
            <section className="grid gap-6">
              <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
                <h2 className="text-2xl font-semibold">Import four batches</h2>
                <p className="mt-2 text-sm text-slate-400">
                  Import order: parts → part-versions → has-version → version-bom. One-click demo uses package presets
                  only. Guided mode supports preset vs AI mapping review per file.
                </p>
                <form action={runPdmDemoImportAction} className="mt-4">
                  <button
                    type="submit"
                    className="rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 hover:bg-cyan-200"
                  >
                    Run full PDM demo import (preset mappings)
                  </button>
                </form>
              </div>

              {batchStates.length > 0 ? (
                <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
                  <h3 className="text-xl font-semibold">Pipeline status</h3>
                  <div className="mt-4">
                    <PdmBatchPipeline profiles={profiles} batchStates={batchStates} />
                  </div>
                </div>
              ) : null}

              <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
                <h3 className="text-xl font-semibold">Guided import (per file)</h3>
                <div className="mt-4 grid gap-4">
                  {profiles.map((profile, index) => {
                    const batchId = batchIds[index] ?? "";
                    const isActive = params.activeProfile ? profile.key === params.activeProfile : index === batchIds.length;
                    const detail = batchStates[index]?.detail;
                    const evidenceId =
                      params.activeProfile === profile.key && params.evidenceId
                        ? params.evidenceId
                        : detail?.evidence[0]?.id ?? "";
                    const profileMappingApproved = Boolean(
                      detail?.mappingVersions.some((item) => item.state === "Approved"),
                    );
                    const profileStaged =
                      detail?.batch.status === "Staged" || Boolean(detail?.stagingRuns.length);

                    if (!isActive && batchId) {
                      return (
                        <div
                          key={profile.key}
                          className="rounded-2xl border border-slate-800 bg-slate-950 px-4 py-3 text-sm text-slate-400"
                        >
                          {profile.fileName} — batch {batchId.slice(0, 8)}…{" "}
                          {profileStaged ? "(staged)" : profileMappingApproved ? "(mapped)" : "(uploaded)"}
                        </div>
                      );
                    }

                    return (
                      <PdmGuidedBatchPanel
                        key={profile.key}
                        profile={profile}
                        batchId={isActive && params.activeBatch ? params.activeBatch : batchId}
                        evidenceId={evidenceId}
                        batches={batches}
                        mappingApproved={profileMappingApproved || mappingApproved}
                        staged={profileStaged || staged}
                      />
                    );
                  })}
                </div>
              </div>

              {batchIds.length >= profiles.length ? (
                <Link
                  href={`/imports/pdm?step=5&batches=${batches}`}
                  className="inline-block rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 hover:bg-cyan-200"
                >
                  Continue to identity review
                </Link>
              ) : null}
            </section>
          ) : null}

          {step === "5" ? (
            <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
              <h2 className="text-2xl font-semibold">Identity review</h2>
              <p className="mt-2 text-sm text-slate-400">
                Pure PDM imports may have few identity candidates. Generate and review links when comparing systems.
              </p>
              <div className="mt-4 grid gap-4">
                {batchIds.map((batchId) => (
                  <form key={batchId} action={generatePdmIdentityCandidatesAction}>
                    <input type="hidden" name="batchId" value={batchId} />
                    <input type="hidden" name="batches" value={batches} />
                    <button
                      type="submit"
                      className="rounded-2xl border border-cyan-400/40 px-4 py-2 text-sm font-semibold text-cyan-200 hover:bg-cyan-400/10"
                    >
                      Generate candidates for batch {batchId.slice(0, 8)}…
                    </button>
                  </form>
                ))}
                {identityByBatch.map(({ batchId, candidates }) => (
                  <div key={batchId} className="grid gap-3">
                    <h3 className="text-sm font-semibold text-cyan-300">Batch {batchId.slice(0, 8)}…</h3>
                    {candidates.error ? <ErrorState error={candidates.error} /> : null}
                    {(candidates.data ?? []).length === 0 ? (
                      <p className="text-sm text-slate-500">No identity candidates for this batch.</p>
                    ) : (
                      (candidates.data ?? []).map((candidate) => (
                        <article key={candidate.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
                          <p className="text-sm text-slate-300">
                            {candidate.sourceSystem} {candidate.sourceRecordId} → {candidate.targetSystem}{" "}
                            {candidate.targetRecordId}
                          </p>
                          <p className="mt-1 text-xs text-slate-500">State: {candidate.state}</p>
                          <div className="mt-3 flex flex-wrap gap-3">
                            <form action={approvePdmIdentityCandidateAction}>
                              <input type="hidden" name="candidateId" value={candidate.id} />
                              <input type="hidden" name="batches" value={batches} />
                              <button type="submit" className="rounded-xl bg-cyan-300 px-3 py-2 text-xs font-semibold text-slate-950">
                                Approve
                              </button>
                            </form>
                            <form action={conflictPdmIdentityCandidateAction}>
                              <input type="hidden" name="candidateId" value={candidate.id} />
                              <input type="hidden" name="batches" value={batches} />
                              <button
                                type="submit"
                                className="rounded-xl border border-amber-500/40 px-3 py-2 text-xs font-semibold text-amber-200"
                              >
                                Mark conflicted
                              </button>
                            </form>
                          </div>
                        </article>
                      ))
                    )}
                  </div>
                ))}
              </div>
              <Link
                href={`/imports/pdm?step=6&batches=${batches}`}
                className="mt-6 inline-block rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 hover:bg-cyan-200"
              >
                Continue to promote
              </Link>
            </section>
          ) : null}

          {step === "6" ? (
            <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
              <h2 className="text-2xl font-semibold">Promote to trusted graph</h2>
              <p className="mt-2 text-sm text-slate-400">
                Promotion copies ready staged batches into the trusted graph. Validation errors and unresolved identity
                candidates block promotion.
              </p>
              <form action={promotePdmBatchesAction} className="mt-4">
                <input type="hidden" name="batches" value={batches} />
                <button
                  type="submit"
                  className="rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 hover:bg-cyan-200"
                >
                  Promote ready PDM batches
                </button>
              </form>
              {batchStates.length > 0 ? (
                <div className="mt-6">
                  <PdmBatchPipeline profiles={profiles} batchStates={batchStates} />
                </div>
              ) : null}
            </section>
          ) : null}

          {step === "7" ? (
            <section className="rounded-3xl border border-cyan-400/30 bg-cyan-400/10 p-6">
              <h2 className="text-2xl font-semibold">Import complete</h2>
              <p className="mt-2 text-sm text-slate-300">
                PDM batches are staged or promoted. Explore the graph, run governed chat, or use the generic import hub
                for latest-batch debugging.
              </p>
              <p className="mt-3 text-xs text-slate-500">
                Note: /imports latest-batch actions follow list order across all source systems, not just PDM.
              </p>
              <div className="mt-6 flex flex-wrap gap-3">
                <ExplorerNavLink href="/graph">Graph explorer</ExplorerNavLink>
                <ExplorerNavLink href="/chat">Governed chat</ExplorerNavLink>
                <ExplorerNavLink href="/imports">Import hub</ExplorerNavLink>
              </div>
              {batchStates.length > 0 ? (
                <div className="mt-6">
                  <PdmBatchPipeline profiles={profiles} batchStates={batchStates} />
                </div>
              ) : null}
            </section>
          ) : null}
        </PdmImportWizard>
      </div>
    </main>
  );
}
