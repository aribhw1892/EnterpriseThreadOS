param(
    [string]$ApiBaseUrl = $(if ($env:ETOS_API_BASE_URL) { $env:ETOS_API_BASE_URL } else { "http://localhost:5000" }),
    [string]$UserId = $(if ($env:NEXT_PUBLIC_ETOS_ADMIN_USER_ID) { $env:NEXT_PUBLIC_ETOS_ADMIN_USER_ID } else { "11111111-1111-1111-1111-111111111111" }),
    [string]$TenantId = $(if ($env:NEXT_PUBLIC_ETOS_TENANT_ID) { $env:NEXT_PUBLIC_ETOS_TENANT_ID } else { "22222222-2222-2222-2222-222222222222" }),
    [switch]$SkipBackendTest
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Invoke-EtosPost([string]$Path, [object]$Body = @{}) {
    $headers = @{
        "X-ETOS-User-Id"   = $UserId
        "X-ETOS-Tenant-Id" = $TenantId
    }
    $uri = "$ApiBaseUrl$Path"
    Write-Host "POST $uri"
    return Invoke-RestMethod -Method Post -Uri $uri -Headers $headers -ContentType "application/json" -Body ($Body | ConvertTo-Json -Depth 8)
}

Write-Step "MVP demonstration bootstrap (Issue 26)"
Write-Host "API: $ApiBaseUrl"
Write-Host "Tenant: $TenantId"
Write-Host "User: $UserId"

Write-Step "Clean development demo data"
Invoke-EtosPost "/api/admin/development/clean-demo-data" | Out-Null

Write-Step "Install manufacturing reference package"
$install = Invoke-EtosPost "/api/admin/development/install-reference-package" @{ packageKey = "etos-manufacturing-reference" }
Write-Host "Model package: $($install.modelPackage.id)"

Write-Step "Backend integration proof (primary acceptance)"
if (-not $SkipBackendTest) {
    Push-Location (Split-Path $PSScriptRoot -Parent)
    dotnet test ETOS.Backend.Tests/ETOS.Backend.Tests.csproj --filter "FullyQualifiedName~MvpDemonstrationFlow" --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "MvpDemonstrationFlow tests failed."
    }
    Pop-Location
}

Write-Step "Operator deep links"
$links = @(
    "Frontend home checklist: http://localhost:3000/"
    "Imports harness: http://localhost:3000/imports"
    "Governed chat: http://localhost:3000/chat"
    "Recommendations: http://localhost:3000/recommendations"
    "Review tasks: http://localhost:3000/tasks"
    "Workflow publish + execute: http://localhost:3000/workflows/bom-impact-review/publish"
    "AI traces: http://localhost:3000/ai-traces"
)
$links | ForEach-Object { Write-Host $_ }

Write-Step "Done"
Write-Host "See docs/mvp-demonstration-flow.md for the full 20-step map."
