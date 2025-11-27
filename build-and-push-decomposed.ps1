# build-and-push-decomposed.ps1
# Build and push all RetailDecomposed microservices Docker images to ACR

<#
.SYNOPSIS
    Builds and pushes all RetailDecomposed microservice Docker images to Azure Container Registry

.DESCRIPTION
    This script:
    - Builds Docker images for all 5 microservices (Frontend, Products, Cart, Orders, Checkout)
    - Tags images with version and 'latest'
    - Pushes all images to Azure Container Registry

.PARAMETER ResourceGroup
    Name of the Azure Resource Group (default: rg-retail-decomposed)

.PARAMETER AcrName
    Optional: Name of the Azure Container Registry. If not provided, script will discover it.

.PARAMETER Version
    Image version tag (default: uses current date and time)

.PARAMETER SkipBuild
    Skip building and only push existing images

.PARAMETER Service
    Build only specific service: frontend, products, cart, orders, checkout, or all (default: all)

.EXAMPLE
    .\build-and-push-decomposed.ps1

.EXAMPLE
    .\build-and-push-decomposed.ps1 -Version "1.0.0"

.EXAMPLE
    .\build-and-push-decomposed.ps1 -Service "frontend"
#>

param(
    [string]$ResourceGroup = "rg-retail-decomposed",
    [string]$AcrName,
    [string]$Version = (Get-Date -Format "yyyyMMdd-HHmmss"),
    [switch]$SkipBuild,
    [ValidateSet("all", "frontend", "products", "cart", "orders", "checkout")]
    [string]$Service = "all"
)

$ErrorActionPreference = "Stop"

# Color functions
function Write-Step {
    param([string]$Message)
    Write-Host "`n▶ $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✓ $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "  ✗ $Message" -ForegroundColor Red
}

# Banner
Write-Host @"
╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║    Build & Push - RetailDecomposed Microservices             ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# Discover ACR if not provided
if ([string]::IsNullOrEmpty($AcrName)) {
    Write-Step "Discovering Azure Container Registry..."
    
    # Check if resource group exists
    $rgExists = az group show --name $ResourceGroup 2>$null
    if (-not $rgExists) {
        Write-Error "Resource group '$ResourceGroup' not found. Please run setup-azure-infrastructure-decomposed.ps1 first."
        exit 1
    }
    
    # Get ACR from resource group
    $acrList = az acr list --resource-group $ResourceGroup --query "[].name" -o tsv
    
    if ([string]::IsNullOrEmpty($acrList)) {
        Write-Error "No Azure Container Registry found in resource group '$ResourceGroup'"
        Write-Host "  Please run setup-azure-infrastructure-decomposed.ps1 first or provide -AcrName parameter" -ForegroundColor Yellow
        exit 1
    }
    
    # Take the first ACR if multiple exist
    $AcrName = ($acrList -split "`n")[0].Trim()
    Write-Success "Discovered ACR: $AcrName"
}

# Configuration
$AcrLoginServer = "$AcrName.azurecr.io"
$WorkingDir = "RetailDecomposed"

# Define microservices
$microservices = @(
    @{Name="frontend"; Dockerfile="Dockerfile.frontend"; Port=8080},
    @{Name="products"; Dockerfile="Dockerfile.products"; Port=8081},
    @{Name="cart"; Dockerfile="Dockerfile.cart"; Port=8082},
    @{Name="orders"; Dockerfile="Dockerfile.orders"; Port=8083},
    @{Name="checkout"; Dockerfile="Dockerfile.checkout"; Port=8084}
)

# Filter services if specific service requested
if ($Service -ne "all") {
    $microservices = $microservices | Where-Object { $_.Name -eq $Service }
}

Write-Host "`nConfiguration:" -ForegroundColor Yellow
Write-Host "  Resource Group:  $ResourceGroup"
Write-Host "  ACR:             $AcrLoginServer"
Write-Host "  Version:         $Version"
Write-Host "  Services:        $($microservices.Count) ($($microservices.Name -join ', '))"
Write-Host "  Working Dir:     $WorkingDir"
Write-Host ""

