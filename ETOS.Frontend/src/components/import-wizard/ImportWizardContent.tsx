import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { BatchPipeline } from "@/components/import-wizard/BatchPipeline";
import { GuidedBatchPanel } from "@/components/import-wizard/GuidedBatchPanel";
import { ImportWizardShell } from "@/components/import-wizard/ImportWizardShell";
import { RunbookPanel } from "@/components/import-wizard/RunbookPanel";
import { TransformUpload } from "@/components/import-wizard/TransformUpload";
import type { ImportWizardActions } from "@/lib/import-wizard/create-import-wizard-actions";
import { getImportProfiles } from "@/lib/import-wizard/import-config.server";
import { readHelpersManifest } from "@/lib/import-wizard/import-demo-fixtures.server";
import { getImportSourceConfig, getImportWizardBasePath } from "@/lib/import-wizard/import-source-config";
import type { ImportWizardSearchParams } from "@/lib/import-wizard/import-wizard-params";
import { adminUserId, getIdentityCandidatesForBatch, selectedTenantId } from "@/lib/etos-api";
import Link from "next/link";

function isReviewableIdentityCandidate(state: string): boolean {
  return state === "Provisional" || state === "Unverified";
}

function ErrorState({ error }: { error: string }) {
  return (
    <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
      {error}
    </div>
  );
}

type ImportWizardContentProps = {
  slug: string;
  searchParams: ImportWizardSearchParams;
  actions: ImportWizardActions;
};

