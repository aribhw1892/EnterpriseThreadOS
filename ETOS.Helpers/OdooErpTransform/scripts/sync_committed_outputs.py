import csv
import json
import shutil
from datetime import UTC, datetime
from pathlib import Path

root = Path(__file__).resolve().parent.parent
etos = root / "etos_import"
fixtures = root / "fixtures" / "committed_etos_import"
demo = Path(__file__).resolve().parents[3] / "packages" / "manufacturing-reference" / "demo-imports" / "odoo"
profiles = Path(__file__).resolve().parents[3] / "packages" / "manufacturing-reference" / "profiles"
fixtures.mkdir(parents=True, exist_ok=True)
demo.mkdir(parents=True, exist_ok=True)

versions = list(csv.DictReader((etos / "odoo-part-versions.csv").open(encoding="utf-8")))
has_rows = []
for i, row in enumerate(versions, start=1):
    has_rows.append(
        {
            "relationId": f"ODOO-HASVER-{i:07d}",
            "parent": row["odooProductId"].strip(),
            "child": row["odooVersionKey"].strip(),
            "revision": row["revision"].strip(),
            "isCurrent": row["isCurrent"].strip(),
            "active": "TRUE",
        }
    )

with (etos / "odoo-has-version.csv").open("w", encoding="utf-8", newline="") as f:
    writer = csv.DictWriter(f, fieldnames=["relationId", "parent", "child", "revision", "isCurrent", "active"])
    writer.writeheader()
    writer.writerows(has_rows)

outputs = [
    "odoo-parts.csv",
    "odoo-part-versions.csv",
    "odoo-has-version.csv",
    "odoo-version-bom.csv",
    "odoo-identifiers-and-mappings.json",
]
for name in outputs:
    src = etos / name
    shutil.copy2(src, fixtures / name)
    if name.endswith(".csv"):
        shutil.copy2(src, demo / name)

shutil.copy2(etos / "odoo-identifiers-and-mappings.json", profiles / "odoo-import-mappings.json")

counts = {
    name: sum(1 for _ in csv.DictReader((fixtures / name).open(encoding="utf-8")))
    for name in outputs
    if name.endswith(".csv")
}
manifest = {
    "generatedAt": datetime.now(UTC).isoformat(),
    "sourceSystem": "ODOO-ERP",
    "inputDir": "committed-upload",
    "outputs": counts,
}
for target in [etos, fixtures, demo]:
    (target / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")

for stale in ["parts.csv", "ebom.csv"]:
    for directory in [etos, fixtures, demo]:
        path = directory / stale
        if path.exists():
            path.unlink()

print("has_version", len(has_rows), "counts", counts)
