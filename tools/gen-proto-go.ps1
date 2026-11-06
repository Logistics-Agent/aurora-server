$ErrorActionPreference = "Stop"

$ProtocPath = "C:\Users\ngoth\.nuget\packages\grpc.tools\2.64.0\tools\windows_x64\protoc.exe"
$env:PATH = "$env:PATH;C:\Users\ngoth\go\bin"

$WorkspaceRoot = (Get-Item $PSScriptRoot\..).FullName
$ProtoDir = Join-Path $WorkspaceRoot "protos"
$OutDir = Join-Path $WorkspaceRoot "src\go\route-planning-agent\proto"

if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

Write-Host "Compiling proto files to Go..."

$protos = @(
    "route-planning-agent.proto",
    "ai_governance.proto",
    "compliance_rag.proto"
)

foreach ($proto in $protos) {
    $protoPath = Join-Path $ProtoDir $proto
    Write-Host "  -> Compiling $proto"
    & $ProtocPath `
        "--proto_path=$ProtoDir" `
        "--go_out=$WorkspaceRoot\src\go\route-planning-agent" `
        "--go_opt=module=aurora/route-planning-agent" `
        "--go-grpc_out=$WorkspaceRoot\src\go\route-planning-agent" `
        "--go-grpc_opt=module=aurora/route-planning-agent" `
        $protoPath
}

Write-Host "Proto compilation completed successfully!" -ForegroundColor Green
