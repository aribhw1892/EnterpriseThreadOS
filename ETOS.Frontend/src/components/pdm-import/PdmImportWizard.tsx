"use client";

import { ImportWizardShell } from "@/components/import-wizard/ImportWizardShell";
import type { ReactNode } from "react";

type PdmImportWizardProps = {
  currentStep: string;
  batches?: string;
  mode?: string;
  children: ReactNode;
};

export function PdmImportWizard({ currentStep, batches, mode, children }: PdmImportWizardProps) {
  return (
    <ImportWizardShell basePath="/imports/pdm" currentStep={currentStep} batches={batches} mode={mode}>
      {children}
    </ImportWizardShell>
  );
}
