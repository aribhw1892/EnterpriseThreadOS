import Image from "next/image";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { PageHeader } from "@/components/ui/PageHeader";

/**
 * Architecture-honest placeholder for routes whose backend does not exist yet
 * (UI-0.4). Never fakes success: primary action renders disabled with the
 * blocking issue as the reason.
 */
export function PlaceholderPage({
  title,
  description,
  issueBlocker,
  mockupSrc,
  mockupAlt,
  primaryAction,
}: {
  title: string;
  description: string;
  issueBlocker?: string;
  mockupSrc?: string;
  mockupAlt?: string;
  primaryAction?: { label: string; reason: string };
}) {
  return (
    <main className="px-6 py-10">
      <div className="mx-auto flex max-w-5xl flex-col gap-6">
        <PageHeader
          title={title}
          description={description}
          actions={
            <>
              {issueBlocker ? (
                <Badge variant="warning">Blocked by {issueBlocker}</Badge>
              ) : null}
              {primaryAction ? (
                <Button disabled title={primaryAction.reason}>
                  {primaryAction.label}
                </Button>
              ) : null}
            </>
          }
        />

        {mockupSrc ? (
          <figure className="overflow-hidden rounded-etos-card border border-etos-border-panel bg-etos-panel shadow-etos">
            <Image
              src={mockupSrc}
              alt={mockupAlt ?? `${title} mockup preview`}
              width={1024}
              height={640}
              className="h-auto w-full"
              data-ui-preview="true"
            />
            <figcaption className="border-t border-etos-border-soft px-4 py-3 text-xs text-etos-ink-muted">
              Design mockup preview — not live data.
              {issueBlocker ? ` Implementation blocked by ${issueBlocker}.` : ""}
            </figcaption>
          </figure>
        ) : null}
      </div>
    </main>
  );
}
