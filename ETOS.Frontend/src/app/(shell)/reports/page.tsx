import Link from "next/link";
import { Button } from "@/components/ui/Button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/Card";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { PageHeader } from "@/components/ui/PageHeader";
import { Quote } from "@/components/ui/SidePanel";
import { getReportArtifacts } from "@/lib/etos-api";

export const dynamic = "force-dynamic";

export default async function ReportsPage() {
  const artifacts = await getReportArtifacts();
  const first = artifacts.data?.[0];

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        title="Report builder preview"
        description="Structured ReportVersion draft with evidence references, narrative sections, tables, and export governance."
        actions={
          <>
            <Link href="/chat">
              <Button variant="primary">Draft from chat</Button>
            </Link>
            <Button type="button" variant="ghost" disabled>
              Save version
            </Button>
            <Button type="button" variant="ghost" disabled>
              Request approval
            </Button>
          </>
        }
      />

      {artifacts.error ? <ErrorState error={artifacts.error} /> : null}

      <div className="grid gap-4 lg:grid-cols-[1fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Report outline</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2.5">
              {[
                {
                  num: "1",
                  title: "Executive summary",
                  sub: "AI-generated, evidence-backed summary of BOM sync risk.",
                },
                {
                  num: "2",
                  title: "Affected assemblies",
                  sub: "Governed query output, permission-filtered.",
                },
                {
                  num: "3",
                  title: "Evidence appendix",
                  sub: "Trace, context package, source import files, document refs.",
                },
              ].map((item) => (
                <div
                  key={item.num}
                  className="flex items-start gap-3 rounded-etos-card border border-etos-border-soft bg-etos-panel-muted p-3"
                >
                  <div className="flex h-[34px] w-[34px] flex-shrink-0 items-center justify-center rounded-xl bg-etos-info-bg font-black text-etos-info-fg">
                    {item.num}
                  </div>
                  <div>
                    <p className="text-[13px] font-extrabold text-etos-ink">
                      {item.title}
                    </p>
                    <p className="mt-0.5 text-xs leading-snug text-etos-ink-muted">
                      {item.sub}
                    </p>
                  </div>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Report canvas</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="rounded-etos-card border-2 border-dashed border-etos-border-soft bg-etos-panel-muted p-4">
              <h3 className="mb-2 text-base font-bold text-etos-ink">
                {first?.name ?? "BOM Synchronization Risk Report"}
              </h3>
              <p className="mb-4 text-xs text-etos-ink-muted">
                {first?.description ??
                  "Generated from QueryIntent and ContextPackage. Sensitive supplier content excluded from LLM-visible context."}
              </p>
              <div className="h-px bg-etos-border-soft" />
              <Quote>
                <p className="mt-3">
                  Evidence-backed narrative from governed retrieval. Open the report detail
                  for live template blocks, export governance, and AI Trace links.
                </p>
              </Quote>
              <div className="mt-4 flex flex-wrap gap-2">
                {first ? (
                  <Link href={`/reports/${first.id}`}>
                    <Button variant="primary">Open report detail</Button>
                  </Link>
                ) : (
                  <Button variant="primary" disabled>
                    Preview export
                  </Button>
                )}
                <Link href="/ai-traces">
                  <Button variant="ghost">View traces</Button>
                </Link>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      <details className="mt-6 rounded-etos-card border border-etos-border bg-etos-panel-muted p-4">
        <summary className="mb-4 cursor-pointer text-sm font-extrabold text-etos-ink">
          Advanced / Debug — report registry
        </summary>
        {artifacts.data && artifacts.data.length > 0 ? (
          <ul className="space-y-3">
            {artifacts.data.map((artifact) => (
              <li key={artifact.id}>
                <Link
                  href={`/reports/${artifact.id}`}
                  className="block rounded-etos-card border border-etos-border bg-etos-panel p-4 transition hover:border-etos-accent"
                >
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <p className="font-semibold text-etos-ink">{artifact.name}</p>
                      <p className="text-sm text-etos-ink-muted">
                        {artifact.description ?? artifact.artifactType}
                      </p>
                    </div>
                    <div className="text-right text-sm text-etos-ink-muted">
                      <p>{artifact.latestVersionLabel ?? "No version"}</p>
                    </div>
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        ) : (
          <EmptyState message="No reports yet. Draft one from governed chat." />
        )}
      </details>
    </main>
  );
}
