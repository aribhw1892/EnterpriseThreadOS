import { RecommendationDetailView, loadRecommendationDetail } from "@/components/recommendations/RecommendationDetailView";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ artifactId: string }>;
  searchParams: Promise<{ versionId?: string }>;
};

export default async function RecommendationDetailPage({ params, searchParams }: PageProps) {
  const { artifactId } = await params;
  const { versionId } = await searchParams;
  const detail = await loadRecommendationDetail(artifactId, versionId);

  if (!detail.data) {
    return (
      <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
        <div className="mx-auto max-w-3xl rounded-3xl border border-amber-500/30 bg-amber-500/10 p-6 text-sm text-amber-100">
          {detail.error ?? "Recommendation was not found."}
        </div>
      </main>
    );
  }

  return (
    <RecommendationDetailView
      artifactId={artifactId}
      versionId={detail.data.versionId}
      artifactName={detail.data.artifactName}
      payload={detail.data.payload}
      readiness={detail.data.readiness}
      dependencyCount={detail.data.dependencyCount}
    />
  );
}
