import Link from "next/link";
import type { ContextView360 } from "@/lib/etos-api";
import { SectionVisibilityBadge } from "./SectionVisibilityBadge";

export function ContextView360({ view }: { view: ContextView360 }) {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
      <div className="mb-6">
        <p className="text-xs uppercase tracking-wide text-slate-500">{view.anchorKind}</p>
        <h2 className="mt-1 text-2xl font-semibold">{view.title}</h2>
        <p className="mt-2 text-sm text-slate-400">{view.safeSummary}</p>
        <p className="mt-3 text-xs text-slate-500">
          Visible {view.filterSummary.visibleSectionCount} · Denied {view.filterSummary.deniedSectionCount} · Empty{" "}
          {view.filterSummary.emptySectionCount}
        </p>
      </div>

      <div className="grid gap-4">
        {view.sections.map((section) => (
          <article key={section.sectionKey} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
            <div className="mb-3 flex items-center justify-between gap-3">
              <h3 className="font-semibold">{section.title}</h3>
              <SectionVisibilityBadge visibility={section.visibility} />
            </div>

            {section.deniedReason ? (
              <p className="text-sm text-rose-200">{section.deniedReason}</p>
            ) : section.items.length === 0 ? (
              <p className="text-sm text-slate-500">No items in this section.</p>
            ) : (
              <ul className="grid gap-3">
                {section.items.map((item) => (
                  <li key={`${section.sectionKey}-${item.itemId}`} className="rounded-xl border border-slate-800 p-3">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <p className="font-medium">{item.title}</p>
                        <p className="mt-1 text-sm text-slate-400">{item.safeSummary}</p>
                        <p className="mt-2 text-xs text-slate-500">{item.itemType}</p>
                      </div>
                      {item.linkRoute ? (
                        <Link href={item.linkRoute} className="text-sm font-semibold text-cyan-300 hover:text-cyan-200">
                          Open
                        </Link>
                      ) : null}
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </article>
        ))}
      </div>
    </section>
  );
}
