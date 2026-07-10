"use client";

const EXTRACT_COMMANDS = `# 1. Extract from PDM SQL
cd ETOS.Helpers\\PdmExtractor
set PDM_DB_SERVER=your-server
set PDM_DB_NAME=your-db
set PDM_DB_USER=your-user
set PDM_DB_PASSWORD=your-password
uv run pdm-extract --csv --json`;

const TRANSFORM_COMMANDS = `# 2. Transform pdm_export to etos_import
cd ETOS.Helpers\\PdmTransform
uv sync --extra dev
uv run pdm-transform --input ..\\PdmExtractor\\pdm_export --output .\\etos_import`;

function CommandBlock({ title, commands }: { title: string; commands: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <h3 className="text-sm font-semibold text-cyan-300">{title}</h3>
      <pre className="mt-3 overflow-x-auto whitespace-pre-wrap text-xs text-slate-300">{commands}</pre>
    </div>
  );
}

export function PdmRunbookPanel({ phase }: { phase: "extract" | "transform" }) {
  if (phase === "extract") {
    return (
      <div className="grid gap-4">
        <p className="text-sm text-slate-300">
          PdmExtractor runs locally against your SolidWorks PDM SQL database. See{" "}
          <code className="text-cyan-200">ETOS.Helpers/PdmExtractor/README.md</code> for connection details.
        </p>
        <CommandBlock title="Extract commands" commands={EXTRACT_COMMANDS} />
      </div>
    );
  }

  return (
    <div className="grid gap-4">
      <p className="text-sm text-slate-300">
        PdmTransform maps <code className="text-cyan-200">pdm_export/</code> into four import CSV batches plus{" "}
        <code className="text-cyan-200">manifest.json</code>.
      </p>
      <CommandBlock title="Transform commands" commands={TRANSFORM_COMMANDS} />
      <p className="text-xs text-slate-500">
        Outputs: parts.csv, part-versions.csv, has-version.csv, version-bom.csv — SourceSystem SOLIDWORKS-PDM.
      </p>
    </div>
  );
}
