#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs both RetailMonolith and RetailDecomposed applications in Docker containers or manages Azure Kubernetes Service (AKS).
.DESCRIPTION
    This script launches both applications in Docker containers or manages Azure Kubernetes Service (AKS) deployments.
    In container mode, it automatically rebuilds images to ensure the latest code changes are included.
    In azure mode, it checks the status of AKS deployments and provides connection information.
    Press Ctrl+C to stop applications in container mode.
.PARAMETER Mode
    Run mode: "container" (Docker Compose) or "azure" (Azure Kubernetes Service). Default: container
.PARAMETER SkipRebuild
    In container mode, skip rebuilding Docker images (use existing images). Default: false
.PARAMETER NoCache
    In container mode, rebuild Docker images without using cache. Default: false
.PARAMETER ResourceGroup
    In azure mode, the Azure resource group containing AKS. Optional - if not specified, uses default.
.PARAMETER Environment
    In azure mode, the AKS environment name. Default: aks-retail-decomposed
.EXAMPLE
    .\run-both-apps.ps1
    Runs both apps in Docker containers with automatic rebuild
.EXAMPLE
    .\run-both-apps.ps1 -Mode container -SkipRebuild
    Runs both apps in Docker containers using existing images (no rebuild)
.EXAMPLE
    .\run-both-apps.ps1 -Mode container -NoCache
    Runs both apps in Docker containers with full rebuild (no cache)
.EXAMPLE
    .\run-both-apps.ps1 -Mode azure
    Check and manage Azure Kubernetes Service deployments
.EXAMPLE
    .\run-both-apps.ps1 -Mode azure -ResourceGroup rg-retail-decomposed
    Check and manage AKS in specific resource group
#>

param(
    [ValidateSet("container", "azure")]
    [string]$Mode = "container",
    
    [switch]$SkipRebuild,
    
    [switch]$NoCache,
    
    [string]$ResourceGroup = "",
    
    [string]$Environment = "aks-retail-decomposed"
)

$ErrorActionPreference = "Stop"

# ============================================================================
# HEADER
# ============================================================================
Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
if ($Mode -eq "container") {
    Write-Host "  Starting both applications in DOCKER CONTAINERS..." -ForegroundColor Cyan
} else {
    Write-Host "  Managing AZURE KUBERNETES SERVICE (AKS)..." -ForegroundColor Cyan
}
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

if ($Mode -eq "container") {
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
    Write-Host ""
    Write-Host "  Press Ctrl+C to stop both applications" -ForegroundColor Yellow
    Write-Host ("=" * 80) -ForegroundColor DarkGray
    Write-Host ""
}

# ============================================================================
# RUN APPLICATIONS
# ============================================================================

