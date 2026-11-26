<#
.SYNOPSIS
    Standardized GPS Tracking Benchmark Execution Harness (.NET vs Go)
.EXAMPLE
    .\benchmarks\gps-tracking\scripts\run-benchmark.ps1 -Target dotnet -Profile C -Duration 300 -Seed 42
    .\benchmarks\gps-tracking\scripts\run-benchmark.ps1 -Target go -Profile C -Duration 300 -Seed 42
#>
param(
    [ValidateSet("dotnet", "go")]
    [string]$Target = "dotnet",

    [ValidateSet("go", "csharp")]
    [string]$Driver = "go",

    [ValidateSet("A", "B", "C", "D", "E")]
    [string]$Profile = "C",

    [int]$WarmupSeconds = 60,
    [int]$DurationSeconds = 300,
    [int]$Iterations = 3,
    [int]$Seed = 42,
    [string]$GpsUrl = "",
    [string]$OutputDir = "$PSScriptRoot\..\results"
)

$ErrorActionPreference = "Stop"
$WorkspaceRoot = (Get-Item "$PSScriptRoot\..\..\..").FullName

if ([string]::IsNullOrWhiteSpace($GpsUrl)) {
    if ($Target -eq "go") {
        # Check if port 6006 is listening (docker-compose profile), else default 6002
        $GpsUrl = "http://localhost:6002"
    } else {
        $GpsUrl = "http://localhost:6002"
    }
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$ResultCsv = Join-Path $OutputDir "benchmark-results.csv"
$SummaryJson = Join-Path $OutputDir "summary-$Target-$Profile-$Timestamp.json"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       AURORA GPS TRACKING BENCHMARK HARNESS              " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "Target SUT         : $Target"
Write-Host "Load Driver        : $Driver"
Write-Host "Load Profile       : Profile $Profile"
Write-Host "Warm-up Window     : ${WarmupSeconds}s"
Write-Host "Measurement Window : ${DurationSeconds}s"
Write-Host "Iterations         : $Iterations"
Write-Host "Random Seed        : $Seed"
Write-Host "Endpoint           : $GpsUrl"
Write-Host "=========================================================="
Write-Host ""

# Ensure CSV header exists
if (-not (Test-Path $ResultCsv)) {
    "Timestamp,Target,Driver,Profile,Iteration,DurationSec,Vehicles,TotalSent,ThroughputEvtSec,LatencyP50Ms,LatencyP95Ms,LatencyP99Ms,SuccessRatePct,Timeouts" | Out-File -FilePath $ResultCsv -Encoding utf8
}

function Invoke-Driver([int]$duration, [bool]$isWarmup) {
    if ($Driver -eq "go") {
        $binPath = "$WorkspaceRoot\tools\gps-loadgen\bin\gps-loadgen.exe"
        if (-not (Test-Path $binPath)) {
            Write-Host "Building Go load-generator..." -ForegroundColor Yellow
            Push-Location "$WorkspaceRoot\tools\gps-loadgen"
            & go build -o bin/gps-loadgen.exe .
            Pop-Location
        }
        $args = @("-profile", $Profile, "-duration", "$duration", "-seed", "$Seed", "-gps", $GpsUrl)
        & $binPath $args
    } else {
        $projPath = "$WorkspaceRoot\tools\gps-simulator\gps-simulator.csproj"
        $args = @("run", "--project", $projPath, "--", "--profile", $Profile, "--duration", "$duration", "--seed", "$Seed", "--gps", $GpsUrl)
        & dotnet $args
    }
}

# 1. Warm-up Phase
if ($WarmupSeconds -gt 0) {
    Write-Host "[WARMUP] Starting ${WarmupSeconds}s warm-up to stabilize JIT/allocators/caches..." -ForegroundColor Yellow
    Invoke-Driver -duration $WarmupSeconds -isWarmup $true
    Write-Host "[WARMUP] Warm-up completed. Cooling down 10s..." -ForegroundColor Green
    Start-Sleep -Seconds 10
}

# 2. Measurement Runs
$allRuns = @()
for ($i = 1; $i -le $Iterations; $i++) {
    Write-Host ""
    Write-Host "----------------------------------------------------------" -ForegroundColor Cyan
    Write-Host "[RUN $i/$Iterations] Executing ${DurationSeconds}s measurement..." -ForegroundColor Cyan
    Write-Host "----------------------------------------------------------" -ForegroundColor Cyan

    $output = Invoke-Driver -duration $DurationSeconds -isWarmup $false

    # Parse metrics from output
    $throughput = 0.0
    $p50 = 0.0
    $p95 = 0.0
    $p99 = 0.0
    $totalSent = 0
    $succRate = 0.0
    $timeouts = 0
    $vehicles = 100

    foreach ($line in $output) {
        if ($line -match "Total Vehicles\s*:\s*(\d+)") { $vehicles = [int]$matches[1] }
        if ($line -match "Total Events Sent\s*:\s*(\d+)") { $totalSent = [int]$matches[1] }
        if ($line -match "Throughput\s*:\s*([\d\.]+)") { $throughput = [double]$matches[1] }
        if ($line -match "Client Latency p50\s*:\s*([\d\.]+)") { $p50 = [double]$matches[1] }
        if ($line -match "Client Latency p95\s*:\s*([\d\.]+)") { $p95 = [double]$matches[1] }
        if ($line -match "Client Latency p99\s*:\s*([\d\.]+)") { $p99 = [double]$matches[1] }
        if ($line -match "Success Rate\s*:\s*([\d\.]+)%") { $succRate = [double]$matches[1] }
        if ($line -match "RPC Timeout Count\s*:\s*(\d+)") { $timeouts = [int]$matches[1] }
    }

    $runRecord = [PSCustomObject]@{
        Timestamp        = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
        Target           = $Target
        Driver           = $Driver
        Profile          = $Profile
        Iteration        = $i
        DurationSec      = $DurationSeconds
        Vehicles         = $vehicles
        TotalSent        = $totalSent
        ThroughputEvtSec = $throughput
        LatencyP50Ms     = $p50
        LatencyP95Ms     = $p95
        LatencyP99Ms     = $p99
        SuccessRatePct   = $succRate
        Timeouts         = $timeouts
    }

    $allRuns += $runRecord

    "$($runRecord.Timestamp),$Target,$Driver,$Profile,$i,$DurationSeconds,$vehicles,$totalSent,$throughput,$p50,$p95,$p99,$succRate,$timeouts" |
        Out-File -FilePath $ResultCsv -Append -Encoding utf8

    if ($i -lt $Iterations) {
        Write-Host "Cooling down 15s before next iteration..." -ForegroundColor Gray
        Start-Sleep -Seconds 15
    }
}

$allRuns | ConvertTo-Json -Depth 4 | Out-File -FilePath $SummaryJson -Encoding utf8

Write-Host ""
Write-Host "==========================================================" -ForegroundColor Green
Write-Host "Benchmark Completed for Target: $Target ($Iterations iterations)" -ForegroundColor Green
Write-Host "Results saved to: $ResultCsv"
Write-Host "Summary saved to: $SummaryJson"
Write-Host "==========================================================" -ForegroundColor Green

