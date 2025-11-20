# Quick Test Script
# Runs tests with minimal output for quick feedback

Write-Host "Running tests..." -ForegroundColor Cyan

$testsDir = $PSScriptRoot

# Run all tests quietly
dotnet test "$testsDir\RetailMonolith.Tests\RetailMonolith.Tests.csproj" --verbosity quiet --nologo
$monolithResult = $LASTEXITCODE

dotnet test "$testsDir\RetailDecomposed.Tests\RetailDecomposed.Tests.csproj" --verbosity quiet --nologo
$decomposedResult = $LASTEXITCODE

Write-Host ""
if ($monolithResult -eq 0 -and $decomposedResult -eq 0) {
    Write-Host "✅ All tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ Tests failed. Run .\run-all-tests.ps1 for details." -ForegroundColor Red
    exit 1
}