if ($Mode -eq "container") {
    # ========================================================================
    # CONTAINER MODE: Run with Docker Compose
    # ========================================================================
    
    Write-Host "Preparing Docker containers..." -ForegroundColor Cyan
    Write-Host ""
    
    # Check if Docker is running
    Write-Host "Step 1: Verifying Docker is running..." -ForegroundColor Cyan
    $dockerRunning = $false
    try {
        docker ps 2>&1 | Out-Null
        $dockerRunning = $LASTEXITCODE -eq 0
    } catch {
        $dockerRunning = $false
    }
    
    if (-not $dockerRunning) {
        Write-Host "⚠️  Docker Desktop is not running" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Attempting to start Docker Desktop..." -ForegroundColor Cyan
        
        # Try to start Docker Desktop
        $dockerPath = "C:\Program Files\Docker\Docker\Docker Desktop.exe"
        if (Test-Path $dockerPath) {
            try {
                Start-Process -FilePath $dockerPath -WindowStyle Hidden
                Write-Host "  Docker Desktop starting..." -ForegroundColor Gray
                Write-Host ""
                Write-Host "Waiting for Docker to be ready (up to 90 seconds)..." -ForegroundColor Cyan
                
                # Wait for Docker to start (check every 5 seconds for up to 90 seconds)
                $maxWaitTime = 90
                $waitInterval = 5
                $elapsed = 0
                
                while ($elapsed -lt $maxWaitTime) {
                    Start-Sleep -Seconds $waitInterval
                    $elapsed += $waitInterval
                    
                    try {
                        docker ps 2>&1 | Out-Null
                        if ($LASTEXITCODE -eq 0) {
                            Write-Host "  ✅ Docker is now running (took $elapsed seconds)" -ForegroundColor Green
                            $dockerRunning = $true
                            break
                        }
                    } catch {
                        # Continue waiting
                    }
                    
                    Write-Host "  Waiting... ($elapsed seconds elapsed)" -ForegroundColor Gray
                }
                
                if (-not $dockerRunning) {
                    Write-Host ""
                    Write-Host "❌ Docker failed to start within $maxWaitTime seconds" -ForegroundColor Red
                    Write-Host ""
                    Write-Host "Please:" -ForegroundColor Yellow
                    Write-Host "  1. Check Docker Desktop in the system tray" -ForegroundColor Gray
                    Write-Host "  2. Wait for Docker to fully start" -ForegroundColor Gray
                    Write-Host "  3. Run this script again: .\run-both-apps.ps1" -ForegroundColor Gray
                    Write-Host ""
                    exit 1
                }
            } catch {
                Write-Host ""
                Write-Host "❌ Failed to start Docker Desktop: $_" -ForegroundColor Red
                Write-Host ""
                Write-Host "Please start Docker Desktop manually and run this script again." -ForegroundColor Yellow
                Write-Host ""
                exit 1
            }
        } else {
            Write-Host ""
            Write-Host "❌ Docker Desktop not found at: $dockerPath" -ForegroundColor Red
            Write-Host ""
            Write-Host "Please:" -ForegroundColor Yellow
            Write-Host "  1. Install Docker Desktop from https://www.docker.com/products/docker-desktop" -ForegroundColor Gray
            Write-Host "  2. Or start Docker Desktop manually if installed elsewhere" -ForegroundColor Gray
            Write-Host ""
            exit 1
        }
    } else {
        Write-Host "  ✅ Docker is running" -ForegroundColor Green
    }
    
    Write-Host ""
    
    # Rebuild containers to ensure latest code
    if (-not $SkipRebuild) {
        Write-Host "Step 2: Rebuilding containers with latest code..." -ForegroundColor Cyan
        Write-Host ""
        
        # Build RetailMonolith
        Write-Host "Building RetailMonolith container..." -ForegroundColor Magenta
        $buildArgs = @("-f", "docker-compose.yml", "build")
        if ($NoCache) {
            $buildArgs += "--no-cache"
            Write-Host "  (Using --no-cache for fresh build)" -ForegroundColor DarkGray
        }
        
        $buildResult = docker-compose @buildArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Failed to build RetailMonolith container" -ForegroundColor Red
            Write-Host $buildResult -ForegroundColor Red
            exit 1
        }
        Write-Host "  ✅ RetailMonolith built successfully" -ForegroundColor Green
        
        Write-Host ""
        
        # Build RetailDecomposed microservices
        Write-Host "Building RetailDecomposed microservices containers..." -ForegroundColor Blue
        Write-Host "  (This may take 60-90 seconds for 5 services)" -ForegroundColor DarkGray
        
        $buildArgs = @("-f", "RetailDecomposed/docker-compose.microservices.yml", "build")
        if ($NoCache) {
            $buildArgs += "--no-cache"
        }
        
        $buildResult = docker-compose @buildArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Failed to build RetailDecomposed containers" -ForegroundColor Red
            Write-Host $buildResult -ForegroundColor Red
            exit 1
        }
        Write-Host "  ✅ RetailDecomposed microservices built successfully" -ForegroundColor Green
        
        Write-Host ""
    } else {
        Write-Host "Step 2: Skipping rebuild (using existing images)..." -ForegroundColor Yellow
        Write-Host "  ⚠️ Warning: Using existing images - code changes may not be reflected!" -ForegroundColor Yellow
        Write-Host ""
    }
    
    # Start RetailMonolith containers
    Write-Host "Step 3: Starting RetailMonolith (Monolith Architecture)..." -ForegroundColor Magenta
    docker-compose -f docker-compose.yml up -d
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to start RetailMonolith containers" -ForegroundColor Red
        exit 1
    }
    Write-Host "  ✅ RetailMonolith containers started" -ForegroundColor Green
    
    Write-Host ""
    
    # Start RetailDecomposed containers
    Write-Host "Step 4: Starting RetailDecomposed (Microservices Architecture)..." -ForegroundColor Blue
    docker-compose -f RetailDecomposed/docker-compose.microservices.yml up -d
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to start RetailDecomposed containers" -ForegroundColor Red
        # Stop RetailMonolith since it started successfully
        docker-compose -f docker-compose.yml down 2>&1 | Out-Null
        exit 1
    }
    Write-Host "  ✅ RetailDecomposed containers started" -ForegroundColor Green
    
    Write-Host ""
    Write-Host "Step 5: Waiting for containers to initialize..." -ForegroundColor Cyan
    Write-Host "  (Waiting 20 seconds for health checks to pass)" -ForegroundColor DarkGray
    Start-Sleep -Seconds 20
    
    Write-Host ""
    Write-Host "Step 6: Verifying container health..." -ForegroundColor Cyan
    
    # Check Docker container status
    Write-Host ""
    Write-Host "  Checking Docker container status..." -ForegroundColor DarkGray
    $allContainers = docker ps -a --filter "name=retail" --format "{{.Names}}: {{.Status}}"
    foreach ($container in $allContainers) {
        if ($container -match "Up.*healthy") {
            Write-Host "    ✅ $container" -ForegroundColor Green
        } elseif ($container -match "Up") {
            Write-Host "    ⚠️ $container" -ForegroundColor Yellow
        } else {
            Write-Host "    ❌ $container" -ForegroundColor Red
        }
    }
    
    Write-Host ""
    Write-Host "  Checking microservices API health..." -ForegroundColor DarkGray
    
    # Check microservices health
    $services = @(
        @{Port=8081; Name="Products"},
        @{Port=8082; Name="Cart"},
        @{Port=8083; Name="Orders"},
        @{Port=8084; Name="Checkout"}
    )
    
    $allHealthy = $true
    $unhealthyServices = @()
    
    foreach ($service in $services) {
        try {
            $health = Invoke-RestMethod -Uri "http://localhost:$($service.Port)/health" -TimeoutSec 5 -ErrorAction Stop
            if ($health.status -eq "healthy") {
                Write-Host "    ✅ $($service.Name) API (port $($service.Port)): Healthy" -ForegroundColor Green
            } else {
                Write-Host "    ⚠️ $($service.Name) API (port $($service.Port)): $($health.status)" -ForegroundColor Yellow
                $allHealthy = $false
                $unhealthyServices += $service.Name
            }
        } catch {
            Write-Host "    ⚠️ $($service.Name) API (port $($service.Port)): Not responding" -ForegroundColor Yellow
            $allHealthy = $false
            $unhealthyServices += $service.Name
        }
    }
    
    # If any services unhealthy, wait and retry
    if (-not $allHealthy) {
        Write-Host ""
        Write-Host "  ⚠️ Some microservices not healthy yet. Waiting additional 15 seconds..." -ForegroundColor Yellow
        Start-Sleep -Seconds 15
        
        Write-Host "  Rechecking unhealthy services..." -ForegroundColor DarkGray
        $stillUnhealthy = @()
        
        foreach ($service in $services | Where-Object { $unhealthyServices -contains $_.Name }) {
            try {
                $health = Invoke-RestMethod -Uri "http://localhost:$($service.Port)/health" -TimeoutSec 5 -ErrorAction Stop
                if ($health.status -eq "healthy") {
                    Write-Host "    ✅ $($service.Name) API: Now Healthy" -ForegroundColor Green
                } else {
                    Write-Host "    ❌ $($service.Name) API: Still unhealthy" -ForegroundColor Red
                    $stillUnhealthy += $service.Name
                }
            } catch {
                Write-Host "    ❌ $($service.Name) API: Still not responding" -ForegroundColor Red
                $stillUnhealthy += $service.Name
            }
        }
        
        if ($stillUnhealthy.Count -gt 0) {
            Write-Host ""
            Write-Host "  ⚠️ Warning: Some services are still unhealthy: $($stillUnhealthy -join ', ')" -ForegroundColor Yellow
            Write-Host "  They may need more time to start. Check logs with:" -ForegroundColor Yellow
            Write-Host "    docker-compose -f RetailDecomposed/docker-compose.microservices.yml logs <service-name>" -ForegroundColor White
            Write-Host ""
        }
    }
    
    Write-Host ""
    Write-Host "  Checking frontend applications..." -ForegroundColor DarkGray
    
    # Check frontend containers
    $frontendHealthy = $true
    
    try {
        $monolithResponse = Invoke-WebRequest -Uri "http://localhost:5068" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        Write-Host "    ✅ RetailMonolith Frontend (port 5068): Running (HTTP $($monolithResponse.StatusCode))" -ForegroundColor Green
    } catch {
        Write-Host "    ⚠️ RetailMonolith Frontend (port 5068): Not responding yet" -ForegroundColor Yellow
        $frontendHealthy = $false
    }
    
    try {
        $decomposedResponse = Invoke-WebRequest -Uri "http://localhost:8080" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        Write-Host "    ✅ RetailDecomposed Frontend (port 8080): Running (HTTP $($decomposedResponse.StatusCode))" -ForegroundColor Green
    } catch {
        Write-Host "    ⚠️ RetailDecomposed Frontend (port 8080): Not responding yet" -ForegroundColor Yellow
        $frontendHealthy = $false
    }
    
    # If frontends not healthy, wait and retry
    if (-not $frontendHealthy) {
        Write-Host ""
        Write-Host "  ⚠️ Frontend applications not responding yet. Waiting additional 10 seconds..." -ForegroundColor Yellow
        Start-Sleep -Seconds 10
        
        Write-Host "  Rechecking frontend applications..." -ForegroundColor DarkGray
        
        try {
            $monolithResponse = Invoke-WebRequest -Uri "http://localhost:5068" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
            Write-Host "    ✅ RetailMonolith: Now responding" -ForegroundColor Green
        } catch {
            Write-Host "    ⚠️ RetailMonolith: Still not responding (may need more time)" -ForegroundColor Yellow
        }
        
        try {
            $decomposedResponse = Invoke-WebRequest -Uri "http://localhost:8080" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
            Write-Host "    ✅ RetailDecomposed: Now responding" -ForegroundColor Green
        } catch {
            Write-Host "    ⚠️ RetailDecomposed: Still not responding (may need more time)" -ForegroundColor Yellow
        }
    }
    
    Write-Host ""
    Write-Host ("=" * 80) -ForegroundColor Green
    Write-Host "  🎉 Container startup complete!" -ForegroundColor Green
    Write-Host ("=" * 80) -ForegroundColor Green
    Write-Host ""
    
    if (-not $SkipRebuild) {
        Write-Host "  ✅ All containers rebuilt with latest code" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️ Using existing container images (rebuild was skipped)" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "  📱 APPLICATION URLs:" -ForegroundColor Cyan
    Write-Host "    • RetailMonolith: " -NoNewline
    Write-Host "http://localhost:5068" -ForegroundColor Magenta
    Write-Host "    • RetailDecomposed Frontend: " -NoNewline
    Write-Host "http://localhost:8080" -ForegroundColor Blue
    Write-Host ""
    Write-Host "  🔧 MICROSERVICES APIs:" -ForegroundColor Cyan
    Write-Host "    • Products API: http://localhost:8081/api/products" -ForegroundColor DarkGray
    Write-Host "    • Cart API: http://localhost:8082/api/cart" -ForegroundColor DarkGray
    Write-Host "    • Orders API: http://localhost:8083/api/orders" -ForegroundColor DarkGray
    Write-Host "    • Checkout API: http://localhost:8084/api/checkout" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  💾 DATABASE Connections:" -ForegroundColor Cyan
    Write-Host "    • Monolith SQL: localhost:1433 (RetailMonolith DB)" -ForegroundColor DarkGray
    Write-Host "    • Microservices SQL: localhost:1434 (RetailDecomposedDB)" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "  📊 MONITORING Commands:" -ForegroundColor Yellow
    Write-Host "    • Container status:       " -NoNewline -ForegroundColor DarkGray
    Write-Host "docker ps" -ForegroundColor White
    Write-Host "    • All logs:               " -NoNewline -ForegroundColor DarkGray
    Write-Host "docker-compose -f docker-compose.yml logs -f" -ForegroundColor White
    Write-Host "    • Microservices logs:     " -NoNewline -ForegroundColor DarkGray
    Write-Host "docker-compose -f RetailDecomposed/docker-compose.microservices.yml logs -f" -ForegroundColor White
    Write-Host "    • Specific service logs:  " -NoNewline -ForegroundColor DarkGray
    Write-Host "docker logs <container-name>" -ForegroundColor White
    Write-Host ""
    Write-Host "  🔄 REBUILD Options:" -ForegroundColor Yellow
    Write-Host "    • Rebuild and run:        " -NoNewline -ForegroundColor DarkGray
    Write-Host ".\run-both-apps.ps1 -Mode container" -ForegroundColor White
    Write-Host "    • Skip rebuild:           " -NoNewline -ForegroundColor DarkGray
    Write-Host ".\run-both-apps.ps1 -Mode container -SkipRebuild" -ForegroundColor White
    Write-Host "    • Force clean rebuild:    " -NoNewline -ForegroundColor DarkGray
    Write-Host ".\run-both-apps.ps1 -Mode container -NoCache" -ForegroundColor White
    Write-Host ""
    Write-Host "  💡 TIP: By default, containers are rebuilt to include latest code changes!" -ForegroundColor Cyan
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
} elseif ($Mode -eq "azure") {
    # ========================================================================
    # AZURE MODE: Manage Azure Kubernetes Service (AKS) Deployments
    # ========================================================================
    
    Write-Host "Managing AKS Deployments..." -ForegroundColor Cyan
    Write-Host ""
    
    # Check for resource group from environment variable if not provided
    if ([string]::IsNullOrWhiteSpace($ResourceGroup)) {
        if ($env:AZURE_RESOURCE_GROUP) {
            $ResourceGroup = $env:AZURE_RESOURCE_GROUP
            Write-Host "  Using resource group from environment variable: $ResourceGroup" -ForegroundColor Cyan
        }
    }
    
    # Check if Azure CLI is installed
    Write-Host "Step 1: Verifying Azure CLI installation..." -ForegroundColor Cyan
    try {
        $azVersion = az version 2>&1 | ConvertFrom-Json
        Write-Host "  ✅ Azure CLI version $($azVersion.'azure-cli')" -ForegroundColor Green
    } catch {
        Write-Host "  ❌ Azure CLI is not installed!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please install Azure CLI from: https://aka.ms/InstallAzureCLIDirect" -ForegroundColor Yellow
        Write-Host ""
        exit 1
    }
    
    Write-Host ""
    
    # Check if kubectl is installed
    Write-Host "Step 2: Verifying kubectl installation..." -ForegroundColor Cyan
    try {
        $kubectlVersion = kubectl version --client --short 2>&1 | Out-String
        Write-Host "  ✅ kubectl is installed" -ForegroundColor Green
    } catch {
        Write-Host "  ❌ kubectl is not installed!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please install kubectl: az aks install-cli" -ForegroundColor Yellow
        Write-Host ""
        exit 1
    }
    
    Write-Host ""
    
    # Check Azure login status
    Write-Host "Step 3: Verifying Azure authentication..." -ForegroundColor Cyan
    try {
        $account = az account show 2>&1 | ConvertFrom-Json
        Write-Host "  ✅ Logged in as: $($account.user.name)" -ForegroundColor Green
        Write-Host "  ✅ Subscription: $($account.name)" -ForegroundColor Green
    } catch {
        Write-Host "  ❌ Not logged in to Azure!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please run: az login" -ForegroundColor Yellow
        Write-Host ""
        exit 1
    }
    
    Write-Host ""
    
    # Define namespaces and deployments to check
    $aksDeployments = @(
        @{
            Namespace = "retail-monolith"
            Name = "retail-monolith"
            DisplayName = "RetailMonolith"
            IngressName = "retail-monolith-ingress"
            ServiceName = "retail-monolith-service"
            LabelSelector = "app=retail-monolith"
        },
        @{
            Namespace = "retail-decomposed"
            Name = "frontend-service"
            DisplayName = "RetailDecomposed Frontend"
            IngressName = "retail-decomposed-ingress"
            ServiceName = "frontend-service"
            LabelSelector = "app=frontend"
        },
        @{
            Namespace = "retail-decomposed"
            Name = "products-service"
            DisplayName = "Products API"
            IngressName = "retail-decomposed-ingress"
            ServiceName = "products-service"
            LabelSelector = "app=products"
        },
        @{
            Namespace = "retail-decomposed"
            Name = "cart-service"
            DisplayName = "Cart API"
            IngressName = "retail-decomposed-ingress"
            ServiceName = "cart-service"
            LabelSelector = "app=cart"
        },
        @{
            Namespace = "retail-decomposed"
            Name = "orders-service"
            DisplayName = "Orders API"
            IngressName = "retail-decomposed-ingress"
            ServiceName = "orders-service"
            LabelSelector = "app=orders"
        },
        @{
            Namespace = "retail-decomposed"
            Name = "checkout-service"
            DisplayName = "Checkout API"
            IngressName = "retail-decomposed-ingress"
            ServiceName = "checkout-service"
            LabelSelector = "app=checkout"
        }
    )
    
    # Auto-detect AKS clusters if resource group not provided
    $aksClusterInfo = @{}
    
    if ([string]::IsNullOrWhiteSpace($ResourceGroup)) {
        Write-Host "Step 4: Auto-detecting AKS clusters across all resource groups..." -ForegroundColor Cyan
        Write-Host "  (This may take a few seconds)" -ForegroundColor DarkGray
        
        try {
            # Get all AKS clusters in subscription
            $allClustersJson = az aks list --output json 2>&1
            if ($LASTEXITCODE -ne 0) {
                throw "Azure CLI command failed: $allClustersJson"
            }
            
            $allClusters = $allClustersJson | ConvertFrom-Json
            
            if (-not $allClusters -or $allClusters.Count -eq 0) {
                Write-Host "  ❌ No AKS clusters found in subscription!" -ForegroundColor Red
                Write-Host ""
                Write-Host "Please deploy to AKS or specify -ResourceGroup parameter" -ForegroundColor Yellow
                Write-Host ""
                exit 1
            }
            
            # Build map of cluster names to resource groups
            foreach ($cluster in $allClusters) {
                $clusterName = $cluster.name
                $rgName = $cluster.resourceGroup
                $aksClusterInfo[$clusterName] = $rgName
                Write-Host "  ✅ Found AKS cluster: $clusterName in resource group: $rgName" -ForegroundColor Green
            }
            
            Write-Host ""
            Write-Host "  Found $($aksClusterInfo.Count) AKS cluster(s)" -ForegroundColor Cyan
        } catch {
            Write-Host "  ❌ Failed to list AKS clusters: $_" -ForegroundColor Red
            Write-Host ""
            Write-Host "Please specify -ResourceGroup parameter" -ForegroundColor Yellow
            Write-Host ""
            exit 1
        }
    } else {
        Write-Host "Step 4: Using specified resource group: $ResourceGroup" -ForegroundColor Cyan
        
        try {
            # Get AKS clusters in the specified resource group
            $clustersInRgJson = az aks list --resource-group $ResourceGroup --output json 2>&1
            if ($LASTEXITCODE -eq 0) {
                $clustersInRg = $clustersInRgJson | ConvertFrom-Json
                foreach ($cluster in $clustersInRg) {
                    $aksClusterInfo[$cluster.name] = $ResourceGroup
                    Write-Host "  ✅ Found AKS cluster: $($cluster.name)" -ForegroundColor Green
                }
            } else {
                Write-Host "  ⚠️ No AKS clusters found in resource group: $ResourceGroup" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "  ⚠️ Could not query AKS clusters: $_" -ForegroundColor Yellow
        }
    }
    
    if ($aksClusterInfo.Count -eq 0) {
        Write-Host "  ❌ No AKS clusters found!" -ForegroundColor Red
        Write-Host ""
        exit 1
    }
    
    Write-Host ""
    
    # Process ALL AKS clusters to start both apps
    Write-Host "Step 5: Checking and starting all AKS clusters..." -ForegroundColor Cyan
    $clusterStatuses = @{}
    
    foreach ($clusterName in $aksClusterInfo.Keys) {
        $clusterRg = $aksClusterInfo[$clusterName]
        Write-Host ""
        Write-Host "  Cluster: $clusterName (Resource Group: $clusterRg)" -ForegroundColor White
        
        # Check if cluster is running
        try {
            $clusterStatus = az aks show --resource-group $clusterRg --name $clusterName --query "powerState.code" -o tsv 2>&1
            if ($clusterStatus -eq "Running") {
                Write-Host "    ✅ Cluster is running" -ForegroundColor Green
                $clusterStatuses[$clusterName] = "Running"
            } else {
                Write-Host "    ⚠️ Cluster state: $clusterStatus" -ForegroundColor Yellow
                Write-Host "    Starting cluster..." -ForegroundColor Yellow
                az aks start --resource-group $clusterRg --name $clusterName 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "    ✅ Cluster started successfully" -ForegroundColor Green
                    $clusterStatuses[$clusterName] = "Running"
                } else {
                    Write-Host "    ❌ Failed to start cluster" -ForegroundColor Red
                    $clusterStatuses[$clusterName] = "Failed"
                }
            }
        } catch {
            Write-Host "    ⚠️ Could not determine cluster status: $_" -ForegroundColor Yellow
            $clusterStatuses[$clusterName] = "Unknown"
        }
        
        # Get credentials for this cluster
        try {
            az aks get-credentials --resource-group $clusterRg --name $clusterName --overwrite-existing 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "    ✅ kubectl configured" -ForegroundColor Green
            }
        } catch {
            Write-Host "    ⚠️ Failed to configure kubectl" -ForegroundColor Yellow
        }
    }
    
    Write-Host ""
    
    # Check SQL Server network access for both resource groups
    Write-Host "Step 6: Checking SQL Server network access..." -ForegroundColor Cyan
    $sqlServersFixed = 0
    
    foreach ($clusterName in $aksClusterInfo.Keys) {
        $clusterRg = $aksClusterInfo[$clusterName]
        
        try {
            $sqlServers = az sql server list --resource-group $clusterRg --query "[].name" -o tsv 2>&1
            if ($LASTEXITCODE -eq 0 -and $sqlServers) {
                foreach ($sqlServerName in $sqlServers -split "`n") {
                    if ([string]::IsNullOrWhiteSpace($sqlServerName)) { continue }
                    $sqlServerName = $sqlServerName.Trim()
                    
                    $publicAccess = az sql server show --resource-group $clusterRg --name $sqlServerName --query "publicNetworkAccess" -o tsv 2>$null
                    if ($publicAccess -eq "Disabled") {
                        Write-Host "  ⚠️ Enabling public network access for SQL Server: $sqlServerName" -ForegroundColor Yellow
                        az sql server update --resource-group $clusterRg --name $sqlServerName --enable-public-network true 2>&1 | Out-Null
                        if ($LASTEXITCODE -eq 0) {
                            Write-Host "    ✅ Public network access enabled" -ForegroundColor Green
                            $sqlServersFixed++
                        }
                    } else {
                        Write-Host "  ✅ SQL Server $sqlServerName has public access enabled" -ForegroundColor Green
                    }
                    
                    # Also ensure Azure services can access
                    $firewallRule = az sql server firewall-rule show --resource-group $clusterRg --server $sqlServerName --name AllowAzureServices 2>$null
                    if (-not $firewallRule) {
                        Write-Host "  ⚠️ Adding firewall rule for Azure services on: $sqlServerName" -ForegroundColor Yellow
                        az sql server firewall-rule create --resource-group $clusterRg --server $sqlServerName --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 2>&1 | Out-Null
                        if ($LASTEXITCODE -eq 0) {
                            Write-Host "    ✅ Firewall rule added" -ForegroundColor Green
                        }
                    }
                }
            }
        } catch {
            # No SQL servers or error - continue
        }
    }
    
    if ($sqlServersFixed -gt 0) {
        Write-Host ""
        Write-Host "  ⚠️ SQL Server access was modified. Pods may need to restart." -ForegroundColor Yellow
        Write-Host "  Waiting 10 seconds for configuration to propagate..." -ForegroundColor Cyan
        Start-Sleep -Seconds 10
    }
    
    Write-Host ""
    
    # Check deployments and pod status across ALL clusters
    Write-Host "Step 7: Checking deployment status across all clusters..." -ForegroundColor Cyan
    $deploymentStatus = @{}
    $zeroReplicaDeployments = @()
    $failedDeployments = @()
    
    foreach ($clusterName in $aksClusterInfo.Keys) {
        $clusterRg = $aksClusterInfo[$clusterName]
        
        # Set kubectl context to this cluster
        az aks get-credentials --resource-group $clusterRg --name $clusterName --overwrite-existing 2>&1 | Out-Null
        
        Write-Host ""
        Write-Host "  Cluster: $clusterName" -ForegroundColor White
        
        foreach ($deployment in $aksDeployments) {
            try {
                $deploymentInfo = kubectl get deployment $deployment.Name -n $deployment.Namespace -o json 2>&1 | ConvertFrom-Json
                
                if ($LASTEXITCODE -ne 0) {
                    continue  # Deployment doesn't exist in this cluster
                }
                
                $replicas = $deploymentInfo.spec.replicas
                $readyReplicas = $deploymentInfo.status.readyReplicas
                
                if (-not $readyReplicas) { $readyReplicas = 0 }
                
                $deploymentKey = "$clusterName|$($deployment.Name)"
                $deploymentStatus[$deploymentKey] = @{
                    ClusterName = $clusterName
                    ClusterRg = $clusterRg
                    Namespace = $deployment.Namespace
                    Replicas = $replicas
                    ReadyReplicas = $readyReplicas
                    DisplayName = $deployment.DisplayName
                    DeploymentName = $deployment.Name
                }
                
                if ($replicas -eq 0) {
                    Write-Host "    ⚠️ $($deployment.DisplayName): Scaled to 0 replicas" -ForegroundColor Yellow
                    $zeroReplicaDeployments += $deploymentStatus[$deploymentKey]
                } elseif ($readyReplicas -eq $replicas) {
                    Write-Host "    ✅ $($deployment.DisplayName): Running ($readyReplicas/$replicas pods ready)" -ForegroundColor Green
                } else {
                    Write-Host "    ⚠️ $($deployment.DisplayName): $readyReplicas/$replicas pods ready" -ForegroundColor Yellow
                    $failedDeployments += $deploymentStatus[$deploymentKey]
                }
            } catch {
                # Deployment doesn't exist in this cluster - expected
            }
        }
    }
    
    Write-Host ""
    
    # Scale up deployments that are at 0 replicas
    if ($zeroReplicaDeployments.Count -gt 0) {
        Write-Host "Step 8: Scaling up deployments..." -ForegroundColor Cyan
        
        foreach ($deployInfo in $zeroReplicaDeployments) {
            # Set correct cluster context
            az aks get-credentials --resource-group $deployInfo.ClusterRg --name $deployInfo.ClusterName --overwrite-existing 2>&1 | Out-Null
            
            Write-Host "  Scaling up $($deployInfo.DisplayName) in $($deployInfo.ClusterName)..." -ForegroundColor Yellow
            try {
                kubectl scale deployment $deployInfo.DeploymentName -n $deployInfo.Namespace --replicas=2 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "  ✅ $($deployInfo.DisplayName): Scaled to 2 replicas" -ForegroundColor Green
                    $deployInfo.Replicas = 2
                } else {
                    Write-Host "  ❌ $($deployInfo.DisplayName): Failed to scale" -ForegroundColor Red
                }
            } catch {
                Write-Host "  ❌ $($deployInfo.DisplayName): Error scaling: $_" -ForegroundColor Red
            }
        }
        
        Write-Host ""
    }
    
    # Restart failed deployments
    if ($failedDeployments.Count -gt 0) {
        Write-Host "Step 8b: Restarting failed deployments..." -ForegroundColor Cyan
        
        foreach ($deployInfo in $failedDeployments) {
            if ($deployInfo.ReadyReplicas -eq 0) {
                # Set correct cluster context
                az aks get-credentials --resource-group $deployInfo.ClusterRg --name $deployInfo.ClusterName --overwrite-existing 2>&1 | Out-Null
                
                Write-Host "  Restarting $($deployInfo.DisplayName) in $($deployInfo.ClusterName)..." -ForegroundColor Yellow
                try {
                    kubectl rollout restart deployment $deployInfo.DeploymentName -n $deployInfo.Namespace 2>&1 | Out-Null
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "  ✅ $($deployInfo.DisplayName): Restart initiated" -ForegroundColor Green
                    }
                } catch {
                    Write-Host "  ⚠️ $($deployInfo.DisplayName): Could not restart" -ForegroundColor Yellow
                }
            }
        }
        
        Write-Host ""
    }
    
    # Wait for pods to become ready
    if ($zeroReplicaDeployments.Count -gt 0 -or $failedDeployments.Count -gt 0) {
        Write-Host "Step 9: Waiting for pods to become ready..." -ForegroundColor Cyan
        Write-Host "  This may take 1-2 minutes..." -ForegroundColor DarkGray
        Write-Host ""
        
        $maxWait = 120  # 2 minutes
        $waited = 0
        $checkInterval = 10
        
        while ($waited -lt $maxWait) {
            Start-Sleep -Seconds $checkInterval
            $waited += $checkInterval
            
            $allReady = $true
            $statusLine = "  "
            
            foreach ($clusterName in $aksClusterInfo.Keys) {
                $clusterRg = $aksClusterInfo[$clusterName]
                az aks get-credentials --resource-group $clusterRg --name $clusterName --overwrite-existing 2>&1 | Out-Null
                
                foreach ($deploymentKey in $deploymentStatus.Keys) {
                    $deployInfo = $deploymentStatus[$deploymentKey]
                    if ($deployInfo.ClusterName -ne $clusterName) { continue }
                    
                    try {
                        $deploymentInfo = kubectl get deployment $deployInfo.DeploymentName -n $deployInfo.Namespace -o json 2>&1 | ConvertFrom-Json
                        $readyReplicas = $deploymentInfo.status.readyReplicas
                        $replicas = $deploymentInfo.spec.replicas
                        
                        if (-not $readyReplicas) { $readyReplicas = 0 }
                        
                        $deployInfo.ReadyReplicas = $readyReplicas
                        
                        if ($readyReplicas -lt $replicas) {
                            $allReady = $false
                        }
                    } catch {
                        $allReady = $false
                    }
                }
            }
            
            if ($allReady) {
                Write-Host "  ✅ All pods are ready!" -ForegroundColor Green
                break
            }
            
            Write-Host "  ⏳ Still waiting... ($waited seconds elapsed)" -ForegroundColor Yellow
        }
        
        Write-Host ""
    }
    
    # Get ingress external IPs from ALL clusters
    Write-Host "Step 10: Getting application URLs..." -ForegroundColor Cyan
    $ingressUrls = @{}
    
    foreach ($clusterName in $aksClusterInfo.Keys) {
        $clusterRg = $aksClusterInfo[$clusterName]
        az aks get-credentials --resource-group $clusterRg --name $clusterName --overwrite-existing 2>&1 | Out-Null
        
        # Check retail-monolith ingress
        try {
            $monolithIngress = kubectl get ingress retail-monolith-ingress -n retail-monolith -o json 2>&1 | ConvertFrom-Json
            if ($LASTEXITCODE -eq 0 -and $monolithIngress.status.loadBalancer.ingress) {
                $monolithIP = $monolithIngress.status.loadBalancer.ingress[0].ip
                if ($monolithIP) {
                    $ingressUrls["retail-monolith"] = "https://$monolithIP"
                    Write-Host "  ✅ RetailMonolith URL: https://$monolithIP" -ForegroundColor Green
                }
            }
        } catch {
            # Ingress doesn't exist in this cluster
        }
        
        # Check retail-decomposed ingress
        try {
            $decomposedIngress = kubectl get ingress retail-decomposed-ingress -n retail-decomposed -o json 2>&1 | ConvertFrom-Json
            if ($LASTEXITCODE -eq 0 -and $decomposedIngress.status.loadBalancer.ingress) {
                $decomposedIP = $decomposedIngress.status.loadBalancer.ingress[0].ip
                if ($decomposedIP) {
                    $ingressUrls["retail-decomposed"] = "https://$decomposedIP"
                    Write-Host "  ✅ RetailDecomposed URL: https://$decomposedIP" -ForegroundColor Green
                }
            }
        } catch {
            # Ingress doesn't exist in this cluster
        }
    }
    
    if ($ingressUrls.Count -eq 0) {
        Write-Host "  ⚠️ No ingress URLs found. Ingresses may still be provisioning." -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host ("=" * 80) -ForegroundColor Green
    Write-Host "  🎉 AKS Deployment Management Complete!" -ForegroundColor Green
    Write-Host ("=" * 80) -ForegroundColor Green
    Write-Host ""
    Write-Host "  📱 APPLICATION URLs:" -ForegroundColor Cyan
    Write-Host ""
    
    if ($ingressUrls.Count -gt 0) {
        foreach ($appName in $ingressUrls.Keys) {
            $url = $ingressUrls[$appName]
            $displayName = if ($appName -eq "retail-monolith") { "RetailMonolith (Monolith)" } else { "RetailDecomposed (Microservices)" }
            $displayName = $displayName.PadRight(35)
            Write-Host "    • $displayName" -NoNewline
            Write-Host "$url" -ForegroundColor Green
        }
    } else {
        Write-Host "    ⚠️ No external IPs available yet. Ingress may still be provisioning." -ForegroundColor Yellow
        Write-Host "    Run this script again in a few minutes." -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "  🏗️ AKS CLUSTERS:" -ForegroundColor Cyan
    foreach ($clusterName in $aksClusterInfo.Keys) {
        $clusterRg = $aksClusterInfo[$clusterName]
        $status = $clusterStatuses[$clusterName]
        $statusColor = if ($status -eq "Running") { "Green" } else { "Yellow" }
        Write-Host "    • $clusterName " -NoNewline -ForegroundColor White
        Write-Host "($clusterRg) " -NoNewline -ForegroundColor DarkGray
        Write-Host "- $status" -ForegroundColor $statusColor
    }
    
    Write-Host ""
    Write-Host "  📊 DEPLOYMENT STATUS:" -ForegroundColor Cyan
    $readyCount = 0
    $totalCount = 0
    foreach ($deploymentKey in $deploymentStatus.Keys) {
        $deployInfo = $deploymentStatus[$deploymentKey]
        $totalCount++
        if ($deployInfo.ReadyReplicas -eq $deployInfo.Replicas -and $deployInfo.Replicas -gt 0) {
            $readyCount++
        }
    }
    Write-Host "    • $readyCount of $totalCount deployments are fully ready" -ForegroundColor $(if ($readyCount -eq $totalCount) { "Green" } else { "Yellow" })
    Write-Host ""
    Write-Host "  📊 KUBERNETES MANAGEMENT Commands:" -ForegroundColor Yellow
    Write-Host "    • View all pods:          " -NoNewline -ForegroundColor DarkGray
    Write-Host "kubectl get pods --all-namespaces" -ForegroundColor White
    Write-Host "    • View deployments:       " -NoNewline -ForegroundColor DarkGray
    Write-Host "kubectl get deployments -n retail-monolith" -ForegroundColor White
    Write-Host "    • View pod logs:          " -NoNewline -ForegroundColor DarkGray
    Write-Host "kubectl logs -n <namespace> <pod-name>" -ForegroundColor White
    Write-Host "    • View services:          " -NoNewline -ForegroundColor DarkGray
    Write-Host "kubectl get services --all-namespaces" -ForegroundColor White
    Write-Host "    • View ingress:           " -NoNewline -ForegroundColor DarkGray
    Write-Host "kubectl get ingress --all-namespaces" -ForegroundColor White
    Write-Host "    • Scale deployment:       " -NoNewline -ForegroundColor DarkGray
    Write-Host "kubectl scale deployment <name> -n <namespace> --replicas=<count>" -ForegroundColor White
    Write-Host ""
    Write-Host "  🔄 RUN THIS SCRIPT AGAIN:" -ForegroundColor Yellow
    Write-Host "    • Auto-detect cluster:    " -NoNewline -ForegroundColor DarkGray
    Write-Host ".\run-both-apps.ps1 -Mode azure" -ForegroundColor White
    if ($ResourceGroup) {
        Write-Host "    • Use specific RG:        " -NoNewline -ForegroundColor DarkGray
        Write-Host ".\run-both-apps.ps1 -Mode azure -ResourceGroup $ResourceGroup" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "  💡 TIP: Copy the external IPs from above and add them to your /etc/hosts file for custom domains!" -ForegroundColor Cyan
    Write-Host ""
}

# ============================================================================
# FINAL SUMMARY
# ============================================================================
if ($Mode -ne "azure") {
    Write-Host ""
    Write-Host ("=" * 80) -ForegroundColor Cyan
    Write-Host "  Session Summary" -ForegroundColor Cyan
    Write-Host ("=" * 80) -ForegroundColor Cyan
    Write-Host ""

    Write-Host "  Applications (Containers):" -ForegroundColor White
    Write-Host "    • RetailMonolith (Monolith):          " -NoNewline
    Write-Host "http://localhost:5068" -ForegroundColor Magenta
    Write-Host "    • RetailDecomposed (Microservices):   " -NoNewline
    Write-Host "http://localhost:8080" -ForegroundColor Blue
    
    if (-not $SkipRebuild) {
        Write-Host ""
        Write-Host "  Containers were rebuilt with latest code ✅" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "  Containers used existing images (rebuild was skipped) ⚠️" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "  Run modes:" -ForegroundColor DarkGray
    Write-Host "    • Containers (rebuild):      " -NoNewline -ForegroundColor DarkGray
    Write-Host ".\run-both-apps.ps1" -ForegroundColor White
    Write-Host "    • Containers (skip rebuild): " -NoNewline -ForegroundColor DarkGray
    Write-Host ".\run-both-apps.ps1 -SkipRebuild" -ForegroundColor White
    Write-Host "    • Containers (clean build):  " -NoNewline -ForegroundColor DarkGray
    Write-Host ".\run-both-apps.ps1 -NoCache" -ForegroundColor White
    Write-Host "    • Azure AKS (auto-detect):   " -NoNewline -ForegroundColor DarkGray
    Write-Host ".\run-both-apps.ps1 -Mode azure" -ForegroundColor White
    Write-Host "    • Azure AKS (specific RG):   " -NoNewline -ForegroundColor DarkGray
    Write-Host ".\run-both-apps.ps1 -Mode azure -ResourceGroup <rg-name>" -ForegroundColor White
    Write-Host ""
}
