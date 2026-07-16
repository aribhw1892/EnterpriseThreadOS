import { notFound } from "next/navigation";
import { Badge, StatusBadge, type BadgeVariant } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { EmptyState } from "@/components/ui/EmptyState";
import { ErrorState } from "@/components/ui/ErrorState";
import { KpiCard } from "@/components/ui/KpiCard";
import { PageHeader } from "@/components/ui/PageHeader";

const badgeVariants: BadgeVariant[] = [
  "success",
  "warning",
  "danger",
  "info",
  "purple",
  "teal",
  "neutral",
];

type SampleRow = {
  id: string;
  name: string;
  type: string;
  status: string;
};

const sampleRows: SampleRow[] = [
  { id: "1", name: "P-1842 Hydraulic Pump", type: "GraphNode", status: "Trusted" },
  { id: "2", name: "BOM mismatch detected", type: "Recommendation", status: "Blocked" },
  { id: "3", name: "Q3 supplier review", type: "ReviewTask", status: "Pending" },
];

export default function UiKitPage() {
  if (process.env.NODE_ENV !== "development") {
    notFound();
  }

  return (
    <main className="min-h-screen bg-etos-canvas px-8 py-10">
      <div className="mx-auto flex max-w-5xl flex-col gap-8">
        <PageHeader
          eyebrow="Dev only"
          title="ETOS UI kit"
          description="Token-based primitives from UI-0.3. Toggle the theme in the shell topbar (or via devtools .dark class on <html>) to verify both palettes."
          actions={<Badge variant="info">UI-0.3</Badge>}
        />

        <Card>
          <CardHeader>
            <CardTitle>Badges</CardTitle>
            <CardDescription>Semantic status variants; label text always included.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-3">
            {badgeVariants.map((variant) => (
              <Badge key={variant} variant={variant}>
                {variant}
              </Badge>
            ))}
            <StatusBadge status="healthy" />
            <StatusBadge status="staged" />
            <StatusBadge status="blocked" />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Buttons</CardTitle>
            <CardDescription>Primary gradient, ghost, danger, and disabled states.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-wrap gap-3">
            <Button>Primary action</Button>
            <Button variant="ghost">Ghost action</Button>
            <Button variant="danger">Danger action</Button>
            <Button disabled title="Requires Issue 16.1">
              Disabled action
            </Button>
          </CardContent>
        </Card>

        <section aria-label="KPI cards" className="grid gap-4 md:grid-cols-3">
          <KpiCard label="Thread health" value="94%" trend="up" trendLabel="Healthy" />
          <KpiCard label="Open decisions" value={9} trend="warn" trendLabel="2 due soon" />
          <KpiCard label="Events / min" value="—" hint="Not provided by API" />
        </section>

        <section aria-label="Ops KPI cards" className="grid gap-4 rounded-etos-card bg-etos-ops-canvas p-4 md:grid-cols-3">
          <KpiCard ops label="Thread health" value="94%" trend="up" trendLabel="Healthy" />
          <KpiCard ops label="Recommendations" value={23} trend="warn" trendLabel="7 pending review" />
          <KpiCard ops label="Active agents" value="—" hint="Requires Issue 16.1" />
        </section>

        <Card>
          <CardHeader>
            <CardTitle>Data table</CardTitle>
            <CardDescription>Simple token-styled table (TanStack wrapper comes later).</CardDescription>
          </CardHeader>
          <CardContent>
            <DataTable<SampleRow>
              columns={[
                { key: "name", header: "Name", render: (row) => row.name },
                { key: "type", header: "Type", render: (row) => row.type },
                {
                  key: "status",
                  header: "Status",
                  render: (row) => <StatusBadge status={row.status} />,
                },
              ]}
              rows={sampleRows}
              rowKey={(row) => row.id}
            />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Empty and error states</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-3">
            <EmptyState message="No artifacts are available for the selected tenant." />
            <ErrorState error="Backend health is unavailable. Start the backend and refresh." />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Tabs</CardTitle>
            <CardDescription>URL-param driven tablist (UI-1.10).</CardDescription>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-etos-ink-muted">
              See <code>/admin/identity?tab=users</code> for live Tabs usage.
            </p>
          </CardContent>
        </Card>
      </div>
    </main>
  );
}
