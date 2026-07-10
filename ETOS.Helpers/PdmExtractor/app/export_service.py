from __future__ import annotations

import csv
import json
import logging
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List

from app.serialization import (
    DateTimeEncoder,
    entity_to_row,
    relationship_to_row,
    safe_filename,
)
from app.xml_mapping import Entity, RelationshipData


def write_csv(path: Path, rows: List[Dict[str, Any]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if not rows:
        path.write_text("", encoding="utf-8")
        logging.warning("Wrote empty CSV: %s", path)
        return

    fieldnames: List[str] = []
    seen = set()
    for row in rows:
        for key in row.keys():
            if key not in seen:
                seen.add(key)
                fieldnames.append(key)

    with path.open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)

    logging.info("Wrote CSV (%d rows): %s", len(rows), path)


def write_json(path: Path, data: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as f:
        json.dump(data, f, indent=2, cls=DateTimeEncoder)
    logging.info("Wrote JSON: %s", path)


def export_entities(
    entities: Dict[str, Dict[Any, Entity]],
    output_dir: Path,
    export_csv: bool,
    export_json: bool,
) -> None:
    objects_dir = output_dir / "objects"
    for obj_type, obj_map in entities.items():
        rows = [entity_to_row(entity) for entity in obj_map.values()]
        safe_type = safe_filename(obj_type)

        if export_csv:
            write_csv(objects_dir / f"{safe_type}.csv", rows)

        if export_json:
            write_json(
                objects_dir / f"{safe_type}.json",
                {
                    "objectType": obj_type,
                    "count": len(rows),
                    "records": rows,
                },
            )


def export_relationships(
    relationships_by_type: Dict[str, List[RelationshipData]],
    output_dir: Path,
    export_csv: bool,
    export_json: bool,
) -> None:
    relationships_dir = output_dir / "relationships"
    for rel_type, relationships in relationships_by_type.items():
        rows = [relationship_to_row(rel) for rel in relationships]
        safe_type = safe_filename(rel_type)

        if export_csv:
            write_csv(relationships_dir / f"{safe_type}.csv", rows)

        if export_json:
            write_json(
                relationships_dir / f"{safe_type}.json",
                {
                    "relationshipType": rel_type,
                    "count": len(rows),
                    "records": rows,
                },
            )


def write_manifest(
    output_dir: Path,
    mapping_file: str,
    entities: Dict[str, Dict[Any, Entity]],
    relationships_by_type: Dict[str, List[RelationshipData]],
) -> None:
    manifest = {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "mappingFile": str(mapping_file),
        "objects": {obj_type: len(obj_map) for obj_type, obj_map in entities.items()},
        "relationships": {rel_type: len(rels) for rel_type, rels in relationships_by_type.items()},
    }
    write_json(output_dir / "manifest.json", manifest)
