# NetDiagPro UI Automation Test Runner
# Usage: .\run-ui-tests.ps1

$ErrorActionPreference = "Continue"
$TestProjectPath = "$PSScriptRoot\DesktopUiTests\DesktopUiTests"
$ArtifactsPath = "$PSScriptRoot\DesktopUiTests\artifacts"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  NetDiagPro UI Automation Test Runner   " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Ensure artifacts directory exists
New-Item -ItemType Directory -Force -Path $ArtifactsPath | Out-Null

# Kill any existing NetDiagPro processes
Write-Host "[1/4] Cleaning up existing processes..." -ForegroundColor Yellow
Stop-Process -Name "NetDiagPro" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# Build test project
Write-Host "[2/4] Building test project..." -ForegroundColor Yellow
Push-Location $TestProjectPath
$buildOutput = dotnet build --configuration Release 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Build failed!" -ForegroundColor Red
    Write-Host $buildOutput
    Pop-Location
    exit 1
}
Write-Host "[OK] Build successful" -ForegroundColor Green

# Run tests
Write-Host "[3/4] Running UI Smoke Tests..." -ForegroundColor Yellow
Write-Host ""

$testOutput = dotnet test --configuration Release --logger "console;verbosity=detailed" 2>&1
$testExitCode = $LASTEXITCODE

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "              TEST RESULTS               " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

if ($testExitCode -eq 0) {
    Write-Host "[PASSED] All tests passed!" -ForegroundColor Green
} else {
    Write-Host "[FAILED] Some tests failed!" -ForegroundColor Red
}

# Show artifacts
Write-Host ""
Write-Host "[4/4] Checking artifacts..." -ForegroundColor Yellow
$screenshots = Get-ChildItem -Path $ArtifactsPath -Filter "*.png" -ErrorAction SilentlyContinue
if ($screenshots) {
    Write-Host "Screenshots captured:" -ForegroundColor Cyan
    foreach ($file in $screenshots) {
        Write-Host "  - $($file.FullName)" -ForegroundColor Gray
    }
} else {
    Write-Host "No screenshots captured" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Artifacts directory: $ArtifactsPath" -ForegroundColor Gray

# Cleanup
Write-Host ""
Write-Host "[Cleanup] Closing any remaining NetDiagPro processes..." -ForegroundColor Yellow
Stop-Process -Name "NetDiagPro" -Force -ErrorAction SilentlyContinue

Pop-Location

Write-Host ""
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "                 DONE                    " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

exit $testExitCode
