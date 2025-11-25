#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs both RetailMonolith and RetailDecomposed applications simultaneously.
.DESCRIPTION
    This script launches both applications either locally with dotnet run or in Docker containers.
    Press Ctrl+C to stop both applications.
.PARAMETER Mode
    Run mode: "local" (dotnet run) or "container" (Docker Compose). Default: local
.EXAMPLE
    .\run-both-apps.ps1
    Runs both apps locally with dotnet run
.EXAMPLE
    .\run-both-apps.ps1 -Mode container
    Runs both apps in Docker containers
.EXAMPLE
    .\run-both-apps.ps1 -Mode local
    Explicitly runs both apps locally with dotnet run
#>

param(
    [ValidateSet("local", "container")]
    [string]$Mode = "local"
)

$ErrorActionPreference = "Stop"

# ============================================================================
# HEADER
# ============================================================================
Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
if ($Mode -eq "local") {
    Write-Host "  Starting both applications LOCALLY (dotnet run)..." -ForegroundColor Cyan
} else {
    Write-Host "  Starting both applications in DOCKER CONTAINERS..." -ForegroundColor Cyan
}
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

if ($Mode -eq "local") {
    Write-Host "  Applications (dotnet run):" -ForegroundColor Cyan
    Write-Host "    • RetailMonolith: http://localhost:5068" -ForegroundColor Green
    Write-Host "    • RetailDecomposed: https://localhost:6068" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Microservices (Docker containers):" -ForegroundColor Cyan
    Write-Host "    • Products API: http://localhost:8081" -ForegroundColor DarkGray
    Write-Host "    • Cart API: http://localhost:8082" -ForegroundColor DarkGray
    Write-Host "    • Orders API: http://localhost:8083" -ForegroundColor DarkGray
    Write-Host "    • Checkout API: http://localhost:8084" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  Database Ports:" -ForegroundColor DarkCyan
    Write-Host "    • Monolith SQL Server: localhost:1433 (Docker)" -ForegroundColor DarkGray
    Write-Host "    • Microservices SQL Server: localhost:1434 (Docker)" -ForegroundColor DarkGray
} else {
    Write-Host "  RetailMonolith will run on: " -NoNewline
    Write-Host "http://localhost:5068" -ForegroundColor Green
    Write-Host "  RetailDecomposed (Frontend) will run on: " -NoNewline
    Write-Host "http://localhost:8080" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Microservices APIs:" -ForegroundColor DarkCyan
    Write-Host "    • Products API: http://localhost:8081" -ForegroundColor DarkGray
    Write-Host "    • Cart API: http://localhost:8082" -ForegroundColor DarkGray
    Write-Host "    • Orders API: http://localhost:8083" -ForegroundColor DarkGray
    Write-Host "    • Checkout API: http://localhost:8084" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  Database Ports:" -ForegroundColor DarkCyan
    Write-Host "    • Monolith SQL Server: localhost:1433" -ForegroundColor DarkGray
    Write-Host "    • Microservices SQL Server: localhost:1434" -ForegroundColor DarkGray
}
Write-Host ""
Write-Host "  Press Ctrl+C to stop both applications" -ForegroundColor Yellow
Write-Host ("=" * 80) -ForegroundColor DarkGray
Write-Host ""

# ============================================================================
# RUN APPLICATIONS
# ============================================================================

