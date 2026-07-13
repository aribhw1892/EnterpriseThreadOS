import { getImportWizardBasePath } from "@/lib/import-wizard/import-source-config";

export type ImportWizardSearchParams = {
  step?: string;
  batches?: string;
  mode?: string;
  error?: string;
  activeBatch?: string;
  activeProfile?: string;
  evidenceId?: string;
  mappingApproved?: string;
  staged?: string;
};

export function buildImportWizardRedirectPath(slug: string, params: Record<string, string | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value) {
      search.set(key, value);
    }
  }
  return `${getImportWizardBasePath(slug)}?${search.toString()}`;
}
