import Link from "next/link";
import { notFound } from "next/navigation";
import { ExplorerNavLink } from "@/components/explorers/ExplorerListShell";
import { ReviewTaskDetailDebugPanel } from "@/components/review-tasks/ReviewTaskDetailDebugPanel";
import {
  getArtifactVersions,
  getReviewTaskArtifacts,
  getReviewTaskPayload,
} from "@/lib/etos-api";

export const dynamic = "force-dynamic";

type TaskDetailPageProps = {
  params: Promise<{ artifactId: string }>;
};

export default async function TaskDetailPage({ params }: TaskDetailPageProps) {
  const { artifactId } = await params;
  const list = await getReviewTaskArtifacts();
  const artifact = list.data?.find((item) => item.id === artifactId);
  if (!artifact) {
    notFound();
  }

  const versions = await getArtifactVersions(artifactId);
  const versionId = versions.data?.[0]?.id;
  if (!versionId) {
    notFound();
  }

  const payloadResult = await getReviewTaskPayload(artifactId, versionId);
  if (!payloadResult.data) {
    return (
      <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
        <div className="mx-auto max-w-4xl rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
          {payloadResult.error ?? "Review task payload could not be loaded."}
        </div>
      </main>
    );
  }

  const payload = payloadResult.data;

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-6xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-8">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p className="text-sm uppercase tracking-wide text-cyan-300">Review task</p>
              <h1 className="mt-2 text-4xl font-semibold">{payload.title}</h1>
              <p className="mt-3 text-slate-400">
                {payload.status} · {payload.priority} · {payload.reviewTaskType}
              </p>
              <p className="mt-2 font-mono text-xs text-slate-500">
                {artifactId} / {versionId}
              </p>
              {payload.blockingReason ? (
                <p className="mt-2 text-sm text-amber-200">Blocked: {payload.blockingReason}</p>
              ) : null}
            </div>
            <div className="flex flex-wrap gap-3">
              <ExplorerNavLink href="/tasks">Task inbox</ExplorerNavLink>
              {payload.recommendationArtifactId ? (
                <ExplorerNavLink href={`/recommendations/${payload.recommendationArtifactId}`}>
                  Source recommendation
                </ExplorerNavLink>
              ) : null}
            </div>
          </div>
        </section>

        <ReviewTaskDetailDebugPanel artifactId={artifactId} versionId={versionId} initialPayload={payload} />

        <section className="grid gap-6 lg:grid-cols-2">
          <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-2xl font-semibold">Evidence</h2>
            <ul className="mt-4 space-y-3 text-sm text-slate-300">
              {payload.evidenceReferences.length > 0 ? (
                payload.evidenceReferences.map((link) => (
                  <li key={link.linkId} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
                    <p className="font-semibold text-slate-100">{link.evidenceType}</p>
                    <p className="mt-1">{link.safeSummary}</p>
                    <p className="mt-2 text-xs text-slate-500">{link.sourceId}</p>
                  </li>
                ))
              ) : (
                <li className="text-slate-500">No evidence references.</li>
              )}
            </ul>
          </div>

          <div className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
            <h2 className="text-2xl font-semibold">Chain &amp; prerequisites</h2>
            {payload.prerequisiteTaskIds.length > 0 ? (
              <ul className="mb-4 space-y-2 text-sm text-slate-300">
                {payload.prerequisiteTaskIds.map((taskId) => (
                  <li key={taskId}>
                    Prerequisite:{" "}
                    <Link href={`/tasks/${taskId}`} className="text-cyan-300 hover:underline">
                      {taskId}
                    </Link>
                  </li>
                ))}
              </ul>
            ) : null}
            {payload.chainLinks.length > 0 ? (
              <ul className="space-y-3 text-sm text-slate-300">
                {payload.chainLinks.map((link) => (
                  <li key={link.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
                    <p>
                      Blocked{" "}
                      <Link href={`/tasks/${link.blockedTaskArtifactId}`} className="text-cyan-300">
                        {link.blockedTaskArtifactId}
                      </Link>{" "}
                      by{" "}
                      <Link href={`/tasks/${link.blockingTaskArtifactId}`} className="text-cyan-300">
                        {link.blockingTaskArtifactId}
                      </Link>
                    </p>
                    <p className="mt-1 text-xs uppercase text-slate-500">
                      {link.chainReason} · {link.resolvedAt ? "resolved" : "active"}
                    </p>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-sm text-slate-500">No chain links.</p>
            )}
          </div>
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="text-2xl font-semibold">Comments</h2>
          {payload.comments.length > 0 ? (
            <ul className="mt-4 space-y-3 text-sm text-slate-300">
              {payload.comments.map((comment) => (
                <li key={comment.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
                  <p>{comment.body}</p>
                  <p className="mt-2 text-xs text-slate-500">
                    {comment.authorUserId} · {comment.createdAt}
                  </p>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-4 text-sm text-slate-500">No comments yet. Add via debug panel.</p>
          )}
        </section>
      </div>
    </main>
  );
}
