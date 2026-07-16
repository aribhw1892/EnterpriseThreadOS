import { redirect } from "next/navigation";

export const dynamic = "force-dynamic";

type PageProps = {
  params: Promise<{ artifactId: string }>;
  searchParams: Promise<{ versionId?: string }>;
};

/** Thin hub — mockup 25 lives at `/edit`. */
export default async function ToolDefinitionDetailPage({ params, searchParams }: PageProps) {
  const { artifactId } = await params;
  const { versionId } = await searchParams;
  const query = versionId ? `?versionId=${encodeURIComponent(versionId)}` : "";
  redirect(`/tools/${artifactId}/edit${query}`);
}
