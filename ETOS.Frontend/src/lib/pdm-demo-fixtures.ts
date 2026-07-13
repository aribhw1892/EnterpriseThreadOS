export { readDemoCsv as readPdmDemoCsv, parseManifestJson as parsePdmManifestJson } from "@/lib/import-wizard/import-demo-fixtures.server";

/** @deprecated Use readHelpersManifest with package manifest paths */
export async function readPdmDemoManifest() {
  const { readHelpersManifest } = await import("@/lib/import-wizard/import-demo-fixtures.server");
  return readHelpersManifest(
    "ETOS.Helpers/PdmTransform/etos_import/manifest.json",
    "demo-imports/pdm",
  );
}
