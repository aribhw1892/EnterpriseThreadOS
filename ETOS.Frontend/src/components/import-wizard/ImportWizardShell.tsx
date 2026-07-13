"use client";

import Link from "next/link";
import type { ReactNode } from "react";

const STEPS = [
  { id: "1", label: "Prerequisites" },
  { id: "2", label: "Extract" },
  { id: "3", label: "Transform" },
  { id: "4", label: "Import" },
  { id: "5", label: "Identity" },
  { id: "6", label: "Promote" },
  { id: "7", label: "Complete" },
] as const;

type ImportWizardShellProps = {
  basePath: string;
  currentStep: string;
  batches?: string;
  mode?: string;
  children: ReactNode;
};

export function ImportWizardShell({ basePath, currentStep, batches, mode, children }: ImportWizardShellProps) {
  return (
    <div className="grid gap-8">
      <nav className="flex flex-wrap gap-2">
        {STEPS.map((step) => {
          const isActive = step.id === currentStep;
          const params = new URLSearchParams();
          params.set("step", step.id);
          if (batches) {
            params.set("batches", batches);
          }
          if (mode) {
            params.set("mode", mode);
          }

          return (
            <Link
              key={step.id}
              href={`${basePath}?${params.toString()}`}
              className={`rounded-full px-4 py-2 text-xs font-semibold uppercase tracking-wide ${
                isActive
                  ? "bg-cyan-300 text-slate-950"
                  : "border border-slate-700 text-slate-400 hover:border-cyan-400/40 hover:text-cyan-200"
              }`}
            >
              {step.id}. {step.label}
            </Link>
          );
        })}
      </nav>
      {children}
    </div>
  );
}
