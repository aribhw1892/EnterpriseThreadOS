# Odoo ERP transform demo fixtures

Synthetic Odoo ERP test dataset aligned with the PDM four-batch import pattern.

Use the guided wizard at **`/imports/odoo`** (or import via `/imports` with `SourceSystem=ODOO-ERP`):

1. `odoo-parts.csv` — flat `part` (`odooProductId` identity)
2. `odoo-part-versions.csv` — flat `partVersion` (`odooVersionKey` identity)
3. `odoo-has-version.csv` — structural `hasVersion`
4. `odoo-version-bom.csv` — structural `contains` on `partVersion`

Mapping profile: [`profiles/odoo-import-mappings.json`](../../profiles/odoo-import-mappings.json)

Canonical source: `ETOS.Helpers/OdooErpTransform/fixtures/committed_etos_import/`
