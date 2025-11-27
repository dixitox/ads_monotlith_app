# build-and-push-monolith.ps1
# Build Docker image for RetailMonolith and push to ACR

<#
.SYNOPSIS
    Builds and pushes RetailMonolith Docker image to Azure Container Registry

.DESCRIPTION
    This script:
    - Builds the Docker image using Dockerfile.monolith
    - Tags the image with version and 'latest'
    - Pushes the image to Azure Container Registry

.PARAMETER ResourceGroup
    Name of the Azure Resource Group (default: rg-retail-monolith)

.PARAMETER AcrName
    Optional: Name of the Azure Container Registry. If not provided, script will discover it.

.PARAMETER Version
    Image version tag (default: uses current date and time)

.PARAMETER SkipBuild
    Skip building and only push existing image

.EXAMPLE
    .\build-and-push-monolith.ps1

.EXAMPLE
    .\build-and-push-monolith.ps1 -ResourceGroup "rg-retail-monolith" -Version "1.0.0"

.EXAMPLE
    .\build-and-push-monolith.ps1 -AcrName "acrretailmonolith" -Version "1.0.0"
#>

param(
    [string]$ResourceGroup = "rg-retail-monolith",
    
    [string]$AcrName,
    
    [string]$Version = (Get-Date -Format "yyyyMMdd-HHmmss"),
    
    [switch]$SkipBuild
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
║        Build & Push - RetailMonolith Docker Image            ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# Discover ACR if not provided
if ([string]::IsNullOrEmpty($AcrName)) {
    Write-Step "Discovering Azure Container Registry..."
    
    # Check if resource group exists
    $rgExists = az group show --name $ResourceGroup 2>$null
    if (-not $rgExists) {
        Write-Error "Resource group '$ResourceGroup' not found. Please run setup-azure-infrastructure-monolith.ps1 first."
        exit 1
    }
    
    # Get ACR from resource group
    $acrList = az acr list --resource-group $ResourceGroup --query "[].name" -o tsv
    
    if ([string]::IsNullOrEmpty($acrList)) {
        Write-Error "No Azure Container Registry found in resource group '$ResourceGroup'"
        Write-Host "  Please run setup-azure-infrastructure-monolith.ps1 first or provide -AcrName parameter" -ForegroundColor Yellow
        exit 1
    }
    
    # Take the first ACR if multiple exist
    $AcrName = ($acrList -split "`n")[0].Trim()
    Write-Success "Discovered ACR: $AcrName"
}

# Configuration
$ImageName = "retail-monolith"
$AcrLoginServer = "$AcrName.azurecr.io"
$FullImageName = "$AcrLoginServer/$ImageName"
$DockerfilePath = "Dockerfile.monolith"

Write-Host "`nConfiguration:" -ForegroundColor Yellow
Write-Host "  Resource Group: $ResourceGroup"
Write-Host "  ACR:            $AcrLoginServer"
Write-Host "  Image:          $ImageName"
Write-Host "  Version:        $Version"
Write-Host "  Dockerfile:     $DockerfilePath"
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

# Check if Dockerfile exists
if (-not (Test-Path $DockerfilePath)) {
    Write-Error "Dockerfile not found: $DockerfilePath"
    exit 1
}
Write-Success "Dockerfile found"

# Login to ACR
Write-Step "Logging in to Azure Container Registry..."
az acr login --name $AcrName
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to login to ACR. Please check your Azure login and ACR name."
    exit 1
}
Write-Success "Logged in to $AcrLoginServer"

if (-not $SkipBuild) {
    # Build Docker image
    Write-Step "Building Docker image..."
    Write-Host "  This may take a few minutes..." -ForegroundColor Yellow
    
    # Check if image already exists
    $existingImage = docker images -q ${FullImageName}:latest 2>$null
    if ($existingImage) {
        Write-Host "  Existing image found - rebuilding..." -ForegroundColor Yellow
    }
    
    docker build -f $DockerfilePath -t ${FullImageName}:$Version -t ${FullImageName}:latest .
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Docker build failed"
        exit 1
    }
    
    Write-Success "Image built successfully"
    
    # Show image details
    Write-Step "Image details:"
    docker images $FullImageName --format "table {{.Repository}}\t{{.Tag}}\t{{.Size}}\t{{.CreatedAt}}"
}

# Push to ACR
Write-Step "Pushing image to ACR..."

Write-Host "  Pushing versioned tag: $Version" -ForegroundColor Yellow
docker push ${FullImageName}:$Version
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to push versioned image"
    exit 1
}
Write-Success "Pushed ${FullImageName}:$Version"

Write-Host "  Pushing latest tag..." -ForegroundColor Yellow
docker push ${FullImageName}:latest
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to push latest image"
    exit 1
}
Write-Success "Pushed ${FullImageName}:latest"

# Summary
Write-Host @"

╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║                  Build & Push Complete! ✓                    ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Green

Write-Host "Images pushed to ACR:" -ForegroundColor Cyan
Write-Host "  ${FullImageName}:$Version" -ForegroundColor Yellow
Write-Host "  ${FullImageName}:latest" -ForegroundColor Yellow
Write-Host ""

Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Update k8s/monolith/deployment.yaml if needed:"
Write-Host "     image: ${FullImageName}:latest"
Write-Host ""
Write-Host "  2. Create Kubernetes secrets (if not done yet):"
Write-Host "     Copy k8s/monolith/secrets-template.yaml to secrets.yaml"
Write-Host "     Update with your base64-encoded credentials"
Write-Host "     kubectl apply -f k8s/monolith/secrets.yaml"
Write-Host ""
Write-Host "  3. Deploy to AKS:"
Write-Host "     .\deploy-monolith.ps1"
Write-Host ""
