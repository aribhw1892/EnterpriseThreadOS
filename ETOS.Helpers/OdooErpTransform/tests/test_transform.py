from __future__ import annotations

from pathlib import Path

from app.config import TransformConfig
from app.transform_service import committed_output_dir, transform_export


def test_transform_export_copies_committed_outputs(tmp_path: Path) -> None:
    reference_dir = committed_output_dir()
    output_dir = tmp_path / "etos_import"
    config = TransformConfig()

    result = transform_export(reference_dir, output_dir, config)

    assert result.row_counts["odoo-parts.csv"] == 107
    assert result.row_counts["odoo-part-versions.csv"] == 479
    assert result.row_counts["odoo-has-version.csv"] == 479
    assert result.row_counts["odoo-version-bom.csv"] == 719
    assert (output_dir / "manifest.json").exists()
    assert (output_dir / "odoo-identifiers-and-mappings.json").exists()

    for file_name in (
        "odoo-parts.csv",
        "odoo-part-versions.csv",
        "odoo-has-version.csv",
        "odoo-version-bom.csv",
        "odoo-identifiers-and-mappings.json",
    ):
        assert (output_dir / file_name).read_text(encoding="utf-8") == (
            reference_dir / file_name
        ).read_text(encoding="utf-8")

    parts = (output_dir / "odoo-parts.csv").read_text(encoding="utf-8")
    assert "ODOO-PROD-001022" in parts
    assert "200081" in parts

    bom = (output_dir / "odoo-version-bom.csv").read_text(encoding="utf-8")
    assert "ODOO-VER-000015-010" in bom
    assert "ODOO-VER-000006-004" in bom

    has_version = (output_dir / "odoo-has-version.csv").read_text(encoding="utf-8")
    assert "ODOO-PROD-000002" in has_version
    assert "ODOO-VER-000002-001" in has_version
