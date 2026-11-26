$ErrorActionPreference = "Stop"

$ProtocPath = "C:\Users\ngoth\.nuget\packages\grpc.tools\2.64.0\tools\windows_x64\protoc.exe"
$IncludePath = "C:\Users\ngoth\.nuget\packages\grpc.tools\2.64.0\build\native\include"
$env:PATH = "$env:PATH;C:\Users\ngoth\go\bin"

$WorkspaceRoot = (Get-Item $PSScriptRoot\..).FullName
$ProtoDir = Join-Path $WorkspaceRoot "protos"
$OutDir = Join-Path $WorkspaceRoot "tools\gps-loadgen"

Write-Host "Compiling GPS and Shipment proto files to Go..."

& $ProtocPath --proto_path=$ProtoDir --proto_path=$IncludePath `
    --go_out=$OutDir --go_opt=module=aurora/gps-loadgen `
    --go-grpc_out=$OutDir --go-grpc_opt=module=aurora/gps-loadgen `
    (Join-Path $ProtoDir "gps_tracking.proto") `
    (Join-Path $ProtoDir "shipment_workflow.proto")

Write-Host "Proto compilation completed successfully!" -ForegroundColor Green

