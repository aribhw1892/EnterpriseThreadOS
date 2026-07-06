"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import {
  adminUserId,
  addReviewTaskComment,
  assignReviewTask,
  completeReviewTask,
  createReviewTaskEscalation,
  getReviewTaskPayload,
  updateReviewTaskStatus,
  type ReviewTaskPayload,
} from "@/lib/etos-api";
import {
  DebugButton,
  DebugFieldLabel,
  DebugJsonBlock,
  DebugSelect,
  DebugStatusPill,
  DebugTextInput,
} from "@/components/review-tasks/review-task-debug-shared";

const REVIEW_TASK_STATUSES = [
  "draft",
  "open",
  "blocked",
  "inReview",
  "completed",
  "cancelled",
  "needsReevaluation",
] as const;

type ReviewTaskDetailDebugPanelProps = {
  artifactId: string;
  versionId: string;
  initialPayload: ReviewTaskPayload;
};

export function ReviewTaskDetailDebugPanel({
  artifactId,
  versionId,
  initialPayload,
}: ReviewTaskDetailDebugPanelProps) {
  const router = useRouter();
  const [payload, setPayload] = useState(initialPayload);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastRaw, setLastRaw] = useState<unknown>(null);

  const [assignUserId, setAssignUserId] = useState(adminUserId ?? "");
  const [assignRoleKey, setAssignRoleKey] = useState("");
  const [statusValue, setStatusValue] = useState(payload.status);
  const [blockingReason, setBlockingReason] = useState(payload.blockingReason ?? "");
  const [commentBody, setCommentBody] = useState("");
  const [completeSummary, setCompleteSummary] = useState("Completed from debug panel.");

  async function refreshPayload() {
    const result = await getReviewTaskPayload(artifactId, versionId);
    if (result.data) {
      setPayload(result.data);
      setStatusValue(result.data.status);
      setBlockingReason(result.data.blockingReason ?? "");
    }
    return result;
  }

  async function runOperation(label: string, action: () => Promise<{ error: string | null }>) {
    setBusy(true);
    setError(null);
    const result = await action();
    setLastRaw({ operation: label, error: result.error });
    if (result.error) {
      setError(result.error);
    } else {
      await refreshPayload();
      router.refresh();
    }
    setBusy(false);
  }

  const isClosed = payload.status === "completed" || payload.status === "cancelled";
  const escalationEnabled = payload.escalationPlaceholder?.enabled === true;

  return (
    <section className="rounded-3xl border border-violet-500/30 bg-slate-900 p-6">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-2xl font-semibold">Operations debug panel</h2>
        {busy ? <DebugStatusPill label="Running…" tone="warn" /> : null}
        {error ? <DebugStatusPill label="Error" tone="error" /> : null}
        {payload.status === "blocked" ? (
          <DebugStatusPill label="Blocked" tone="warn" />
        ) : null}
        {escalationEnabled ? <DebugStatusPill label="Escalation path enabled" tone="ok" /> : null}
      </div>
      <p className="mt-2 text-sm text-slate-400">
        Assign, transition status, comment, complete (accepted/rejected), and escalation placeholder create.
      </p>

      {error ? (
        <p className="mt-4 rounded-2xl border border-rose-500/30 bg-rose-500/10 p-3 text-sm text-rose-100">{error}</p>
      ) : null}

      <div className="mt-6 grid gap-6 lg:grid-cols-2">
        <div className="space-y-3 rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold text-violet-200">Assign</h3>
          <DebugFieldLabel>Primary owner user id</DebugFieldLabel>
          <DebugTextInput value={assignUserId} onChange={(event) => setAssignUserId(event.target.value)} />
          <DebugFieldLabel>Assigned role key</DebugFieldLabel>
          <DebugTextInput
            value={assignRoleKey}
            onChange={(event) => setAssignRoleKey(event.target.value)}
            placeholder="tenant-admin"
          />
          <DebugButton
            disabled={busy || isClosed}
            onClick={() =>
              void runOperation("assign", async () => {
                const result = await assignReviewTask(artifactId, versionId, {
                  primaryOwnerUserId: assignUserId.trim() || null,
                  assignedRoleKey: assignRoleKey.trim() || null,
                });
                return { error: result.error };
              })
            }
          >
            PATCH /assign
          </DebugButton>
        </div>

        <div className="space-y-3 rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold text-violet-200">Status</h3>
          <DebugFieldLabel>Status</DebugFieldLabel>
          <DebugSelect value={statusValue} onChange={(event) => setStatusValue(event.target.value)}>
            {REVIEW_TASK_STATUSES.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </DebugSelect>
          <DebugFieldLabel>Blocking reason</DebugFieldLabel>
          <DebugTextInput value={blockingReason} onChange={(event) => setBlockingReason(event.target.value)} />
          <DebugButton
            disabled={busy || isClosed}
            onClick={() =>
              void runOperation("status", async () => {
                const result = await updateReviewTaskStatus(artifactId, versionId, {
                  status: statusValue,
                  blockingReason: blockingReason.trim() || null,
                });
                return { error: result.error };
              })
            }
          >
            PATCH /status
          </DebugButton>
        </div>

        <div className="space-y-3 rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold text-violet-200">Comment</h3>
          <DebugFieldLabel>Body</DebugFieldLabel>
          <textarea
            value={commentBody}
            onChange={(event) => setCommentBody(event.target.value)}
            rows={3}
            className="w-full rounded-xl border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100"
            placeholder="Debug comment"
          />
          <DebugButton
            disabled={busy || !commentBody.trim()}
            onClick={() =>
              void runOperation("comment", async () => {
                const result = await addReviewTaskComment(artifactId, versionId, commentBody.trim());
                if (!result.error) {
                  setCommentBody("");
                }
                return { error: result.error };
              })
            }
          >
            POST /comments
          </DebugButton>
        </div>

        <div className="space-y-3 rounded-2xl border border-slate-800 bg-slate-950 p-4">
          <h3 className="font-semibold text-violet-200">Complete</h3>
          <DebugFieldLabel>Summary</DebugFieldLabel>
          <DebugTextInput value={completeSummary} onChange={(event) => setCompleteSummary(event.target.value)} />
          <div className="flex flex-wrap gap-2">
            <DebugButton
              variant="primary"
              disabled={busy || isClosed}
              onClick={() =>
                void runOperation("complete-accepted", async () => {
                  const result = await completeReviewTask(
                    artifactId,
                    versionId,
                    "accepted",
                    completeSummary.trim() || undefined,
                  );
                  setLastRaw((current: unknown) => ({ ...(current as object), completeResult: result.data, error: result.error }));
                  return { error: result.error };
                })
              }
            >
              Complete (accepted)
            </DebugButton>
            <DebugButton
              variant="danger"
              disabled={busy || isClosed}
              onClick={() =>
                void runOperation("complete-rejected", async () => {
                  const result = await completeReviewTask(
                    artifactId,
                    versionId,
                    "rejected",
                    completeSummary.trim() || undefined,
                  );
                  setLastRaw((current: unknown) => ({ ...(current as object), completeResult: result.data, error: result.error }));
                  return { error: result.error };
                })
              }
            >
              Complete (rejected)
            </DebugButton>
          </div>
          <p className="text-xs text-slate-500">Completion creates a decision artifact when Issue 20 handler is active.</p>
          {(lastRaw &&
          typeof lastRaw === "object" &&
          lastRaw !== null &&
          "completeResult" in lastRaw &&
          (lastRaw as { completeResult?: { decisionArtifactId?: string | null } }).completeResult?.decisionArtifactId) ? (
            <Link
              href={`/decisions/${String((lastRaw as { completeResult: { decisionArtifactId: string } }).completeResult.decisionArtifactId)}`}
              className="inline-block text-sm text-cyan-300 hover:underline"
            >
              Open created decision
            </Link>
          ) : null}
        </div>

        <div className="space-y-3 rounded-2xl border border-slate-800 bg-slate-950 p-4 lg:col-span-2">
          <h3 className="font-semibold text-violet-200">Escalation placeholder</h3>
          <p className="text-sm text-slate-400">
            Requires template escalation path enabled on this task. Governance-security and business-action seeds enable
            it in dev.
          </p>
          <DebugButton
            disabled={busy || !escalationEnabled}
            onClick={() =>
              void runOperation("escalation", async () => {
                const result = await createReviewTaskEscalation(artifactId, versionId);
                if (result.data?.artifactId) {
                  setLastRaw({ operation: "escalation", createdTaskId: result.data.artifactId, ...result });
                }
                return { error: result.error };
              })
            }
          >
            POST /escalation
          </DebugButton>
          {lastRaw && typeof lastRaw === "object" && lastRaw !== null && "createdTaskId" in lastRaw ? (
            <Link
              href={`/tasks/${String((lastRaw as { createdTaskId: string }).createdTaskId)}`}
              className="inline-block text-sm text-cyan-300 hover:underline"
            >
              Open escalation task
            </Link>
          ) : null}
        </div>
      </div>

      <div className="mt-6 space-y-4">
        <DebugJsonBlock title="Linked source IDs" value={{
          recommendationArtifactId: payload.recommendationArtifactId,
          suggestedActionId: payload.suggestedActionId,
          dataQualityIssueId: payload.dataQualityIssueId,
          securityEventId: payload.securityEventId,
          accessRequestId: payload.accessRequestId,
          aiTraceId: payload.aiTraceId,
          contextPackageId: payload.contextPackageId,
          prerequisiteTaskIds: payload.prerequisiteTaskIds,
        }} />
        <DebugJsonBlock title="Full task payload" value={payload} />
        {lastRaw ? <DebugJsonBlock title="Last operation result" value={lastRaw} /> : null}
      </div>
    </section>
  );
}
