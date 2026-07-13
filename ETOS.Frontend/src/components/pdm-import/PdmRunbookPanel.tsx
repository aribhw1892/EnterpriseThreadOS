"use client";

import { RunbookPanel } from "@/components/import-wizard/RunbookPanel";
import type { ImportSourceWizardCopy } from "@/lib/import-wizard/import-source-config";

const PDM_COPY: Pick<
  ImportSourceWizardCopy,
  "extractIntro" | "transformIntro" | "extractCommands" | "transformCommands" | "transformOutputsNote"
> = {
  extractIntro:
    "PdmExtractor runs locally against your SolidWorks PDM SQL database. See ETOS.Helpers/PdmExtractor/README.md for connection details.",
  transformIntro:
    "PdmTransform maps pdm_export/ into four import CSV batches plus manifest.json.",
  extractCommands: `# 1. Extract from PDM SQL
cd ETOS.Helpers\\PdmExtractor
set PDM_DB_SERVER=your-server
set PDM_DB_NAME=your-db
set PDM_DB_USER=your-user
set PDM_DB_PASSWORD=your-password
uv run pdm-extract --csv --json`,
  transformCommands: `# 2. Transform pdm_export to etos_import
cd ETOS.Helpers\\PdmTransform
uv sync --extra dev
uv run pdm-transform --input ..\\PdmExtractor\\pdm_export --output .\\etos_import`,
  transformOutputsNote:
    "Outputs: parts.csv, part-versions.csv, has-version.csv, version-bom.csv — SourceSystem SOLIDWORKS-PDM.",
};

export function PdmRunbookPanel({ phase }: { phase: "extract" | "transform" }) {
  return <RunbookPanel phase={phase} copy={PDM_COPY} />;
}
