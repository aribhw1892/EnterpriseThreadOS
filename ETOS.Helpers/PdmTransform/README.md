# PdmTransform

Transforms [`PdmExtractor`](../PdmExtractor/) `pdm_export/` output into four EnterpriseThreadOS import CSV batches.

Shared CSV utilities live in [`../EtosTransformCommon/`](../EtosTransformCommon/).

## Outputs

| File | Import mode | ETOS relationship |
| --- | --- | --- |
| `parts.csv` | Flat | `part` (`documentId`) |
| `part-versions.csv` | Flat | `partVersion` (`pdmVersionKey`) |
| `has-version.csv` | Structural | `hasVersion` (`part` → `partVersion`) |
| `version-bom.csv` | Structural | `contains` (`partVersion` → `partVersion`) |

Set `structuralRelationshipType` on structural mapping approval (`hasVersion` or `contains`).

## Run

```powershell
cd ETOS.Helpers\PdmTransform
uv sync --extra dev
uv run pdm-transform --input ..\PdmExtractor\pdm_export --output .\etos_import
```

Optional config: `--config transform.config.json`

## Full pipeline

```powershell
# 1. Extract from PDM SQL (see PdmExtractor README)
cd ETOS.Helpers\PdmExtractor
uv run pdm-extract --csv --json

# 2. Transform
cd ..\PdmTransform
uv run pdm-transform --input ..\PdmExtractor\pdm_export --output .\etos_import

# 3. Import four CSV files via /imports with SourceSystem=SOLIDWORKS-PDM
```

Demo fixtures: [`packages/manufacturing-reference/demo-imports/pdm/`](../../packages/manufacturing-reference/demo-imports/pdm/)

## Tests

```powershell
uv run pytest
```
