#!/usr/bin/env pwsh
# Stop All Containers Script
# This script stops all running Docker containers for both RetailMonolith and RetailDecomposed

Write-Host "`n" -NoNewline
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  🛑 STOPPING ALL CONTAINERS" -ForegroundColor Cyan
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

# Stop RetailMonolith containers
Write-Host "📦 Stopping RetailMonolith containers..." -ForegroundColor Yellow
try {
    docker-compose down
    Write-Host "  ✅ RetailMonolith containers stopped" -ForegroundColor Green
} catch {
    Write-Host "  ⚠️ Error stopping RetailMonolith: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Stop RetailDecomposed microservices containers
Write-Host "📦 Stopping RetailDecomposed microservices..." -ForegroundColor Yellow
try {
    Set-Location RetailDecomposed
    docker-compose -f docker-compose.microservices.yml down
    Set-Location ..
    Write-Host "  ✅ RetailDecomposed containers stopped" -ForegroundColor Green
} catch {
    Write-Host "  ⚠️ Error stopping RetailDecomposed: $($_.Exception.Message)" -ForegroundColor Red
    Set-Location ..
}

Write-Host ""

# Verify all containers are stopped
Write-Host "🔍 Verifying container status..." -ForegroundColor Yellow
$runningContainers = docker ps --filter "name=retail" --format "{{.Names}}"

if ($runningContainers) {
    Write-Host "  ⚠️ Still running:" -ForegroundColor Yellow
    $runningContainers | ForEach-Object { Write-Host "    - $_" -ForegroundColor Gray }
} else {
    Write-Host "  ✅ All retail containers stopped" -ForegroundColor Green
}

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  ✅ SHUTDOWN COMPLETE" -ForegroundColor Green
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""
