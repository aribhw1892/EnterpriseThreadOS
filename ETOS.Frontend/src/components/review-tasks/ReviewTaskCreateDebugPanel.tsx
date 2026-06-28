"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import {
  adminUserId,
  createAccessRequest,
  createManualReviewTask,
  createReviewTaskFromAccessRequest,
  createReviewTaskFromDataQualityIssue,
  createReviewTaskFromSecurityEvent,
  getReviewTaskTemplateArtifacts,
  listAccessRequests,
  listDataQualityIssues,
  listSecurityEvents,
  type DataQualityIssue,
  type ReviewTaskAccessRequest,
  type ReviewTaskTemplateArtifactSummary,
  type SecurityEvent,
} from "@/lib/etos-api";
import {
  DebugButton,
  DebugFieldLabel,
  DebugJsonBlock,
  DebugSelect,
  DebugStatusPill,
  DebugTextInput,
} from "@/components/review-tasks/review-task-debug-shared";

type CreateResult = {
  artifactId: string;
  versionId: string;
  versionLabel: string;
  status: string;
};

export function ReviewTaskCreateDebugPanel() {
  const router = useRouter();
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastResult, setLastResult] = useState<CreateResult | null>(null);
  const [lastRaw, setLastRaw] = useState<unknown>(null);

  const [templates, setTemplates] = useState<ReviewTaskTemplateArtifactSummary[]>([]);
  const [issues, setIssues] = useState<DataQualityIssue[]>([]);
  const [securityEvents, setSecurityEvents] = useState<SecurityEvent[]>([]);
  const [accessRequests, setAccessRequests] = useState<ReviewTaskAccessRequest[]>([]);

  const [manualTitle, setManualTitle] = useState("Debug manual review task");
  const [manualReviewTaskType, setManualReviewTaskType] = useState("business-action-review");
  const [selectedIssueId, setSelectedIssueId] = useState("");
  const [selectedSecurityEventId, setSelectedSecurityEventId] = useState("");
  const [selectedAccessRequestId, setSelectedAccessRequestId] = useState("");

  useEffect(() => {
    void (async () => {
      setLoading(true);
      const [templateResult, issueResult, eventResult, requestResult] = await Promise.all([
        getReviewTaskTemplateArtifacts(),
        listDataQualityIssues(),
        listSecurityEvents(25),
        listAccessRequests(),
      ]);

      const nextTemplates = templateResult.data ?? [];
      setTemplates(nextTemplates);
      if (nextTemplates[0]?.reviewTaskType) {
        setManualReviewTaskType(nextTemplates[0].reviewTaskType);
      }

      const nextIssues = issueResult.data ?? [];
      setIssues(nextIssues);
      if (nextIssues[0]?.id) {
        setSelectedIssueId(nextIssues[0].id);
      }

      const nextEvents = eventResult.data ?? [];
      setSecurityEvents(nextEvents);
      if (nextEvents[0]?.id) {
        setSelectedSecurityEventId(nextEvents[0].id);
      }

      const nextRequests = requestResult.data ?? [];
      setAccessRequests(nextRequests);
      if (nextRequests[0]?.id) {
        setSelectedAccessRequestId(nextRequests[0].id);
      }

      const loadErrors = [templateResult.error, issueResult.error, eventResult.error, requestResult.error].filter(
        Boolean,
      );
      setError(loadErrors.length > 0 ? loadErrors.join(" · ") : null);
      setLoading(false);
    })();
  }, []);

  async function runCreate(label: string, action: () => Promise<{ data: CreateResult | null; error: string | null }>) {
    setBusy(true);
    setError(null);
    setLastResult(null);
    setLastRaw(null);

    const result = await action();
    setLastRaw({ operation: label, ...result });

    if (result.error) {
      setError(result.error);
      setBusy(false);
      return;
    }

    if (result.data) {
      setLastResult(result.data);
      router.refresh();
    }

    setBusy(false);
  }

  return (
    <section className="rounded-3xl border border-violet-500/30 bg-slate-900 p-6">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-2xl font-semibold">Debug &amp; test harness</h2>
        <DebugStatusPill label="Issue 19 API" tone="neutral" />
        {loading ? <DebugStatusPill label="Loading sources…" tone="warn" /> : null}
        {busy ? <DebugStatusPill label="Running…" tone="warn" /> : null}
        {lastResult ? <DebugStatusPill label="Last call OK" tone="ok" /> : null}
        {error ? <DebugStatusPill label="Error" tone="error" /> : null}
      </div>
      <p className="mt-2 text-sm text-slate-400">
        Exercise review-task factory endpoints without Postman. Uses tenant headers from{" "}
        <code className="text-cyan-200">NEXT_PUBLIC_ETOS_*</code> env vars.
      </p>

      {error ? (
        <p className="mt-4 rounded-2xl border border-rose-500/30 bg-rose-500/10 p-3 text-sm text-rose-100">{error}</p>
      ) : null}

      {lastResult ? (
        <div className="mt-4 flex flex-wrap items-center gap-3">
          <Link
            href={`/tasks/${lastResult.artifactId}`}
            className="rounded-full bg-emerald-500 px-4 py-2 text-sm font-semibold text-slate-950"
          >
            Open created task
          </Link>
          <span className="text-xs text-slate-500">
            {lastResult.artifactId} · {lastResult.status}
          </span>
        </div>
      ) : null}

      <div className="mt-6 grid gap-6 xl:grid-cols-2">
        <div className="space-y-4 rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="text-lg font-semibold text-violet-200">Manual create</h3>
          <div className="space-y-2">
            <DebugFieldLabel>Title</DebugFieldLabel>
            <DebugTextInput value={manualTitle} onChange={(event) => setManualTitle(event.target.value)} />
          </div>
          <div className="space-y-2">
            <DebugFieldLabel>Review task type / template key</DebugFieldLabel>
            <DebugSelect value={manualReviewTaskType} onChange={(event) => setManualReviewTaskType(event.target.value)}>
              {templates.length > 0 ? (
                templates.map((template) => (
                  <option key={template.id} value={template.reviewTaskType ?? template.templateKey ?? template.name}>
                    {template.templateKey ?? template.name} ({template.readinessState})
                  </option>
                ))
              ) : (
                <>
                  <option value="business-action-review">business-action-review</option>
                  <option value="data-quality-review">data-quality-review</option>
                  <option value="governance-security-review">governance-security-review</option>
                  <option value="access-request-review">access-request-review</option>
                </>
              )}
            </DebugSelect>
          </div>
          <DebugButton
            disabled={busy || !manualTitle.trim()}
            onClick={() =>
              void runCreate("manual", () =>
                createManualReviewTask({
                  title: manualTitle.trim(),
                  reviewTaskType: manualReviewTaskType,
                  sourceType: "manual",
                  sourceReference: `debug-manual-${Date.now()}`,
                  primaryOwnerUserId: adminUserId ?? undefined,
                  severity: "medium",
                  trustState: "provisional",
                  conflictState: "none",
                }),
              )
            }
          >
            POST /review-tasks
          </DebugButton>
        </div>

        <div className="space-y-4 rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="text-lg font-semibold text-violet-200">From data quality issue</h3>
          {issues.length > 0 ? (
            <>
              <div className="space-y-2">
                <DebugFieldLabel>Issue</DebugFieldLabel>
                <DebugSelect value={selectedIssueId} onChange={(event) => setSelectedIssueId(event.target.value)}>
                  {issues.map((issue) => (
                    <option key={issue.id} value={issue.id}>
                      {issue.title} · {issue.severity} · {issue.status}
                    </option>
                  ))}
                </DebugSelect>
              </div>
              <DebugButton
                disabled={busy || !selectedIssueId}
                onClick={() =>
                  void runCreate("from-data-quality-issue", () =>
                    createReviewTaskFromDataQualityIssue(selectedIssueId),
                  )
                }
              >
                POST /from-data-quality-issue
              </DebugButton>
            </>
          ) : (
            <p className="text-sm text-slate-500">No data quality issues. Generate from Imports or Governance first.</p>
          )}
        </div>

        <div className="space-y-4 rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="text-lg font-semibold text-violet-200">From security event</h3>
          {securityEvents.length > 0 ? (
            <>
              <div className="space-y-2">
                <DebugFieldLabel>Security event</DebugFieldLabel>
                <DebugSelect
                  value={selectedSecurityEventId}
                  onChange={(event) => setSelectedSecurityEventId(event.target.value)}
                >
                  {securityEvents.map((event) => (
                    <option key={event.id} value={event.id}>
                      {event.eventType} · {event.severity} · {event.safeSummary.slice(0, 60)}
                    </option>
                  ))}
                </DebugSelect>
              </div>
              <DebugButton
                disabled={busy || !selectedSecurityEventId}
                onClick={() =>
                  void runCreate("from-security-event", () =>
                    createReviewTaskFromSecurityEvent(selectedSecurityEventId),
                  )
                }
              >
                POST /from-security-event
              </DebugButton>
            </>
          ) : (
            <p className="text-sm text-slate-500">No security events yet. Trigger a denied export or policy denial.</p>
          )}
        </div>

        <div className="space-y-4 rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="text-lg font-semibold text-violet-200">From access request</h3>
          {accessRequests.length > 0 ? (
            <>
              <div className="space-y-2">
                <DebugFieldLabel>Access request</DebugFieldLabel>
                <DebugSelect
                  value={selectedAccessRequestId}
                  onChange={(event) => setSelectedAccessRequestId(event.target.value)}
                >
                  {accessRequests.map((request) => (
                    <option key={request.id} value={request.id}>
                      {request.permissionKey} · {request.status} · {request.reason.slice(0, 50)}
                    </option>
                  ))}
                </DebugSelect>
              </div>
              <DebugButton
                disabled={busy || !selectedAccessRequestId}
                onClick={() =>
                  void runCreate("from-access-request", () =>
                    createReviewTaskFromAccessRequest(selectedAccessRequestId),
                  )
                }
              >
                POST /from-access-request
              </DebugButton>
            </>
          ) : (
            <div className="space-y-3">
              <p className="text-sm text-slate-500">No access requests. Seed one for debug:</p>
              <DebugButton
                variant="secondary"
                disabled={busy || !adminUserId}
                onClick={() => {
                  void (async () => {
                    setBusy(true);
                    setError(null);
                    const created = await createAccessRequest({
                      userId: adminUserId!,
                      permissionKey: "review_tasks.manage",
                      reason: "Debug access request for review task factory smoke test.",
                    });
                    setLastRaw({ operation: "seed-access-request", ...created });
                    if (created.error) {
                      setError(created.error);
                    } else if (created.data) {
                      setAccessRequests((current) => [created.data!, ...current]);
                      setSelectedAccessRequestId(created.data.id);
                    }
                    setBusy(false);
                  })();
                }}
              >
                Seed access request
              </DebugButton>
            </div>
          )}
        </div>
      </div>

      <div className="mt-6 space-y-4">
        <DebugJsonBlock title="Published templates" value={templates} />
        {lastRaw ? <DebugJsonBlock title="Last API response" value={lastRaw} /> : null}
      </div>
    </section>
  );
}
