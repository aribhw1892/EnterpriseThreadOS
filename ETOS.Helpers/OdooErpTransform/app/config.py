from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path


@dataclass
class OutputConfig:
    parts: str = "odoo-parts.csv"
    part_versions: str = "odoo-part-versions.csv"
    has_version: str = "odoo-has-version.csv"
    version_bom: str = "odoo-version-bom.csv"
    mappings: str = "odoo-identifiers-and-mappings.json"


@dataclass
class TransformConfig:
    source_system: str = "ODOO-ERP"
    outputs: OutputConfig = field(default_factory=OutputConfig)

    @classmethod
    def load(cls, path: Path | None) -> TransformConfig:
        if path is None or not path.exists():
            return cls()

        raw = json.loads(path.read_text(encoding="utf-8"))
        outputs = raw.get("outputs", {})
        return cls(
            source_system=raw.get("sourceSystem", "ODOO-ERP"),
            outputs=OutputConfig(
                parts=outputs.get("parts", "odoo-parts.csv"),
                part_versions=outputs.get("partVersions", "odoo-part-versions.csv"),
                has_version=outputs.get("hasVersion", "odoo-has-version.csv"),
                version_bom=outputs.get("versionBom", "odoo-version-bom.csv"),
                mappings=outputs.get("mappings", "odoo-identifiers-and-mappings.json"),
            ),
        )

    def output_file_names(self) -> list[str]:
        return [
            self.outputs.parts,
            self.outputs.part_versions,
            self.outputs.has_version,
            self.outputs.version_bom,
            self.outputs.mappings,
        ]
