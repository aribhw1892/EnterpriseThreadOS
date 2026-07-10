from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path


@dataclass
class VariablePivotConfig:
    enabled: bool = True
    columns: list[str] = field(default_factory=list)


@dataclass
class OutputConfig:
    parts: str = "parts.csv"
    part_versions: str = "part-versions.csv"
    has_version: str = "has-version.csv"
    version_bom: str = "version-bom.csv"


@dataclass
class TransformConfig:
    source_system: str = "SOLIDWORKS-PDM"
    version_filter: str = "all"
    variable_pivot: VariablePivotConfig = field(default_factory=VariablePivotConfig)
    outputs: OutputConfig = field(default_factory=OutputConfig)

    @classmethod
    def load(cls, path: Path | None) -> TransformConfig:
        if path is None or not path.exists():
            return cls()

        raw = json.loads(path.read_text(encoding="utf-8"))
        pivot = raw.get("variablePivot", {})
        outputs = raw.get("outputs", {})
        return cls(
            source_system=raw.get("sourceSystem", "SOLIDWORKS-PDM"),
            version_filter=raw.get("versionFilter", "all"),
            variable_pivot=VariablePivotConfig(
                enabled=bool(pivot.get("enabled", True)),
                columns=list(pivot.get("columns", [])),
            ),
            outputs=OutputConfig(
                parts=outputs.get("parts", "parts.csv"),
                part_versions=outputs.get("partVersions", "part-versions.csv"),
                has_version=outputs.get("hasVersion", "has-version.csv"),
                version_bom=outputs.get("versionBom", "version-bom.csv"),
            ),
        )
