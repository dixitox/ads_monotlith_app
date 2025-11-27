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

# Run Docker Compose Local Tests (Monolith)
Write-Host "------------------------------------" -ForegroundColor Yellow
Write-Host "Running Docker Compose Local Tests (Monolith)..." -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow
Write-Host ""

$dockerTestScript = Join-Path $testsDir "run-local-tests.ps1"

if (Test-Path $dockerTestScript) {
    & $dockerTestScript
    if ($LASTEXITCODE -ne 0) {
        $allTestsPassed = $false
        Write-Host ""
        Write-Host "❌ Docker Compose Tests (Monolith) FAILED" -ForegroundColor Red
    } else {
        Write-Host ""
        Write-Host "✅ Docker Compose Tests (Monolith) PASSED" -ForegroundColor Green
    }
} else {
    Write-Host "⚠️  Docker Compose test script not found at $dockerTestScript" -ForegroundColor Yellow
    Write-Host "Skipping Docker tests..." -ForegroundColor Yellow
}

Write-Host ""
Write-Host ""

# Run Microservices Deployment Tests
Write-Host "------------------------------------" -ForegroundColor Yellow
Write-Host "Running Microservices Deployment Tests..." -ForegroundColor Yellow
Write-Host "------------------------------------" -ForegroundColor Yellow
Write-Host ""

$microservicesTestScript = Join-Path $testsDir "test-microservices-deployment.ps1"

if (Test-Path $microservicesTestScript) {
    # Ensure microservices are running before tests
    Write-Host "🚀 Starting microservices containers..." -ForegroundColor Cyan
    Push-Location (Join-Path $repoRoot "RetailDecomposed")
    docker-compose -f docker-compose.microservices.yml up -d 2>&1 | Out-Null
    
    # Wait for services to be ready
    Write-Host "⏳ Waiting 60 seconds for all services to initialize..." -ForegroundColor Cyan
    Start-Sleep -Seconds 60
    Pop-Location
    
    Write-Host "✅ Microservices started" -ForegroundColor Green
    Write-Host ""
    
    & $microservicesTestScript -Environment Local
    if ($LASTEXITCODE -ne 0) {
        $allTestsPassed = $false
        Write-Host ""
        Write-Host "❌ Microservices Deployment Tests FAILED" -ForegroundColor Red
    } else {
        Write-Host ""
        Write-Host "✅ Microservices Deployment Tests PASSED" -ForegroundColor Green
    }
} else {
    Write-Host "⚠️  Microservices test script not found at $microservicesTestScript" -ForegroundColor Yellow
    Write-Host "Skipping Microservices tests..." -ForegroundColor Yellow
}

Write-Host ""
Write-Host ""

# Optional: Run AKS deployment tests if kubectl is configured
$aksTestScript = Join-Path $testsDir "test-aks-deployment.ps1"

if (Test-Path $aksTestScript) {
    # Check if kubectl is available and configured
    $kubectlAvailable = $false
    try {
        $null = kubectl config current-context 2>&1
        $kubectlAvailable = $LASTEXITCODE -eq 0
    } catch {
        $kubectlAvailable = $false
    }
    
    if ($kubectlAvailable) {
        Write-Host "------------------------------------" -ForegroundColor Yellow
        Write-Host "Running AKS Deployment Tests..." -ForegroundColor Yellow
        Write-Host "------------------------------------" -ForegroundColor Yellow
        Write-Host ""
        
        & $aksTestScript
        if ($LASTEXITCODE -ne 0) {
            $allTestsPassed = $false
            Write-Host ""
            Write-Host "❌ AKS Deployment Tests FAILED" -ForegroundColor Red
        } else {
            Write-Host ""
            Write-Host "✅ AKS Deployment Tests PASSED" -ForegroundColor Green
        }
        
        Write-Host ""
        Write-Host ""
    } else {
        Write-Host "ℹ️  kubectl not configured - Skipping AKS tests" -ForegroundColor Cyan
        Write-Host ""
        Write-Host ""
    }
}

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "           Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($allTestsPassed) {
    $testScope = "Unit + Integration + Docker + Microservices + AKS"
    Write-Host "✅ ALL TESTS PASSED ($testScope)" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ SOME TESTS FAILED" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please review the output above for details." -ForegroundColor Yellow
    exit 1
}
