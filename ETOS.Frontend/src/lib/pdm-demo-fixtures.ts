import { readFile } from "node:fs/promises";
import path from "node:path";
import type { PdmImportManifest } from "@/lib/pdm-import-types";

function resolveManufacturingReferenceRoot(): string {
  const candidates = [
    path.join(process.cwd(), "..", "packages", "manufacturing-reference"),
    path.join(process.cwd(), "packages", "manufacturing-reference"),
  ];

  return candidates[0];
}

export async function readPdmDemoCsv(fileName: string): Promise<{ data: string | null; error: string | null }> {
  try {
    const root = resolveManufacturingReferenceRoot();
    const filePath = path.join(root, "demo-imports", "pdm", fileName);
    const csv = await readFile(filePath, "utf8");
    return { data: csv, error: null };
  } catch (error) {
    const message = error instanceof Error ? error.message : "Failed to read demo CSV.";
    return {
      data: null,
      error: `Could not read packages/manufacturing-reference/demo-imports/pdm/${fileName}: ${message}`,
    };
  }
}

export async function readPdmDemoManifest(): Promise<{
  data: PdmImportManifest | null;
  error: string | null;
}> {
  try {
    const root = resolveManufacturingReferenceRoot();
    const helpersManifest = path.join(process.cwd(), "..", "ETOS.Helpers", "PdmTransform", "etos_import", "manifest.json");
    const packageManifest = path.join(root, "demo-imports", "pdm", "manifest.json");

    let raw: string;
    try {
      raw = await readFile(helpersManifest, "utf8");
    } catch {
      raw = await readFile(packageManifest, "utf8");
    }

    return { data: JSON.parse(raw) as PdmImportManifest, error: null };
  } catch {
    return { data: null, error: null };
  }
}

export function parsePdmManifestJson(raw: string): { data: PdmImportManifest | null; error: string | null } {
  try {
    return { data: JSON.parse(raw) as PdmImportManifest, error: null };
  } catch {
    return { data: null, error: "Uploaded manifest.json is not valid JSON." };
  }
}
