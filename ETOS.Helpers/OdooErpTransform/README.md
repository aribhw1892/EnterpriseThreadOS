# OdooErpTransform

Transforms [`OdooErpExtractor`](../OdooErpExtractor/) `odoo_export/` into EnterpriseThreadOS import CSV batches.

**Current status:** transform outputs are **committed uploads** under `fixtures/committed_etos_import/`. The CLI copies those files byte-for-byte to `--output`. Live extract → transform logic is stubbed for later.

## Outputs (committed)

| File | Import mode | ETOS relationship |
| --- | --- | --- |
| `odoo-parts.csv` | Flat | `part` (`odooProductId`) |
| `odoo-part-versions.csv` | Flat | `partVersion` (`odooVersionKey`) |
| `odoo-has-version.csv` | Structural | `hasVersion` (`part` → `partVersion`) |
| `odoo-version-bom.csv` | Structural | `contains` (`partVersion` → `partVersion`) |
| `odoo-identifiers-and-mappings.json` | Profile reference | Import mapping hints for `ODOO-ERP` |

Set `structuralRelationshipType` on structural mapping approval (`hasVersion` or `contains`).

Odoo uses separate ERP identities (`ODOO-PROD-*`, `ODOO-VER-*`) with `sourceDocumentId` / `sourcePdmVersionKey` retained for cross-system identity resolution against PDM.

## Run

```powershell
cd ETOS.Helpers\OdooErpTransform
uv sync --extra dev
uv run odoo-erp-transform --input ..\OdooErpExtractor\odoo_export\mock --output .\etos_import
```

`--input` is recorded in `manifest.json` only. Output files always come from `fixtures/committed_etos_import/`.

## Update committed outputs

1. Replace CSV/JSON files under `etos_import/` or `fixtures/committed_etos_import/`
2. Run `python scripts/sync_committed_outputs.py` to mirror fixtures, demo-imports, and profile mappings
3. If `odoo-has-version.csv` is missing, the sync script derives it from `odoo-part-versions.csv`

## Full pipeline (mock, today)

```powershell
cd ETOS.Helpers\OdooErpExtractor
uv run odoo-erp-extract --use-mock

cd ..\OdooErpTransform
uv run odoo-erp-transform --input ..\OdooErpExtractor\odoo_export\mock --output .\etos_import

# Import four CSV batches via /imports with SourceSystem=ODOO-ERP
```

Demo fixtures: [`packages/manufacturing-reference/demo-imports/odoo/`](../../packages/manufacturing-reference/demo-imports/odoo/)

## Tests

```powershell
uv run pytest
```
