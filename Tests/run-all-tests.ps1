# Run All Tests Script
# This script runs all tests in both RetailMonolith.Tests and RetailDecomposed.Tests projects

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Running Retail Application Tests" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Continue"
$testsDir = $PSScriptRoot
$repoRoot = Split-Path $testsDir -Parent

# Track results
$allTestsPassed = $true

# Run RetailMonolith.Tests
Write-Host "------------------------------------" -ForegroundColor Yellow
Write-Host "Running RetailMonolith.Tests..." -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow
Write-Host ""

$monolithProject = Join-Path $testsDir "RetailMonolith.Tests\RetailMonolith.Tests.csproj"

if (Test-Path $monolithProject) {
    dotnet test $monolithProject --verbosity minimal --nologo
    if ($LASTEXITCODE -ne 0) {
        $allTestsPassed = $false
        Write-Host ""
        Write-Host "❌ RetailMonolith.Tests FAILED" -ForegroundColor Red
    } else {
        Write-Host ""
        Write-Host "✅ RetailMonolith.Tests PASSED" -ForegroundColor Green
    }
} else {
    Write-Host "⚠️  RetailMonolith.Tests project not found at $monolithProject" -ForegroundColor Yellow
    $allTestsPassed = $false
}

Write-Host ""
Write-Host ""

# Run RetailDecomposed.Tests
Write-Host "------------------------------------" -ForegroundColor Yellow
Write-Host "Running RetailDecomposed.Tests..." -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow
Write-Host ""

$decomposedProject = Join-Path $testsDir "RetailDecomposed.Tests\RetailDecomposed.Tests.csproj"

if (Test-Path $decomposedProject) {
    dotnet test $decomposedProject --verbosity minimal --nologo
    if ($LASTEXITCODE -ne 0) {
        $allTestsPassed = $false
        Write-Host ""
        Write-Host "❌ RetailDecomposed.Tests FAILED" -ForegroundColor Red
    } else {
        Write-Host ""
        Write-Host "✅ RetailDecomposed.Tests PASSED" -ForegroundColor Green
    }
} else {
    Write-Host "⚠️  RetailDecomposed.Tests project not found at $decomposedProject" -ForegroundColor Yellow
    $allTestsPassed = $false
}

Write-Host ""
Write-Host ""

# Run Docker Compose Local Tests
Write-Host "------------------------------------" -ForegroundColor Yellow
Write-Host "Running Docker Compose Local Tests..." -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow
Write-Host ""

$dockerTestScript = Join-Path $testsDir "run-local-tests.ps1"

if (Test-Path $dockerTestScript) {
    & $dockerTestScript
    if ($LASTEXITCODE -ne 0) {
        $allTestsPassed = $false
        Write-Host ""
        Write-Host "❌ Docker Compose Tests FAILED" -ForegroundColor Red
    } else {
        Write-Host ""
        Write-Host "✅ Docker Compose Tests PASSED" -ForegroundColor Green
    }
} else {
    Write-Host "⚠️  Docker Compose test script not found at $dockerTestScript" -ForegroundColor Yellow
    Write-Host "Skipping Docker tests..." -ForegroundColor Yellow
}

Write-Host ""
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "           Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($allTestsPassed) {
    Write-Host "✅ ALL TESTS PASSED (Unit + Integration + Docker)" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ SOME TESTS FAILED" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please review the output above for details." -ForegroundColor Yellow
    exit 1
}
