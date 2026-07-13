import { readFile } from "node:fs/promises";
import path from "node:path";
import { getImportSourceConfig } from "@/lib/import-wizard/import-source-config";
import type { ImportFileProfile, ImportMappingsDocument } from "@/lib/import-wizard/import-profile-types";
import { resolveManufacturingReferenceRoot } from "@/lib/import-wizard/package-root.server";

export async function loadImportMappings(mappingsFile: string): Promise<{
  data: ImportMappingsDocument | null;
  error: string | null;
}> {
  try {
    const root = resolveManufacturingReferenceRoot();
    const filePath = path.join(root, mappingsFile);
    const raw = await readFile(filePath, "utf8");
    const parsed = JSON.parse(raw) as ImportMappingsDocument;

    if (!parsed.files?.length) {
      return { data: null, error: `Import mappings file has no file profiles: ${mappingsFile}` };
    }

    return { data: parsed, error: null };
  } catch (error) {
    const message = error instanceof Error ? error.message : "Failed to load import mappings.";
    return {
      data: null,
      error: `Could not load packages/manufacturing-reference/${mappingsFile}: ${message}`,
    };
  }
}

export async function getImportProfiles(slug: string): Promise<{
  profiles: ImportFileProfile[];
  sourceSystem: string;
  error: string | null;
}> {
  const { config, error: configError } = await getImportSourceConfig(slug);
  if (!config) {
    return { profiles: [], sourceSystem: "", error: configError };
  }

  const loaded = await loadImportMappings(config.mappingsFile);
  if (!loaded.data) {
    return { profiles: [], sourceSystem: config.sourceSystem, error: loaded.error };
  }

  return {
    profiles: loaded.data.files,
    sourceSystem: loaded.data.sourceSystem || config.sourceSystem,
    error: null,
  };
}

export async function getImportProfileByKey(
  slug: string,
  profileKey: string,
): Promise<{
  profile: ImportFileProfile | null;
  error: string | null;
}> {
  const { profiles, error } = await getImportProfiles(slug);
  if (error) {
    return { profile: null, error };
  }

  const profile = profiles.find((item) => item.key === profileKey) ?? null;
  if (!profile) {
    return { profile: null, error: `Unknown import profile '${profileKey}' for source '${slug}'.` };
  }

  return { profile, error: null };
}
