import {
  adminUserId,
  getDigitalThreadBranches,
  getDigitalThreadEvents,
  getDigitalThreadMinimap,
  getDigitalThreadSettings,
  getDigitalThreadSummary,
  getDigitalThreadSystems,
  resolveSelectedTenantId,
} from "@/lib/etos-api";
import {
  digitalThreadPreviewBranches,
  digitalThreadPreviewEvents,
  digitalThreadPreviewMinimap,
  digitalThreadPreviewSummary,
  digitalThreadPreviewSystems,
} from "@/lib/ui-fixtures/digital-thread-timeline";
import { DigitalThreadTimelineWorkspace } from "@/components/digital-thread/DigitalThreadTimelineWorkspace";

export const dynamic = "force-dynamic";

export default async function DigitalThreadTimelinePage() {
  const [settings, tenantId] = await Promise.all([
    getDigitalThreadSettings(),
    resolveSelectedTenantId(),
  ]);

  // Default to preview when settings unavailable or UseLiveProjection=false.
  const useLiveProjection = settings.data?.useLiveProjection === true;

  if (!useLiveProjection) {
    return (
      <main className="min-h-full bg-etos-ops-canvas px-5 py-6">
        <div className="mx-auto max-w-[1600px]">
          <DigitalThreadTimelineWorkspace
            summary={digitalThreadPreviewSummary}
            systems={digitalThreadPreviewSystems}
            events={digitalThreadPreviewEvents}
            branches={digitalThreadPreviewBranches}
            minimap={digitalThreadPreviewMinimap}
            loadError={null}
            useLiveProjection={false}
            auth={{
              userId: adminUserId,
              tenantId,
            }}
          />
        </div>
      </main>
    );
  }

  const [summary, systems, events, branches, minimap] = await Promise.all([
    getDigitalThreadSummary(24),
    getDigitalThreadSystems(),
    getDigitalThreadEvents({ limit: 100 }),
    getDigitalThreadBranches({ windowHours: 24 }),
    getDigitalThreadMinimap(24),
  ]);

  const loadError =
    summary.error ??
    systems.error ??
    events.error ??
    branches.error ??
    minimap.error ??
    null;

  return (
    <main className="min-h-full bg-etos-ops-canvas px-5 py-6">
      <div className="mx-auto max-w-[1600px]">
        <DigitalThreadTimelineWorkspace
          summary={summary.data}
          systems={systems.data ?? []}
          events={events.data ?? []}
          branches={branches.data ?? []}
          minimap={minimap.data}
          loadError={loadError}
          useLiveProjection
          auth={{
            userId: adminUserId,
            tenantId,
          }}
        />
      </div>
    </main>
  );
}
