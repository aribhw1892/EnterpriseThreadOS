"use client";

import type { ImportSourceWizardCopy } from "@/lib/import-wizard/import-source-config";

function CommandBlock({ title, commands }: { title: string; commands: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <h3 className="text-sm font-semibold text-cyan-300">{title}</h3>
      <pre className="mt-3 overflow-x-auto whitespace-pre-wrap text-xs text-slate-300">{commands}</pre>
    </div>
  );
}

type RunbookPanelProps = {
  phase: "extract" | "transform";
  copy: Pick<
    ImportSourceWizardCopy,
    "extractIntro" | "transformIntro" | "extractCommands" | "transformCommands" | "transformOutputsNote"
  >;
};

export function RunbookPanel({ phase, copy }: RunbookPanelProps) {
  if (phase === "extract") {
    return (
      <div className="grid gap-4">
        <p className="text-sm text-slate-300">{copy.extractIntro}</p>
        <CommandBlock title="Extract commands" commands={copy.extractCommands} />
      </div>
    );
  }

  return (
    <div className="grid gap-4">
      <p className="text-sm text-slate-300">{copy.transformIntro}</p>
      <CommandBlock title="Transform commands" commands={copy.transformCommands} />
      <p className="text-xs text-slate-500">{copy.transformOutputsNote}</p>
    </div>
  );
}
