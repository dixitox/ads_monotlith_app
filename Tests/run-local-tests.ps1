#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Complete local testing workflow for RetailMonolith
.DESCRIPTION
    Builds, starts, tests, and validates the RetailMonolith application locally
#>

$ErrorActionPreference = "Stop"

# Navigate to repository root
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repoRoot

Write-Host "╔══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  RetailMonolith - Complete Local Testing Workflow    ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

# Step 1: Pre-flight checks
Write-Host "📋 Step 1: Pre-flight Checks" -ForegroundColor Yellow
Write-Host "   Checking prerequisites...`n" -ForegroundColor Gray

# Check Docker
try {
    $dockerVersion = docker --version
    Write-Host "   ✅ Docker: $dockerVersion" -ForegroundColor Green
} catch {
    Write-Host "   ❌ Docker not found! Please install Docker Desktop." -ForegroundColor Red
    exit 1
}

# Check Docker Compose
try {
    $composeVersion = docker-compose --version
    Write-Host "   ✅ Docker Compose: $composeVersion" -ForegroundColor Green
} catch {
    Write-Host "   ❌ Docker Compose not found!" -ForegroundColor Red
    exit 1
}

# Check if Docker is running
try {
    docker ps | Out-Null
    Write-Host "   ✅ Docker daemon is running" -ForegroundColor Green
} catch {
    Write-Host "   ❌ Docker daemon is not running! Please start Docker Desktop." -ForegroundColor Red
    exit 1
}

# Check .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "   ✅ .NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️  .NET SDK not found (needed for running tests)" -ForegroundColor Yellow
}

# Step 2: Clean up previous runs
Write-Host "`n📦 Step 2: Cleaning Up Previous Runs" -ForegroundColor Yellow
Write-Host "   Stopping any running containers...`n" -ForegroundColor Gray

docker-compose down -v 2>&1 | Out-Null
Write-Host "   ✅ Previous containers stopped and removed" -ForegroundColor Green

# Step 3: Build and start services
Write-Host "`n🔨 Step 3: Building and Starting Services" -ForegroundColor Yellow
Write-Host "   This may take 2-3 minutes...`n" -ForegroundColor Gray

docker-compose up -d --build
if ($LASTEXITCODE -ne 0) {
    Write-Host "   ❌ Failed to start services" -ForegroundColor Red
    exit 1
}

Write-Host "   ✅ Services started" -ForegroundColor Green

# Step 4: Wait for services to be healthy
Write-Host "`n⏳ Step 4: Waiting for Services to be Healthy" -ForegroundColor Yellow
Write-Host "   Checking service health (max 60 seconds)...`n" -ForegroundColor Gray

$maxWaitTime = 60
$elapsedTime = 0
$allHealthy = $false

while ($elapsedTime -lt $maxWaitTime) {
    Start-Sleep -Seconds 5
    $elapsedTime += 5
    
    $containers = docker-compose ps --format json | ConvertFrom-Json
    $sqlHealthy = ($containers | Where-Object { $_.Service -eq "sqlserver" }).Health -eq "healthy"
    $appRunning = ($containers | Where-Object { $_.Service -eq "retail-monolith" }).State -eq "running"
    
    if ($sqlHealthy -and $appRunning) {
        $allHealthy = $true
        break
    }
    
    Write-Host "   ⏳ Waiting... ($elapsedTime seconds)" -ForegroundColor Gray
}

if ($allHealthy) {
    Write-Host "   ✅ All services are healthy" -ForegroundColor Green
} else {
    Write-Host "   ❌ Services did not become healthy in time" -ForegroundColor Red
    Write-Host "`n   Checking logs..." -ForegroundColor Yellow
    docker-compose logs --tail=50
    exit 1
}

# Step 5: Run automated tests
Write-Host "`n🧪 Step 5: Running Automated Tests" -ForegroundColor Yellow
Write-Host "   Executing test suite...`n" -ForegroundColor Gray

$testScript = Join-Path $PSScriptRoot "test-local-deployment.ps1"
& $testScript
$testExitCode = $LASTEXITCODE

# Step 6: Show container status
Write-Host "`n📊 Step 6: Container Status" -ForegroundColor Yellow
docker-compose ps

# Step 7: Show logs summary
Write-Host "`n📝 Step 7: Recent Application Logs" -ForegroundColor Yellow
Write-Host "   Last 20 lines from application:`n" -ForegroundColor Gray
docker-compose logs --tail=20 retail-monolith

# Step 8: Provide next steps
Write-Host "`n✅ Step 8: Testing Complete!" -ForegroundColor Yellow

if ($testExitCode -eq 0) {
    Write-Host "`n🎉 All tests passed!" -ForegroundColor Green
    Write-Host "`n📱 Application is running at:" -ForegroundColor Cyan
    Write-Host "   • Main App:    http://localhost:5068" -ForegroundColor White
    Write-Host "   • Health Check: http://localhost:5068/health" -ForegroundColor White
    Write-Host "   • Products:     http://localhost:5068/Products" -ForegroundColor White
    Write-Host "`n💡 Next Steps:" -ForegroundColor Cyan
    Write-Host "   1. Open browser to http://localhost:5068" -ForegroundColor White
    Write-Host "   2. Test the application manually" -ForegroundColor White
    Write-Host "   3. View logs: docker-compose logs -f" -ForegroundColor White
    Write-Host "   4. Stop services: docker-compose down" -ForegroundColor White
    Write-Host "`n   Ready to deploy to Azure? See MONOLITH_DEPLOYMENT_GUIDE.md" -ForegroundColor Gray
} else {
    Write-Host "`n⚠️  Some tests failed!" -ForegroundColor Yellow
    Write-Host "`n💡 Troubleshooting:" -ForegroundColor Cyan
    Write-Host "   1. Check logs: docker-compose logs" -ForegroundColor White
    Write-Host "   2. Check container status: docker-compose ps" -ForegroundColor White
    Write-Host "   3. Restart services: docker-compose restart" -ForegroundColor White
    Write-Host "   4. See LOCAL_TESTING_GUIDE.md for more help" -ForegroundColor White
}

Write-Host ""
