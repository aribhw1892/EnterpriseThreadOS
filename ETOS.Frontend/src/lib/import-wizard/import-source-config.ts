import { readFile } from "node:fs/promises";
import path from "node:path";
import { resolveManufacturingReferenceRoot } from "@/lib/import-wizard/package-root.server";

export type ImportSourceManifestEntry = {
  slug: string;
  title: string;
  sourceSystem: string;
  mappingsFile: string;
  demoImportDir: string;
  helpersManifestPath: string;
  runbook: {
    extractReadme: string;
    transformReadme: string;
  };
};

export type ImportSourceWizardCopy = {
  slug: string;
  title: string;
  sourceSystem: string;
  wizardTitle: string;
  wizardDescription: string;
  prerequisitesHelper: string;
  importOrderDescription: string;
  demoImportButtonLabel: string;
  promoteButtonLabel: string;
  completeTitle: string;
  completeDescription: string;
  identityDescription: string;
  mappingsFile: string;
  demoImportDir: string;
  helpersManifestPath: string;
  extractIntro: string;
  transformIntro: string;
  extractCommands: string;
  transformCommands: string;
  transformOutputsNote: string;
};

type PackageManifest = {
  importSources?: ImportSourceManifestEntry[];
};

const PDM_EXTRACT = `# 1. Extract from PDM SQL
cd ETOS.Helpers\\PdmExtractor
set PDM_DB_SERVER=your-server
set PDM_DB_NAME=your-db
set PDM_DB_USER=your-user
set PDM_DB_PASSWORD=your-password
uv run pdm-extract --csv --json`;

const PDM_TRANSFORM = `# 2. Transform pdm_export to etos_import
cd ETOS.Helpers\\PdmTransform
uv sync --extra dev
uv run pdm-transform --input ..\\PdmExtractor\\pdm_export --output .\\etos_import`;

const ODOO_EXTRACT = `# 1. Mock extract (no Odoo DB) or live extract when SQL is ready
cd ETOS.Helpers\\OdooErpExtractor
uv sync
uv run odoo-erp-extract --use-mock

# Live extract (when PostgreSQL + mapping SQL are configured):
# uv sync --extra postgres
# set ODOO_DB_HOST=localhost
# set ODOO_DB_NAME=odoo
# set ODOO_DB_USER=odoo
# set ODOO_DB_PASSWORD=your-password
# uv run odoo-erp-extract --csv --json`;

const ODOO_TRANSFORM = `# 2. Copy committed transform outputs to etos_import
cd ETOS.Helpers\\OdooErpTransform
uv sync --extra dev
uv run odoo-erp-transform --input ..\\OdooErpExtractor\\odoo_export\\mock --output .\\etos_import`;

function buildWizardCopy(entry: ImportSourceManifestEntry): ImportSourceWizardCopy {
  if (entry.slug === "odoo") {
    return {
      ...entry,
      wizardTitle: "Odoo ERP Import Wizard",
      wizardDescription:
        "Use committed Odoo ERP transform outputs or local extract/transform helpers, then import four governed CSV batches with package preset mappings and optional AI mapping suggestions.",
      prerequisitesHelper: "Local OdooErpExtractor + OdooErpTransform helpers available under ETOS.Helpers.",
      importOrderDescription:
        "Import order: odoo-parts → odoo-part-versions → odoo-has-version → odoo-version-bom. One-click demo uses package presets only. Guided mode supports preset vs AI mapping review per file.",
      demoImportButtonLabel: "Run full Odoo ERP demo import (preset mappings)",
      promoteButtonLabel: "Promote ready Odoo ERP batches",
      completeTitle: "Import complete",
      completeDescription:
        "Odoo ERP batches are staged or promoted. Explore the graph, run governed chat, or link ERP nodes to existing PDM data via identity resolution.",
      identityDescription:
        "Generate identity candidates to link Odoo ERP nodes to staged SOLIDWORKS-PDM batches. Package-seeded cross-attribute rules match sourceDocumentId ↔ documentId (part) and sourcePdmVersionKey ↔ pdmVersionKey (partVersion). Approve matches to create IDENTITY_LINK edges before promotion.",
      extractIntro:
        "OdooErpExtractor uses the same XML-driven export contract as PDM. Committed mock data lives under odoo_export/mock until live PostgreSQL extraction is configured.",
      transformIntro:
        "OdooErpTransform copies committed ETOS import CSV batches from fixtures/committed_etos_import/ (or regenerates when live transform is implemented).",
      extractCommands: ODOO_EXTRACT,
      transformCommands: ODOO_TRANSFORM,
      transformOutputsNote:
        "Outputs: odoo-parts.csv, odoo-part-versions.csv, odoo-has-version.csv, odoo-version-bom.csv — SourceSystem ODOO-ERP.",
    };
  }

  return {
    ...entry,
    wizardTitle: "PDM Import Wizard",
    wizardDescription:
      "Extract and transform SolidWorks PDM data locally, then import four governed CSV batches with package preset mappings and optional AI mapping suggestions.",
    prerequisitesHelper: "Local PdmExtractor + PdmTransform helpers available under ETOS.Helpers.",
    importOrderDescription:
      "Import order: parts → part-versions → has-version → version-bom. One-click demo uses package presets only. Guided mode supports preset vs AI mapping review per file.",
    demoImportButtonLabel: "Run full PDM demo import (preset mappings)",
    promoteButtonLabel: "Promote ready PDM batches",
    completeTitle: "Import complete",
    completeDescription:
      "PDM batches are staged or promoted. Explore the graph, run governed chat, or use the generic import hub for latest-batch debugging.",
    identityDescription:
      "Pure PDM imports may have few identity candidates. Generate and review links when comparing systems or after importing ERP data.",
    extractIntro:
      "PdmExtractor runs locally against your SolidWorks PDM SQL database. See ETOS.Helpers/PdmExtractor/README.md for connection details.",
    transformIntro:
      "PdmTransform maps pdm_export/ into four import CSV batches plus manifest.json.",
    extractCommands: PDM_EXTRACT,
    transformCommands: PDM_TRANSFORM,
    transformOutputsNote:
      "Outputs: parts.csv, part-versions.csv, has-version.csv, version-bom.csv — SourceSystem SOLIDWORKS-PDM.",
  };
}

export async function loadPackageManifest(): Promise<{ data: PackageManifest | null; error: string | null }> {
  try {
    const root = resolveManufacturingReferenceRoot();
    const raw = await readFile(path.join(root, "package.manifest.json"), "utf8");
    return { data: JSON.parse(raw) as PackageManifest, error: null };
  } catch (error) {
    const message = error instanceof Error ? error.message : "Failed to load package manifest.";
    return { data: null, error: message };
  }
}

export async function getImportSourceConfig(slug: string): Promise<{
  config: ImportSourceWizardCopy | null;
  error: string | null;
}> {
  const loaded = await loadPackageManifest();
  if (!loaded.data?.importSources?.length) {
    return { config: null, error: loaded.error ?? "Package manifest has no importSources registry." };
  }

  const entry = loaded.data.importSources.find((item) => item.slug === slug);
  if (!entry) {
    return { config: null, error: `Unknown import source slug '${slug}'.` };
  }

  return { config: buildWizardCopy(entry), error: null };
}

export function getImportWizardBasePath(slug: string): string {
  return `/imports/${slug}`;
}
