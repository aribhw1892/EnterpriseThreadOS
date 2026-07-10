import { readFile } from "node:fs/promises";
import path from "node:path";
import type { PdmImportFileProfile, PdmImportMappingsDocument } from "@/lib/pdm-import-types";
import { PDM_SOURCE_SYSTEM } from "@/lib/pdm-import-types";

function resolveManufacturingReferenceRoot(): string {
  const candidates = [
    path.join(process.cwd(), "..", "packages", "manufacturing-reference"),
    path.join(process.cwd(), "packages", "manufacturing-reference"),
  ];

  return candidates[0];
}

export async function loadPdmImportMappings(): Promise<{
  data: PdmImportMappingsDocument | null;
  error: string | null;
}> {
  try {
    const root = resolveManufacturingReferenceRoot();
    const filePath = path.join(root, "profiles", "pdm-import-mappings.json");
    const raw = await readFile(filePath, "utf8");
    const parsed = JSON.parse(raw) as PdmImportMappingsDocument;

    if (!parsed.files?.length) {
      return { data: null, error: "PDM import mappings file has no file profiles." };
    }

    return { data: parsed, error: null };
  } catch (error) {
    const message = error instanceof Error ? error.message : "Failed to load PDM import mappings.";
    return {
      data: null,
      error: `Could not load packages/manufacturing-reference/profiles/pdm-import-mappings.json: ${message}`,
    };
  }
}

export async function getPdmImportProfiles(): Promise<{
  profiles: PdmImportFileProfile[];
  sourceSystem: string;
  error: string | null;
}> {
  const loaded = await loadPdmImportMappings();
  if (!loaded.data) {
    return { profiles: [], sourceSystem: PDM_SOURCE_SYSTEM, error: loaded.error };
  }

  return {
    profiles: loaded.data.files,
    sourceSystem: loaded.data.sourceSystem || PDM_SOURCE_SYSTEM,
    error: null,
  };
}

export async function getPdmImportProfileByKey(profileKey: string): Promise<{
  profile: PdmImportFileProfile | null;
  error: string | null;
}> {
  const { profiles, error } = await getPdmImportProfiles();
  if (error) {
    return { profile: null, error };
  }

  const profile = profiles.find((item) => item.key === profileKey) ?? null;
  if (!profile) {
    return { profile: null, error: `Unknown PDM import profile '${profileKey}'.` };
  }

  return { profile, error: null };
}
