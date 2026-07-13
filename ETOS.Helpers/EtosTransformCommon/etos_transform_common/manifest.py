from __future__ import annotations

import json
from datetime import UTC, datetime
from pathlib import Path


def write_transform_manifest(
    output_dir: Path,
    *,
    source_system: str,
    input_dir: Path,
    row_counts: dict[str, int],
) -> Path:
    manifest = {
        "generatedAt": datetime.now(UTC).isoformat(),
        "sourceSystem": source_system,
        "inputDir": str(input_dir),
        "outputs": row_counts,
    }
    manifest_path = output_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return manifest_path
