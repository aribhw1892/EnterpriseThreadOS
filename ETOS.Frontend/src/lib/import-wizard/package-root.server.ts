import path from "node:path";

export function resolveManufacturingReferenceRoot(): string {
  const candidates = [
    path.join(process.cwd(), "..", "packages", "manufacturing-reference"),
    path.join(process.cwd(), "packages", "manufacturing-reference"),
  ];

  return candidates.find((candidate) => candidate) ?? candidates[0];
}

export function resolveRepoRoot(): string {
  return path.join(process.cwd(), "..");
}
