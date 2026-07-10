from __future__ import annotations

from pathlib import Path

from app.config import TransformConfig, VariablePivotConfig
from app.transform_service import transform_export


def test_transform_export_writes_four_batches(tmp_path: Path) -> None:
    fixture_dir = Path(__file__).parent / "fixtures" / "pdm_export"
    output_dir = tmp_path / "etos_import"
    config = TransformConfig(
        variable_pivot=VariablePivotConfig(
            enabled=True,
            columns=["Description", "Material"],
        )
    )

    result = transform_export(fixture_dir, output_dir, config)

    assert result.row_counts["parts.csv"] == 2
    assert result.row_counts["part-versions.csv"] == 2
    assert result.row_counts["has-version.csv"] == 2
    assert result.row_counts["version-bom.csv"] == 1
    assert (output_dir / "manifest.json").exists()

    part_versions = (output_dir / "part-versions.csv").read_text(encoding="utf-8")
    assert "6-4" in part_versions
    assert "Bracket housing" in part_versions
    assert "Steel" in part_versions

    has_version = (output_dir / "has-version.csv").read_text(encoding="utf-8")
    assert "15,15-10" in has_version.replace("\r\n", "\n")
    assert "6,6-4" in has_version.replace("\r\n", "\n")

    version_bom = (output_dir / "version-bom.csv").read_text(encoding="utf-8")
    assert "15-10,6-4,2" in version_bom.replace("\r\n", "\n")
