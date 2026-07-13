# Odoo ERP Synthetic Test Dataset

Derived from the supplied PDM exports while preserving the same four-object/relationship pattern.

| File | Rows | Purpose |
| --- | ---: | --- |
| `odoo-parts.csv` | 107 | Odoo-style products/items |
| `odoo-part-versions.csv` | 479 | Revision abstraction for ERP testing |
| `odoo-has-version.csv` | 479 | Product-to-revision relationships |
| `odoo-version-bom.csv` | 719 | Revision-to-revision manufacturing BOM |
| `odoo-identifiers-and-mappings.json` | — | Import mapping profile for `SourceSystem=ODOO-ERP` |

Important: standard Odoo Product and BoM models do not natively match the exact PDM part/version structure. The revision entity and version-level BOM are synthetic test abstractions designed to match the canonical `part`, `partVersion`, `hasVersion`, and `contains` model. Original PDM identifiers are retained as non-identity attributes (`sourceDocumentId`, `sourcePdmVersionKey`) for cross-system identity-resolution tests.

Installing the manufacturing reference package seeds cross-attribute identity rules:

- `sourceDocumentId` (Odoo `part`) ↔ `documentId` (PDM `part`)
- `sourcePdmVersionKey` (Odoo `partVersion`) ↔ `pdmVersionKey` (PDM `partVersion`)

After both PDM and Odoo batches are staged, use `/imports/odoo` step 5 (Identity review) to generate candidates, approve matches, then promote.

Import via `/imports` with `SourceSystem=ODOO-ERP`:

1. `odoo-parts.csv` — flat `part` (`odooProductId` identity)
2. `odoo-part-versions.csv` — flat `partVersion` (`odooVersionKey` identity)
3. `odoo-has-version.csv` — structural `hasVersion`
4. `odoo-version-bom.csv` — structural `contains` on `partVersion`

Stage flat batches before structural batches.

Canonical source: `fixtures/committed_etos_import/`
