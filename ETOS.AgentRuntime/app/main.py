from __future__ import annotations

import os

from fastapi import FastAPI
from fastapi.responses import JSONResponse

from app.contracts import ExecuteRequest, ExecuteResponse
from app.execute_service import execute_request

app = FastAPI(
    title="ETOS Agent Runtime",
    description="Governed single-step agent execution sidecar for EnterpriseThreadOS.",
    version="0.1.0",
)


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "healthy"}


@app.post("/v1/execute", response_model=ExecuteResponse)
async def execute(request: ExecuteRequest) -> ExecuteResponse | JSONResponse:
    response = await execute_request(request)
    if response.status == "Failed":
        return JSONResponse(status_code=422, content=response.model_dump(by_alias=True))
    return response


def main() -> None:
    import uvicorn

    port = int(os.environ.get("PORT", os.environ.get("AGENT_RUNTIME_PORT", "8010")))
    uvicorn.run("app.main:app", host="0.0.0.0", port=port, reload=False)


if __name__ == "__main__":
    main()