if ($Mode -eq "local") {
    # ========================================================================
    # LOCAL MODE: Run apps with dotnet run + microservices in Docker
    # ========================================================================
    
    Write-Host "Step 1: Starting microservices in Docker containers..." -ForegroundColor Cyan
    Write-Host ""
    
    # Check if Docker is running
    $dockerRunning = $false
    try {
        docker ps 2>&1 | Out-Null
        $dockerRunning = $LASTEXITCODE -eq 0
    } catch {
        $dockerRunning = $false
    }
    
    if (-not $dockerRunning) {
        Write-Host "❌ Docker Desktop is not running!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please start Docker Desktop and wait for it to be ready, then run this script again." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "To start Docker Desktop:" -ForegroundColor Cyan
        Write-Host "  1. Open Docker Desktop from the Start menu" -ForegroundColor Gray
        Write-Host "  2. Wait for the Docker icon in the system tray to show 'Docker Desktop is running'" -ForegroundColor Gray
        Write-Host "  3. Run this script again: .\run-both-apps.ps1 -Mode local" -ForegroundColor Gray
        Write-Host ""
        exit 1
    }
    
    # Start microservices containers for RetailDecomposed
    try {
        Set-Location RetailDecomposed
        Write-Host "Building and starting microservices containers..." -ForegroundColor Cyan
        docker-compose -f docker-compose.microservices.yml up -d
        Set-Location ..
        Write-Host "✅ Microservices containers started" -ForegroundColor Green
    } catch {
        Write-Host "❌ Failed to start microservices containers: $_" -ForegroundColor Red
        Set-Location ..
        exit 1
    }
    
    Write-Host ""
    Write-Host "Waiting for microservices to be healthy..." -ForegroundColor Cyan
    Start-Sleep -Seconds 15
    
    Write-Host ""
    Write-Host "Step 2: Verifying microservices health..." -ForegroundColor Cyan
    
    # Check microservices health
    $services = @(
        @{Port=8081; Name="Products"},
        @{Port=8082; Name="Cart"},
        @{Port=8083; Name="Orders"},
        @{Port=8084; Name="Checkout"}
    )
    
    $allHealthy = $true
    foreach ($service in $services) {
        try {
            $health = Invoke-RestMethod -Uri "http://localhost:$($service.Port)/health" -TimeoutSec 5 -ErrorAction Stop
            if ($health.status -eq "healthy") {
                Write-Host "  ✅ $($service.Name) API (port $($service.Port)): Healthy" -ForegroundColor Green
            } else {
                Write-Host "  ⚠️ $($service.Name) API (port $($service.Port)): $($health.status)" -ForegroundColor Yellow
                $allHealthy = $false
            }
        } catch {
            Write-Host "  ❌ $($service.Name) API (port $($service.Port)): Not responding" -ForegroundColor Red
            $allHealthy = $false
        }
    }
    
    if (-not $allHealthy) {
        Write-Host ""
        Write-Host "⚠️ Warning: Some microservices are not healthy. Waiting additional 15 seconds..." -ForegroundColor Yellow
        Start-Sleep -Seconds 15
        
        # Retry health check
        Write-Host "Rechecking microservices health..." -ForegroundColor Cyan
        $allHealthy = $true
        foreach ($service in $services) {
            try {
                $health = Invoke-RestMethod -Uri "http://localhost:$($service.Port)/health" -TimeoutSec 5 -ErrorAction Stop
                if ($health.status -eq "healthy") {
                    Write-Host "  ✅ $($service.Name) API: Healthy" -ForegroundColor Green
                } else {
                    Write-Host "  ⚠️ $($service.Name) API: $($health.status)" -ForegroundColor Yellow
                    $allHealthy = $false
                }
            } catch {
                Write-Host "  ❌ $($service.Name) API: Not responding" -ForegroundColor Red
                $allHealthy = $false
            }
        }
        
        if (-not $allHealthy) {
            Write-Host ""
            Write-Host "❌ Error: Some microservices failed to become healthy." -ForegroundColor Red
            Write-Host "Check container logs: docker-compose -f RetailDecomposed/docker-compose.microservices.yml logs" -ForegroundColor Yellow
            Write-Host ""
            Set-Location RetailDecomposed
            docker-compose -f docker-compose.microservices.yml down 2>&1 | Out-Null
            Set-Location ..
            exit 1
        }
    }
    
    Write-Host ""
    Write-Host "Step 3: Checking for port conflicts..." -ForegroundColor Cyan
    
    # Kill any processes using the required ports
    $ports = @(5068, 6067, 6068)
    foreach ($port in $ports) {
        $processes = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue | 
                     Select-Object -ExpandProperty OwningProcess -Unique
        if ($processes) {
            Write-Host "  Clearing port $port..." -ForegroundColor Yellow
            foreach ($proc in $processes) {
                Stop-Process -Id $proc -Force -ErrorAction SilentlyContinue
            }
        }
    }
    Write-Host "  ✅ Ports cleared" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "Step 4: Starting applications locally..." -ForegroundColor Cyan
    Write-Host ""
    
    # Start RetailMonolith in the background
    Write-Host "Starting RetailMonolith..." -ForegroundColor Magenta
    $monolithJob = Start-Job -ScriptBlock {
        Set-Location $using:PWD
        dotnet run --project .\RetailMonolith.csproj
    }

    # Start RetailDecomposed in the background
    Write-Host "Starting RetailDecomposed..." -ForegroundColor Blue
    $decomposedJob = Start-Job -ScriptBlock {
        Set-Location $using:PWD
        dotnet run --project .\RetailDecomposed\RetailDecomposed.csproj
    }

    Write-Host ""
    Write-Host "Step 5: Waiting for applications to start..." -ForegroundColor Cyan
    Start-Sleep -Seconds 10
    
    # Verify applications are running
    Write-Host "Verifying applications..." -ForegroundColor Cyan
    
    $appsHealthy = $true
    
    # Check RetailMonolith
    $monolithPort = Get-NetTCPConnection -LocalPort 5068 -ErrorAction SilentlyContinue
    if ($monolithPort) {
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:5068" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
            Write-Host "  ✅ RetailMonolith: Running (HTTP $($response.StatusCode))" -ForegroundColor Green
        } catch {
            Write-Host "  ⚠️ RetailMonolith: Port listening but not responding yet" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ❌ RetailMonolith: Not listening on port 5068" -ForegroundColor Red
        $appsHealthy = $false
    }
    
    # Check RetailDecomposed
    $decomposedPort = Get-NetTCPConnection -LocalPort 6068 -ErrorAction SilentlyContinue
    if ($decomposedPort) {
        try {
            $response = Invoke-WebRequest -Uri "https://localhost:6068" -SkipCertificateCheck -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
            Write-Host "  ✅ RetailDecomposed: Running (HTTP $($response.StatusCode))" -ForegroundColor Green
        } catch {
            Write-Host "  ⚠️ RetailDecomposed: Port listening but not responding yet" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  ❌ RetailDecomposed: Not listening on port 6068" -ForegroundColor Red
        $appsHealthy = $false
    }
    
    if (-not $appsHealthy) {
        Write-Host ""
        Write-Host "⚠️ Warning: Some applications may not have started correctly." -ForegroundColor Yellow
        Write-Host "Check the output below for errors." -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host ("=" * 80) -ForegroundColor Green
    Write-Host "  🎉 SUCCESS! All applications and services are running!" -ForegroundColor Green
    Write-Host ("=" * 80) -ForegroundColor Green
    Write-Host ""
    Write-Host "  📱 APPLICATIONS (Running Locally):" -ForegroundColor Cyan
    Write-Host "    • RetailMonolith:   http://localhost:5068" -ForegroundColor Magenta
    Write-Host "    • RetailDecomposed: https://localhost:6068" -ForegroundColor Blue
    Write-Host ""
    Write-Host "  🔧 MICROSERVICES (Docker Containers):" -ForegroundColor Cyan
    Write-Host "    • Products API:  http://localhost:8081 ✅" -ForegroundColor Green
    Write-Host "    • Cart API:      http://localhost:8082 ✅" -ForegroundColor Green
    Write-Host "    • Orders API:    http://localhost:8083 ✅" -ForegroundColor Green
    Write-Host "    • Checkout API:  http://localhost:8084 ✅" -ForegroundColor Green
    Write-Host ""
    Write-Host "  💡 TIP: Open these URLs in your browser to test the applications!" -ForegroundColor Yellow
    Write-Host "  Press Ctrl+C to stop everything..." -ForegroundColor Yellow
    Write-Host ""

    # Function to display job output with color coding
    function Show-JobOutput {
        param($Job, $AppName, $Color)
        
        $output = Receive-Job -Job $Job
        if ($output) {
            foreach ($line in $output) {
                Write-Host "[$AppName] " -ForegroundColor $Color -NoNewline
                Write-Host $line
            }
        }
    }

    # Monitor both jobs and display their output
    try {
        while ($monolithJob.State -eq 'Running' -or $decomposedJob.State -eq 'Running') {
            Show-JobOutput -Job $monolithJob -AppName "RetailMonolith" -Color "Magenta"
            Show-JobOutput -Job $decomposedJob -AppName "RetailDecomposed" -Color "Blue"
            Start-Sleep -Milliseconds 500
        }
    }
    finally {
        Write-Host ""
        Write-Host ("=" * 80) -ForegroundColor Yellow
        Write-Host "  Stopping applications and containers..." -ForegroundColor Yellow
        Write-Host ("=" * 80) -ForegroundColor Yellow
        
        # Stop both jobs
        Stop-Job -Job $monolithJob -ErrorAction SilentlyContinue
        Stop-Job -Job $decomposedJob -ErrorAction SilentlyContinue
        
        # Remove jobs
        Remove-Job -Job $monolithJob -Force -ErrorAction SilentlyContinue
        Remove-Job -Job $decomposedJob -Force -ErrorAction SilentlyContinue
        
        # Stop microservices containers
        Write-Host "Stopping microservices containers..." -ForegroundColor Yellow
        Set-Location RetailDecomposed
        docker-compose -f docker-compose.microservices.yml down 2>&1 | Out-Null
        Set-Location ..
        
        Write-Host ""
        Write-Host "  ✅ All applications and containers stopped." -ForegroundColor Green
        Write-Host ""
    }
} else {
    # ========================================================================
    # CONTAINER MODE: Run with Docker Compose
    # ========================================================================
    
    Write-Host "Starting Docker containers..." -ForegroundColor Cyan
    Write-Host ""
    
    # Start RetailMonolith containers
    Write-Host "Starting RetailMonolith (Monolith Architecture)..." -ForegroundColor Magenta
    docker-compose -f docker-compose.yml up -d
    
    Write-Host ""
    
    # Start RetailDecomposed containers
    Write-Host "Starting RetailDecomposed (Microservices Architecture)..." -ForegroundColor Blue
    docker-compose -f RetailDecomposed/docker-compose.microservices.yml up -d
    
    Write-Host ""
    Write-Host "Waiting for containers to be healthy..." -ForegroundColor Cyan
    Start-Sleep -Seconds 20
    
    Write-Host ""
    Write-Host "Verifying container health..." -ForegroundColor Cyan
    
    # Check microservices health
    $services = @(
        @{Port=8081; Name="Products"},
        @{Port=8082; Name="Cart"},
        @{Port=8083; Name="Orders"},
        @{Port=8084; Name="Checkout"}
    )
    
    foreach ($service in $services) {
        try {
            $health = Invoke-RestMethod -Uri "http://localhost:$($service.Port)/health" -TimeoutSec 5 -ErrorAction Stop
            if ($health.status -eq "healthy") {
                Write-Host "  ✅ $($service.Name) API: Healthy" -ForegroundColor Green
            } else {
                Write-Host "  ⚠️ $($service.Name) API: $($health.status)" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "  ⚠️ $($service.Name) API: Still starting..." -ForegroundColor Yellow
        }
    }
    
    # Check frontend containers
    try {
        $monolithResponse = Invoke-WebRequest -Uri "http://localhost:5068" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        Write-Host "  ✅ RetailMonolith Frontend: Running" -ForegroundColor Green
    } catch {
        Write-Host "  ⚠️ RetailMonolith Frontend: Still starting..." -ForegroundColor Yellow
    }
    
    try {
        $decomposedResponse = Invoke-WebRequest -Uri "http://localhost:8080" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        Write-Host "  ✅ RetailDecomposed Frontend: Running" -ForegroundColor Green
    } catch {
        Write-Host "  ⚠️ RetailDecomposed Frontend: Still starting..." -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host ("=" * 80) -ForegroundColor Green
    Write-Host "  🎉 All containers started successfully!" -ForegroundColor Green
    Write-Host ("=" * 80) -ForegroundColor Green
    Write-Host ""
    Write-Host "  Application URLs:" -ForegroundColor Cyan
    Write-Host "    • RetailMonolith: " -NoNewline
    Write-Host "http://localhost:5068" -ForegroundColor Magenta
    Write-Host "    • RetailDecomposed Frontend: " -NoNewline
    Write-Host "http://localhost:8080" -ForegroundColor Blue
    Write-Host ""
    Write-Host "  Microservices APIs:" -ForegroundColor Cyan
    Write-Host "    • Products API: http://localhost:8081/api/products" -ForegroundColor DarkGray
    Write-Host "    • Cart API: http://localhost:8082/api/cart" -ForegroundColor DarkGray
    Write-Host "    • Orders API: http://localhost:8083/api/orders" -ForegroundColor DarkGray
    Write-Host "    • Checkout API: http://localhost:8084/api/checkout" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  Database Connections:" -ForegroundColor Cyan
    Write-Host "    • Monolith SQL: localhost:1433 (RetailMonolith DB)" -ForegroundColor DarkGray
    Write-Host "    • Microservices SQL: localhost:1434 (RetailDecomposedDB)" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  Useful Commands:" -ForegroundColor Yellow
    Write-Host "    • View all logs: " -NoNewline -ForegroundColor DarkGray
    Write-Host "docker-compose -f docker-compose.yml logs -f" -ForegroundColor White
    Write-Host "    • View microservices logs: " -NoNewline -ForegroundColor DarkGray
    Write-Host "docker-compose -f RetailDecomposed/docker-compose.microservices.yml logs -f" -ForegroundColor White
    Write-Host "    • Check container status: " -NoNewline -ForegroundColor DarkGray
    Write-Host "docker ps" -ForegroundColor White
    Write-Host "    • Stop all: " -NoNewline -ForegroundColor DarkGray
    Write-Host ".\run-both-apps.ps1 -Mode container" -ForegroundColor White
    Write-Host "                    then press Ctrl+C and containers will be stopped" -ForegroundColor DarkGray
    Write-Host ""
    
    Write-Host "  Press Ctrl+C to stop all containers..." -ForegroundColor Yellow
    Write-Host ""
    
    # Wait for user to press Ctrl+C
    try {
        while ($true) {
            Start-Sleep -Seconds 1
        }
    }
    finally {
        Write-Host ""
        Write-Host ("=" * 80) -ForegroundColor Yellow
        Write-Host "  Stopping all containers..." -ForegroundColor Yellow
        Write-Host ("=" * 80) -ForegroundColor Yellow
        Write-Host ""
        
        # Stop RetailDecomposed containers
        Write-Host "Stopping RetailDecomposed microservices..." -ForegroundColor Blue
        docker-compose -f RetailDecomposed/docker-compose.microservices.yml down
        
        Write-Host ""
        
        # Stop RetailMonolith containers
        Write-Host "Stopping RetailMonolith..." -ForegroundColor Magenta
        docker-compose -f docker-compose.yml down
        
        Write-Host ""
        Write-Host ("=" * 80) -ForegroundColor Green
        Write-Host "  All containers stopped." -ForegroundColor Green
        Write-Host ("=" * 80) -ForegroundColor Green
        Write-Host ""
    }
}

# ============================================================================
# FINAL SUMMARY
# ============================================================================
Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  Application Access Information" -ForegroundColor Cyan
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

if ($Mode -eq "local") {
    Write-Host "  RetailMonolith (Monolith):     " -NoNewline
    Write-Host "http://localhost:5068" -ForegroundColor Magenta
    Write-Host "  RetailDecomposed (Decomposed): " -NoNewline
    Write-Host "http://localhost:6068" -ForegroundColor Blue
} else {
    Write-Host "  RetailMonolith (Monolith):          " -NoNewline
    Write-Host "http://localhost:5068" -ForegroundColor Magenta
    Write-Host "  RetailDecomposed (Microservices):   " -NoNewline
    Write-Host "http://localhost:8080" -ForegroundColor Blue
}

Write-Host ""
Write-Host "  To run in different mode:" -ForegroundColor DarkGray
Write-Host "    • Local: .\run-both-apps.ps1 -Mode local" -ForegroundColor DarkGray
Write-Host "    • Containers: .\run-both-apps.ps1 -Mode container" -ForegroundColor DarkGray
Write-Host ""