export async function ImportWizardContent({ slug, searchParams: params, actions }: ImportWizardContentProps) {
  const step = params.step ?? "1";
  const batches = params.batches ?? "";
  const batchIds = batches.split(",").map((item) => item.trim()).filter(Boolean);
  const basePath = getImportWizardBasePath(slug);

  const { config, error: configError } = await getImportSourceConfig(slug);
  const { profiles, sourceSystem, error: profilesError } = await getImportProfiles(slug);

  const manifest =
    config != null
      ? await readHelpersManifest(config.helpersManifestPath, config.demoImportDir)
      : { data: null, error: null };

  const batchStates = batchIds.length > 0 ? await actions.loadBatchStates(batchIds) : [];

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

  if (!config) {
    return <ErrorState error={configError ?? `Import source '${slug}' is not configured.`} />;
  }

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto grid max-w-7xl gap-8">
        <header className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <p className="text-sm font-semibold uppercase tracking-[0.3em] text-cyan-300">EnterpriseThreadOS</p>
          <div className="mt-4 flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <h1 className="text-4xl font-bold tracking-tight">{config.wizardTitle}</h1>
              <p className="mt-3 max-w-3xl text-slate-300">{config.wizardDescription}</p>
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

        <ImportWizardShell basePath={basePath} currentStep={step} batches={batches || undefined} mode={params.mode}>
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
                <li>{config.prerequisitesHelper}</li>
                <li>Import mapping assistant agent configured for AI suggestions (optional).</li>
              </ul>
              <p className="mt-4 text-xs text-slate-500">
                Re-import creates duplicate staging rows (CREATE per row). Use a clean tenant for repeatable demos.
              </p>
              <Link
                href={`${basePath}?step=2${batches ? `&batches=${batches}` : ""}`}
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
                <RunbookPanel phase="extract" copy={config} />
              </div>
              <Link
                href={`${basePath}?step=3${batches ? `&batches=${batches}` : ""}`}
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
                <RunbookPanel phase="transform" copy={config} />
                <TransformUpload profiles={profiles} manifest={manifest.data} />
              </div>
              <Link
                href={`${basePath}?step=4${batches ? `&batches=${batches}` : ""}`}
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
                <p className="mt-2 text-sm text-slate-400">{config.importOrderDescription}</p>
                <form action={actions.runDemoImportAction} className="mt-4">
                  <button
                    type="submit"
                    className="rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 hover:bg-cyan-200"
                  >
                    {config.demoImportButtonLabel}
                  </button>
                </form>
              </div>

              {batchStates.length > 0 ? (
                <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
                  <h3 className="text-xl font-semibold">Pipeline status</h3>
                  <div className="mt-4">
                    <BatchPipeline profiles={profiles} batchStates={batchStates} />
                  </div>
                </div>
              ) : null}

              <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
                <h3 className="text-xl font-semibold">Guided import (per file)</h3>
                <div className="mt-4 grid gap-4">
                  {profiles.map((profile, index) => {
                    const batchId = batchIds[index] ?? "";
                    const isActive = params.activeProfile
                      ? profile.key === params.activeProfile
                      : index === batchIds.length;
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
                      <GuidedBatchPanel
                        key={profile.key}
                        profile={profile}
                        batchId={isActive && params.activeBatch ? params.activeBatch : batchId}
                        evidenceId={evidenceId}
                        batches={batches}
                        mappingApproved={profileMappingApproved || mappingApproved}
                        staged={profileStaged || staged}
                        uploadBatch={actions.uploadBatchAction}
                        stageBatch={actions.stageBatchAction}
                        approveMapping={actions.approveMappingAction}
                        loadAiPreview={actions.loadAiPreviewAction}
                      />
                    );
                  })}
                </div>
              </div>

              {batchIds.length >= profiles.length ? (
                <Link
                  href={`${basePath}?step=5&batches=${batches}`}
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
              <p className="mt-2 text-sm text-slate-400">{config.identityDescription}</p>
              <div className="mt-4 grid gap-4">
                {batchIds.map((batchId) => (
                  <form key={batchId} action={actions.generateIdentityCandidatesAction}>
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
                {identityByBatch.map(({ batchId, candidates }) => {
                  const candidateList = candidates.data ?? [];
                  const reviewableCount = candidateList.filter((item) => isReviewableIdentityCandidate(item.state)).length;
                  const conflictedCount = candidateList.filter((item) => item.state === "Conflicted").length;

                  return (
                  <div key={batchId} className="grid gap-3">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <h3 className="text-sm font-semibold text-cyan-300">Batch {batchId.slice(0, 8)}…</h3>
                      {candidateList.length > 0 ? (
                        <div className="flex flex-wrap items-center gap-3">
                          <p className="text-xs text-slate-500">
                            {reviewableCount} reviewable
                            {conflictedCount > 0 ? ` · ${conflictedCount} conflicted (skipped by approve all)` : ""}
                          </p>
                          <form action={actions.approveAllIdentityCandidatesAction}>
                            <input type="hidden" name="batchId" value={batchId} />
                            <input type="hidden" name="batches" value={batches} />
                            <button
                              type="submit"
                              disabled={reviewableCount === 0}
                              className="rounded-xl bg-cyan-300 px-3 py-2 text-xs font-semibold text-slate-950 disabled:cursor-not-allowed disabled:opacity-40"
                            >
                              Approve all reviewable ({reviewableCount})
                            </button>
                          </form>
                        </div>
                      ) : null}
                    </div>
                    {candidates.error ? <ErrorState error={candidates.error} /> : null}
                    {candidateList.length === 0 ? (
                      <p className="text-sm text-slate-500">No identity candidates for this batch.</p>
                    ) : (
                      candidateList.map((candidate) => (
                        <article key={candidate.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
                          <p className="text-sm text-slate-300">
                            {candidate.sourceSystem} {candidate.sourceRecordId} → {candidate.targetSystem}{" "}
                            {candidate.targetRecordId}
                          </p>
                          <p className="mt-1 text-xs text-slate-500">State: {candidate.state}</p>
                          <div className="mt-3 flex flex-wrap gap-3">
                            <form action={actions.approveIdentityCandidateAction}>
                              <input type="hidden" name="candidateId" value={candidate.id} />
                              <input type="hidden" name="batches" value={batches} />
                              <button
                                type="submit"
                                disabled={!isReviewableIdentityCandidate(candidate.state)}
                                className="rounded-xl bg-cyan-300 px-3 py-2 text-xs font-semibold text-slate-950 disabled:cursor-not-allowed disabled:opacity-40"
                              >
                                Approve
                              </button>
                            </form>
                            <form action={actions.conflictIdentityCandidateAction}>
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
                  );
                })}
              </div>
              <Link
                href={`${basePath}?step=6&batches=${batches}`}
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
              <form action={actions.promoteBatchesAction} className="mt-4">
                <input type="hidden" name="batches" value={batches} />
                <button
                  type="submit"
                  className="rounded-2xl bg-cyan-300 px-5 py-3 text-sm font-semibold text-slate-950 hover:bg-cyan-200"
                >
                  {config.promoteButtonLabel}
                </button>
              </form>
              {batchStates.length > 0 ? (
                <div className="mt-6">
                  <BatchPipeline profiles={profiles} batchStates={batchStates} />
                </div>
              ) : null}
            </section>
          ) : null}

          {step === "7" ? (
            <section className="rounded-3xl border border-cyan-400/30 bg-cyan-400/10 p-6">
              <h2 className="text-2xl font-semibold">{config.completeTitle}</h2>
              <p className="mt-2 text-sm text-slate-300">{config.completeDescription}</p>
              <p className="mt-3 text-xs text-slate-500">
                Note: /imports latest-batch actions follow list order across all source systems, not just this wizard.
              </p>
              <div className="mt-6 flex flex-wrap gap-3">
                <ExplorerNavLink href="/graph">Graph explorer</ExplorerNavLink>
                <ExplorerNavLink href="/chat">Governed chat</ExplorerNavLink>
                <ExplorerNavLink href="/imports">Import hub</ExplorerNavLink>
              </div>
              {batchStates.length > 0 ? (
                <div className="mt-6">
                  <BatchPipeline profiles={profiles} batchStates={batchStates} />
                </div>
              ) : null}
            </section>
          ) : null}
        </ImportWizardShell>
      </div>
    </main>
  );
}
