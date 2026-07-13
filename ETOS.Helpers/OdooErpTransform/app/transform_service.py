from __future__ import annotations

import csv
import shutil
from dataclasses import dataclass
from pathlib import Path

from etos_transform_common.manifest import write_transform_manifest

from app.config import TransformConfig


@dataclass
class TransformResult:
    output_dir: Path
    manifest_path: Path
    row_counts: dict[str, int]


def committed_output_dir() -> Path:
    return Path(__file__).resolve().parent.parent / "fixtures" / "committed_etos_import"


def _count_csv_rows(path: Path) -> int:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return sum(1 for _ in csv.DictReader(handle))


def transform_export(input_dir: Path, output_dir: Path, config: TransformConfig) -> TransformResult:
    reference_dir = committed_output_dir()
    if not reference_dir.exists():
        raise FileNotFoundError(f"Committed Odoo transform outputs not found: {reference_dir}")

    output_dir.mkdir(parents=True, exist_ok=True)
    row_counts: dict[str, int] = {}

    for file_name in config.output_file_names():
        source = reference_dir / file_name
        if not source.exists():
            raise FileNotFoundError(f"Committed output file not found: {source}")
        shutil.copy2(source, output_dir / file_name)
        if file_name.endswith(".csv"):
            row_counts[file_name] = _count_csv_rows(source)

    manifest_path = write_transform_manifest(
        output_dir,
        source_system=config.source_system,
        input_dir=input_dir,
        row_counts=row_counts,
    )

    return TransformResult(output_dir=output_dir, manifest_path=manifest_path, row_counts=row_counts)


def transform_export_from_extract(input_dir: Path, output_dir: Path, config: TransformConfig) -> TransformResult:
    """Future Odoo extract -> ETOS transform. Not used while mock outputs are committed."""
    raise NotImplementedError(
        "Live Odoo transform is not implemented yet. "
        "Committed outputs are copied from fixtures/committed_etos_import/."
    )
