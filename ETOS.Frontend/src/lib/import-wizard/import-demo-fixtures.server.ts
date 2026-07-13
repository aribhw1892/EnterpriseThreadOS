import { readFile } from "node:fs/promises";
import path from "node:path";
import type { ImportManifest } from "@/lib/import-wizard/import-profile-types";
import { resolveManufacturingReferenceRoot, resolveRepoRoot } from "@/lib/import-wizard/package-root.server";

export async function readDemoCsv(
  demoImportDir: string,
  fileName: string,
): Promise<{ data: string | null; error: string | null }> {
  try {
    const root = resolveManufacturingReferenceRoot();
    const filePath = path.join(root, demoImportDir, fileName);
    const csv = await readFile(filePath, "utf8");
    return { data: csv, error: null };
  } catch (error) {
    const message = error instanceof Error ? error.message : "Failed to read demo CSV.";
    return {
      data: null,
      error: `Could not read packages/manufacturing-reference/${demoImportDir}/${fileName}: ${message}`,
    };
  }
}

export async function readHelpersManifest(
  helpersManifestPath: string,
  demoImportDir: string,
): Promise<{
  data: ImportManifest | null;
  error: string | null;
}> {
  try {
    const repoRoot = resolveRepoRoot();
    const packageRoot = resolveManufacturingReferenceRoot();
    const helpersManifest = path.join(repoRoot, helpersManifestPath);
    const packageManifest = path.join(packageRoot, demoImportDir, "manifest.json");

    let raw: string;
    try {
      raw = await readFile(helpersManifest, "utf8");
    } catch {
      raw = await readFile(packageManifest, "utf8");
    }

    return { data: JSON.parse(raw) as ImportManifest, error: null };
  } catch {
    return { data: null, error: null };
  }
}

export function parseManifestJson(raw: string): { data: ImportManifest | null; error: string | null } {
  try {
    return { data: JSON.parse(raw) as ImportManifest, error: null };
  } catch {
    return { data: null, error: "Uploaded manifest.json is not valid JSON." };
  }
}
