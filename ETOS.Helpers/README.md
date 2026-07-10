# ETOS Helpers

Standalone helper utilities for EnterpriseThreadOS workflows that are not part of the core backend or frontend.

| Project | Purpose |
| --- | --- |
| [`PdmExtractor/`](./PdmExtractor/) | XML-driven SolidWorks PDM SQL extractor; exports per-type CSV/JSON |
| [`PdmTransform/`](./PdmTransform/) | Maps `pdm_export/` to four ETOS import CSV batches (Issue 29) |

## PDM import pipeline (Issue 29)

```powershell
# Extract
cd ETOS.Helpers\PdmExtractor
set PDM_DB_SERVER=your-server
set PDM_DB_NAME=your-db
set PDM_DB_USER=your-user
set PDM_DB_PASSWORD=your-password
uv run pdm-extract --csv --json

# Transform
cd ..\PdmTransform
uv run pdm-transform --input ..\PdmExtractor\pdm_export --output .\etos_import

# Import via /imports UI or API — four batches, SourceSystem=SOLIDWORKS-PDM
```

See [`.docs/.prd/issue-29-pdm-extract-transform-import.md`](../.docs/.prd/issue-29-pdm-extract-transform-import.md) for the full issue sheet.