# Check if Docker is running
Write-Step "Checking Docker..."
try {
    docker version | Out-Null
    Write-Success "Docker is running"
} catch {
    Write-Error "Docker is not running. Please start Docker Desktop."
    exit 1
}

# Check if working directory exists
if (-not (Test-Path $WorkingDir)) {
    Write-Error "RetailDecomposed directory not found: $WorkingDir"
    exit 1
}
Write-Success "RetailDecomposed directory found"

# Login to ACR
Write-Step "Logging in to Azure Container Registry..."
az acr login --name $AcrName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to login to ACR. Please check your Azure login and ACR name."
    exit 1
}
Write-Success "Logged in to $AcrLoginServer"

# Build and push each microservice
foreach ($svc in $microservices) {
    $serviceName = $svc.Name
    $dockerfile = $svc.Dockerfile
    $dockerfilePath = Join-Path $WorkingDir $dockerfile
    $imageName = "retail-decomposed-$serviceName"
    $fullImageName = "$AcrLoginServer/$imageName"
    
    Write-Host "`n$('=' * 70)" -ForegroundColor Cyan
    Write-Host "  Building: $serviceName (Port: $($svc.Port))" -ForegroundColor Cyan
    Write-Host "$('=' * 70)" -ForegroundColor Cyan
    
    # Check if Dockerfile exists
    if (-not (Test-Path $dockerfilePath)) {
        Write-Error "Dockerfile not found: $dockerfilePath"
        continue
    }
    
    if (-not $SkipBuild) {
        # Build Docker image
        Write-Step "Building $serviceName image..."
        Write-Host "  Dockerfile: $dockerfile" -ForegroundColor Yellow
        
        # Check if image already exists
        $existingImage = docker images -q ${fullImageName}:latest 2>$null
        if ($existingImage) {
            Write-Host "  Existing image found - rebuilding..." -ForegroundColor Yellow
        }
        
        # Build from RetailDecomposed directory
        Push-Location $WorkingDir
        try {
            docker build -f $dockerfile -t ${fullImageName}:$Version -t ${fullImageName}:latest .
            
            if ($LASTEXITCODE -ne 0) {
                Write-Error "Docker build failed for $serviceName"
                Pop-Location
                exit 1
            }
            
            Write-Success "Image built successfully: $serviceName"
        } finally {
            Pop-Location
        }
    }
    
    # Push to ACR
    Write-Step "Pushing $serviceName images to ACR..."
    
    Write-Host "  Pushing versioned tag: $Version" -ForegroundColor Yellow
    docker push ${fullImageName}:$Version
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to push versioned image for $serviceName"
        exit 1
    }
    Write-Success "Pushed ${fullImageName}:$Version"
    
    Write-Host "  Pushing latest tag..." -ForegroundColor Yellow
    docker push ${fullImageName}:latest
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to push latest image for $serviceName"
        exit 1
    }
    Write-Success "Pushed ${fullImageName}:latest"
}

# Summary
Write-Host @"

╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║              Build & Push Complete! ✓                        ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Green

Write-Host "Images pushed to ACR:" -ForegroundColor Cyan
foreach ($svc in $microservices) {
    $imageName = "retail-decomposed-$($svc.Name)"
    $fullImageName = "$AcrLoginServer/$imageName"
    Write-Host "  $($svc.Name.PadRight(12)) → ${fullImageName}:$Version" -ForegroundColor Yellow
    Write-Host "  $(' '.PadRight(12))   ${fullImageName}:latest" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Create Kubernetes manifests in k8s/decomposed/ directory"
Write-Host "     - namespace.yaml"
Write-Host "     - configmap.yaml"
Write-Host "     - secrets.yaml (from secrets-template.yaml)"
Write-Host "     - deployments (frontend, products, cart, orders, checkout)"
Write-Host "     - services (for each microservice)"
Write-Host "     - ingress.yaml"
Write-Host ""
Write-Host "  2. Deploy to AKS:"
Write-Host "     .\deploy-decomposed.ps1"
Write-Host ""

Write-Success "Build and push completed for $($microservices.Count) service(s)"
