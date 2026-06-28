"use client";

import Link from "next/link";
import { useState } from "react";
import { createReviewTaskFromRecommendationAction } from "@/lib/etos-api";
import { DebugButton, DebugStatusPill } from "@/components/review-tasks/review-task-debug-shared";

type ReviewTaskRecommendationDebugActionsProps = {
  artifactId: string;
  versionId: string;
  actionId: string;
  actionTitle: string;
  actionStatus: string;
};

export function ReviewTaskRecommendationDebugActions({
  artifactId,
  versionId,
  actionId,
  actionTitle,
  actionStatus,
}: ReviewTaskRecommendationDebugActionsProps) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [createdTaskId, setCreatedTaskId] = useState<string | null>(null);
  const [lastRaw, setLastRaw] = useState<unknown>(null);

  if (actionStatus === "convertedToReviewTask") {
    return <span className="text-xs uppercase text-cyan-300">Converted</span>;
  }

  return (
    <div className="flex flex-col gap-2">
      <DebugButton
        disabled={busy}
        onClick={() => {
          void (async () => {
            setBusy(true);
            setError(null);
            const result = await createReviewTaskFromRecommendationAction(artifactId, versionId, actionId);
            setLastRaw(result);
            if (result.error) {
              setError(result.error);
            } else if (result.data?.artifactId) {
              setCreatedTaskId(result.data.artifactId);
            }
            setBusy(false);
          })();
        }}
      >
        Debug: create task
      </DebugButton>
      {busy ? <DebugStatusPill label="Creating…" tone="warn" /> : null}
      {error ? <span className="text-xs text-rose-300">{error}</span> : null}
      {createdTaskId ? (
        <Link href={`/tasks/${createdTaskId}`} className="text-xs text-emerald-300 hover:underline">
          Open {actionTitle} task
        </Link>
      ) : null}
      {lastRaw ? (
        <details className="text-xs text-slate-500">
          <summary className="cursor-pointer">API response</summary>
          <pre className="mt-1 max-h-32 overflow-auto whitespace-pre-wrap">{JSON.stringify(lastRaw, null, 2)}</pre>
        </details>
      ) : null}
    </div>
  );
}
