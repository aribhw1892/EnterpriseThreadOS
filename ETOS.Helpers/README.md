# ETOS Helpers

Standalone helper utilities for EnterpriseThreadOS workflows that are not part of the core backend or frontend.

## Shared libraries

| Project | Purpose |
| --- | --- |
| [`EtosExtractCommon/`](./EtosExtractCommon/) | XML mapping models, SQL extract orchestration, per-type CSV/JSON export |
| [`EtosTransformCommon/`](./EtosTransformCommon/) | Shared CSV I/O and transform manifest helpers |

## Source-system pipelines

| Project | Purpose |
| --- | --- |
| [`PdmExtractor/`](./PdmExtractor/) | XML-driven SolidWorks PDM SQL extractor; exports per-type CSV/JSON |
| [`PdmTransform/`](./PdmTransform/) | Maps `pdm_export/` to four ETOS import CSV batches (Issue 29) |
| [`OdooErpExtractor/`](./OdooErpExtractor/) | XML-driven Odoo ERP PostgreSQL extractor (mock export committed; live SQL placeholder) |
| [`OdooErpTransform/`](./OdooErpTransform/) | Maps `odoo_export/` to four ETOS Odoo import CSV batches + mapping profile |

PDM and Odoo share the same extract contract (`mapping_definition.xml` → `objects/` + `relationships/` + `manifest.json`). Each source system keeps its own transform config and canonical CSV outputs.

## PDM import pipeline (Issue 29)

```powershell
# Extract
cd ETOS.Helpers\PdmExtractor
set PDM_DB_SERVER=your-server
set PDM_DB_NAME=your-db
set PDM_DB_USER=your-user
set PDM_DB_PASSWORD=your-password
uv sync
uv run pdm-extract --csv --json

# Transform
cd ..\PdmTransform
uv sync --extra dev
uv run pdm-transform --input ..\PdmExtractor\pdm_export --output .\etos_import

# Import via /imports UI or API — four batches, SourceSystem=SOLIDWORKS-PDM
```

## Odoo ERP import pipeline (mock)

```powershell
# Mock extract (no Odoo DB)
cd ETOS.Helpers\OdooErpExtractor
uv sync
uv run odoo-erp-extract --use-mock

# 2. Copy committed transform outputs (uploaded Odoo CSVs)
cd ..\OdooErpTransform
uv sync --extra dev
uv run odoo-erp-transform --input ..\OdooErpExtractor\odoo_export\mock --output .\etos_import

# Import via /imports — four batches, SourceSystem=ODOO-ERP
# Outputs are committed in OdooErpTransform/fixtures/committed_etos_import/
```

When Odoo PostgreSQL is available, replace SQL in `OdooErpExtractor/mapping_definition.xml`, run `uv sync --extra postgres`, and use the same transform step.

See [`.docs/.prd/issue-29-pdm-extract-transform-import.md`](../.docs/.prd/issue-29-pdm-extract-transform-import.md) for the PDM issue sheet.
