"use client";

import type { ImportFileProfile, ImportManifest } from "@/lib/import-wizard/import-profile-types";

type TransformUploadProps = {
  profiles: ImportFileProfile[];
  manifest?: ImportManifest | null;
};

export function TransformUpload({ profiles, manifest }: TransformUploadProps) {
  return (
    <div className="grid gap-4">
      {manifest?.outputs ? (
        <div className="rounded-2xl border border-cyan-400/30 bg-slate-900 p-4 text-sm text-slate-200">
          <p className="font-semibold text-cyan-200">Manifest preview</p>
          <ul className="mt-2 grid gap-1 text-xs text-slate-300">
            {Object.entries(manifest.outputs).map(([file, count]) => (
              <li key={file}>
                {file}: {count} rows
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      <p className="text-sm text-slate-400">
        Upload each transformed CSV in Step 4, or use demo fixtures / one-click demo import.
      </p>

      <ul className="grid gap-2 text-xs text-slate-500">
        {profiles.map((profile) => (
          <li key={profile.key} className="rounded-xl border border-slate-800 bg-slate-950 px-4 py-3">
            <span className="font-mono text-cyan-200">{profile.fileName}</span>
            <span className="ml-2 uppercase tracking-wide text-slate-500">{profile.kind}</span>
            {profile.structuralRelationshipType ? (
              <span className="ml-2 text-amber-300">{profile.structuralRelationshipType}</span>
            ) : null}
          </li>
        ))}
      </ul>
    </div>
  );
}
