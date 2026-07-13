import {
  getImportProfileByKey,
  getImportProfiles,
  loadImportMappings,
} from "@/lib/import-wizard/import-config.server";

export async function getPdmImportProfiles() {
  return getImportProfiles("pdm");
}

export async function getPdmImportProfileByKey(profileKey: string) {
  return getImportProfileByKey("pdm", profileKey);
}

/** @deprecated Use loadImportMappings("profiles/pdm-import-mappings.json") */
export async function loadPdmImportMappings() {
  return loadImportMappings("profiles/pdm-import-mappings.json");
}
