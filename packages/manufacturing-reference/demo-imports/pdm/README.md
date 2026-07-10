# PDM transform demo fixtures

Synthetic subset aligned with `ETOS.Helpers/PdmTransform/tests/fixtures/pdm_export`.

Import via `/imports` with `SourceSystem=SOLIDWORKS-PDM`:

1. `parts.csv` — flat `part` objects (`documentId` identity)
2. `part-versions.csv` — flat `partVersion` objects
3. `has-version.csv` — structural `hasVersion` (`parent=documentId`, `child=pdmVersionKey`)
4. `version-bom.csv` — structural `contains` on `partVersion` (`parent`/`child` = `pdmVersionKey`)
