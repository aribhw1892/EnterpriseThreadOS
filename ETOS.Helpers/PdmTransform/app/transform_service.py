from __future__ import annotations

import csv
import json
from collections import defaultdict
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path

from app.config import TransformConfig


@dataclass
class TransformResult:
    output_dir: Path
    manifest_path: Path
    row_counts: dict[str, int]


def _read_csv_rows(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return [dict(row) for row in csv.DictReader(handle)]


def _write_csv(path: Path, fieldnames: list[str], rows: list[dict[str, str]]) -> int:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow(row)
    return len(rows)


def _index_by(rows: list[dict[str, str]], key: str) -> dict[str, dict[str, str]]:
    indexed: dict[str, dict[str, str]] = {}
    for row in rows:
        value = row.get(key, "").strip()
        if value:
            indexed[value] = row
    return indexed


def _build_variable_pivot(
    version_rows: list[dict[str, str]],
    version_to_variable_rows: list[dict[str, str]],
    variable_rows: list[dict[str, str]],
    pivot_columns: list[str],
) -> dict[str, dict[str, str]]:
    if not pivot_columns:
        return {}

    variable_name_by_id = {
        row.get("Id", "").strip(): row.get("Name", "").strip() or row.get("VariableName", "").strip()
        for row in variable_rows
    }
    pivot_lookup = {name.casefold(): name for name in pivot_columns}
    values_by_version: dict[str, dict[str, str]] = defaultdict(dict)

    for link in version_to_variable_rows:
        version_key = link.get("ChildId", "").strip() or link.get("MigrationSourceId", "").strip()
        variable_id = link.get("ParentId", "").strip()
        variable_name = variable_name_by_id.get(variable_id, "")
        canonical_name = pivot_lookup.get(variable_name.casefold())
        if not version_key or not canonical_name:
            continue
        value = link.get("Value", "").strip() or link.get("VariableValue", "").strip()
        if value:
            values_by_version[version_key][canonical_name] = value

    # Ensure every version row key exists even when no pivoted values were found.
    for version in version_rows:
        version_key = version.get("MigrationSourceId", "").strip()
        if version_key:
            values_by_version.setdefault(version_key, {})

    return values_by_version


def transform_export(input_dir: Path, output_dir: Path, config: TransformConfig) -> TransformResult:
    objects_dir = input_dir / "objects"
    relationships_dir = input_dir / "relationships"

    file_rows = _read_csv_rows(objects_dir / "File.csv")
    version_rows = _read_csv_rows(objects_dir / "Version.csv")
    variable_rows = _read_csv_rows(objects_dir / "Variables.csv")
    version_to_variable_rows = _read_csv_rows(relationships_dir / "VersionToVariable.csv")
    file_to_version_rows = _read_csv_rows(relationships_dir / "FileToVersion.csv")
    version_to_version_rows = _read_csv_rows(relationships_dir / "VersionToVersion.csv")

    if config.version_filter.casefold() == "latest":
        version_rows = [row for row in version_rows if row.get("IsLatest", "").strip().upper() == "TRUE"]

    files_by_id = _index_by(file_rows, "Id")
    versions_by_key = _index_by(version_rows, "MigrationSourceId")

    parts_rows: list[dict[str, str]] = []
    for file_row in file_rows:
        document_id = file_row.get("DocumentID", "").strip()
        if not document_id:
            continue
        parts_rows.append(
            {
                "documentId": document_id,
                "fileName": file_row.get("FileName", "").strip(),
                "projectPath": file_row.get("ProjectPath", "").strip(),
                "status": "MFG",
            }
        )

    pivot_values = {}
    if config.variable_pivot.enabled:
        pivot_values = _build_variable_pivot(
            version_rows,
            version_to_variable_rows,
            variable_rows,
            config.variable_pivot.columns,
        )

    part_version_rows: list[dict[str, str]] = []
    for version_row in version_rows:
        version_key = version_row.get("MigrationSourceId", "").strip()
        if not version_key:
            continue
        row = {
            "pdmVersionKey": version_key,
            "documentId": version_row.get("DocumentID", "").strip(),
            "revision": version_row.get("RevNr", "").strip(),
            "fileName": version_row.get("FileName", "").strip(),
            "status": version_row.get("Status", "").strip() or "Project Under Design",
            "workflow": version_row.get("Workflow", "").strip(),
            "isLatest": version_row.get("IsLatest", "").strip(),
            "projectPath": version_row.get("ProjectPath", "").strip(),
        }
        row.update(pivot_values.get(version_key, {}))
        part_version_rows.append(row)

    has_version_rows: list[dict[str, str]] = []
    for link in file_to_version_rows:
        file_id = link.get("ParentId", "").strip()
        child_key = link.get("ChildId", "").strip() or link.get("MigrationSourceId", "").strip()
        file_row = files_by_id.get(file_id)
        if not file_row or not child_key:
            continue
        parent_document_id = file_row.get("DocumentID", "").strip()
        if not parent_document_id:
            continue
        has_version_rows.append({"parent": parent_document_id, "child": child_key})

    version_bom_rows: list[dict[str, str]] = []
    for link in version_to_version_rows:
        parent_key = link.get("ParentId", "").strip()
        child_key = link.get("ChildId", "").strip()
        if not parent_key or not child_key:
            continue
        if parent_key not in versions_by_key or child_key not in versions_by_key:
            continue
        version_bom_rows.append(
            {
                "parent": parent_key,
                "child": child_key,
                "quantity": link.get("QTY", "").strip() or "1",
            }
        )

    output_dir.mkdir(parents=True, exist_ok=True)
    row_counts = {
        config.outputs.parts: _write_csv(
            output_dir / config.outputs.parts,
            ["documentId", "fileName", "projectPath", "status"],
            parts_rows,
        ),
        config.outputs.part_versions: _write_csv(
            output_dir / config.outputs.part_versions,
            sorted(
                {
                    key
                    for row in part_version_rows
                    for key in row.keys()
                }
            ),
            part_version_rows,
        ),
        config.outputs.has_version: _write_csv(
            output_dir / config.outputs.has_version,
            ["parent", "child"],
            has_version_rows,
        ),
        config.outputs.version_bom: _write_csv(
            output_dir / config.outputs.version_bom,
            ["parent", "child", "quantity"],
            version_bom_rows,
        ),
    }

    manifest = {
        "generatedAt": datetime.now(UTC).isoformat(),
        "sourceSystem": config.source_system,
        "inputDir": str(input_dir),
        "outputs": row_counts,
    }
    manifest_path = output_dir / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    return TransformResult(output_dir=output_dir, manifest_path=manifest_path, row_counts=row_counts)
