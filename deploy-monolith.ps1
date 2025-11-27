# deploy-monolith.ps1
# Deploy RetailMonolith to Azure Kubernetes Service

<#
.SYNOPSIS
    Deploys RetailMonolith application to AKS

.DESCRIPTION
    This script:
    - Verifies kubectl is configured
    - Installs NGINX Ingress Controller (if not present)
    - Creates namespace
    - Applies ConfigMap
    - Applies Secrets (you must create this file first)
    - Applies Deployment
    - Applies Service
    - Applies Ingress
    - Runs database migrations
    - Shows deployment status and access information

.PARAMETER ResourceGroup
    Azure Resource Group name (default: rg-retail-monolith)

.PARAMETER AksName
    Optional: AKS cluster name. If not provided, script will discover it.

.PARAMETER Namespace
    Kubernetes namespace (default: retail-monolith)

.PARAMETER SkipIngressInstall
    Skip NGINX Ingress Controller installation

.PARAMETER WaitForReady
    Wait for pods to be ready before exiting

.EXAMPLE
    .\deploy-monolith.ps1

.EXAMPLE
    .\deploy-monolith.ps1 -WaitForReady

.EXAMPLE
    .\deploy-monolith.ps1 -ResourceGroup "my-rg" -AksName "my-aks"
#>

param(
    [string]$ResourceGroup = "rg-retail-monolith",
    [string]$AksName,
    [string]$Namespace = "retail-monolith",
    [switch]$SkipIngressInstall,
    [switch]$WaitForReady
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

function Write-Warning {
    param([string]$Message)
    Write-Host "  ⚠ $Message" -ForegroundColor Yellow
}

# Banner
Write-Host @"
╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║           Deploy RetailMonolith to AKS                       ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# Discover AKS cluster if not provided
if ([string]::IsNullOrEmpty($AksName)) {
    Write-Step "Discovering AKS cluster..."
    
    # Check if resource group exists
    $rgExists = az group show --name $ResourceGroup 2>$null
    if (-not $rgExists) {
        Write-Error "Resource group '$ResourceGroup' not found. Please run setup-azure-infrastructure-monolith.ps1 first."
        exit 1
    }
    
    # Get AKS cluster from resource group
    $aksList = az aks list --resource-group $ResourceGroup --query "[].name" -o tsv
    
    if ([string]::IsNullOrEmpty($aksList)) {
        Write-Error "No AKS cluster found in resource group '$ResourceGroup'"
        Write-Host "  Please run setup-azure-infrastructure-monolith.ps1 first or provide -AksName parameter" -ForegroundColor Yellow
        exit 1
    }
    
    # Take the first AKS if multiple exist
    $AksName = ($aksList -split "`n")[0].Trim()
    Write-Success "Discovered AKS cluster: $AksName"
}

# Check and configure SQL Server network access
Write-Step "Configuring SQL Server network access..."
$sqlServers = az sql server list --resource-group $ResourceGroup --query "[].name" -o tsv
if (-not [string]::IsNullOrEmpty($sqlServers)) {
    $sqlServerName = ($sqlServers -split "`n")[0].Trim()
    Write-Host "  Found SQL Server: $sqlServerName" -ForegroundColor Yellow
    
    # Check public network access status
    $publicAccess = az sql server show --resource-group $ResourceGroup --name $sqlServerName --query "publicNetworkAccess" -o tsv 2>$null
    
    if ($publicAccess -eq "Disabled") {
        Write-Warning "SQL Server public network access is disabled. Enabling..."
        az sql server update --resource-group $ResourceGroup --name $sqlServerName --enable-public-network true 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Success "SQL Server public network access enabled"
        } else {
            Write-Warning "Failed to enable public network access"
        }
    } else {
        Write-Success "SQL Server public network access is enabled"
    }
    
    # Verify firewall rule for Azure services
    $firewallRule = az sql server firewall-rule show --resource-group $ResourceGroup --server $sqlServerName --name AllowAzureServices 2>$null
    if (-not $firewallRule) {
        Write-Warning "Azure services firewall rule not found. Creating..."
        az sql server firewall-rule create --resource-group $ResourceGroup --server $sqlServerName --name AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 2>&1 | Out-Null
        Write-Success "Azure services firewall rule created"
    } else {
        Write-Success "Azure services firewall rule exists"
    }
} else {
    Write-Warning "No SQL Server found in resource group"
}

# Check AKS cluster status
Write-Step "Checking AKS cluster status..."
$aksStatus = az aks show --resource-group $ResourceGroup --name $AksName --query "powerState.code" -o tsv 2>$null

if ($aksStatus -eq "Stopped") {
    Write-Warning "AKS cluster is stopped. Starting cluster..."
    Write-Host "  This may take 5-10 minutes..." -ForegroundColor Yellow
    
    az aks start --resource-group $ResourceGroup --name $AksName
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to start AKS cluster"
        exit 1
    }
    Write-Success "AKS cluster started"
} elseif ($aksStatus -eq "Running") {
    Write-Success "AKS cluster is running"
} else {
    Write-Warning "AKS cluster status: $aksStatus"
}

# Get AKS credentials (force refresh)
Write-Step "Connecting to AKS cluster..."
Write-Host "  Getting credentials for: $AksName" -ForegroundColor Yellow

# Clear any cached/stale kubectl config for this cluster
$existingContext = kubectl config get-contexts -o name 2>$null | Where-Object { $_ -match $AksName }
if ($existingContext) {
    Write-Host "  Removing stale kubectl context..." -ForegroundColor Yellow
    kubectl config delete-context $existingContext 2>&1 | Out-Null
}

# Get fresh credentials
az aks get-credentials --resource-group $ResourceGroup --name $AksName --overwrite-existing

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to get AKS credentials"
    exit 1
}
Write-Success "Connected to AKS cluster"

# Verify kubectl connection
Write-Step "Verifying kubectl connection..."
try {
    $currentContext = kubectl config current-context
    Write-Success "kubectl context: $currentContext"
    
    # Test actual connectivity with retries (cluster might need a moment after starting)
    Write-Host "  Testing cluster connectivity..." -ForegroundColor Yellow
    $maxRetries = 3
    $retryCount = 0
    $connected = $false
    
    while ($retryCount -lt $maxRetries -and -not $connected) {
        kubectl cluster-info 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            $connected = $true
            Write-Success "Cluster is reachable"
        } else {
            $retryCount++
            if ($retryCount -lt $maxRetries) {
                Write-Host "  Retry $retryCount/$maxRetries..." -ForegroundColor Yellow
                Start-Sleep -Seconds 10
            }
        }
    }
    
    if (-not $connected) {
        Write-Error "Cannot connect to cluster after $maxRetries attempts."
        Write-Host "  Try running: az aks start --resource-group $ResourceGroup --name $AksName" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Error "kubectl not configured properly."
    exit 1
}

# Verify cluster connectivity
try {
    kubectl get nodes | Out-Null
    Write-Success "Connected to cluster"
} catch {
    Write-Error "Cannot connect to cluster. Check your kubectl configuration."
    exit 1
}

# Install NGINX Ingress Controller
if (-not $SkipIngressInstall) {
    Write-Step "Checking NGINX Ingress Controller..."
    
    $ingressNamespace = kubectl get namespace ingress-nginx --ignore-not-found
    
    if (-not $ingressNamespace) {
        Write-Warning "NGINX Ingress Controller not found. Installing..."
        
        kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.2/deploy/static/provider/cloud/deploy.yaml
        
        Write-Host "  Waiting for Ingress Controller to be ready (this may take 2-3 minutes)..." -ForegroundColor Yellow
        kubectl wait --namespace ingress-nginx `
            --for=condition=ready pod `
            --selector=app.kubernetes.io/component=controller `
            --timeout=300s
        
        Write-Success "NGINX Ingress Controller installed"
    } else {
        Write-Success "NGINX Ingress Controller already installed"
    }
}

# Create namespace
Write-Step "Creating/Verifying namespace..."
$existingNs = kubectl get namespace $Namespace --ignore-not-found 2>$null
if ($existingNs) {
    Write-Success "Namespace '$Namespace' already exists"
} else {
    kubectl apply -f k8s/monolith/namespace.yaml
    Write-Success "Namespace '$Namespace' created"
}

# Apply ConfigMap
Write-Step "Applying/Updating ConfigMap..."
kubectl apply -f k8s/monolith/configmap.yaml
if ($LASTEXITCODE -eq 0) {
    Write-Success "ConfigMap applied"
} else {
    Write-Error "Failed to apply ConfigMap"
    exit 1
}

# Apply Service Account
Write-Step "Applying/Updating Service Account (for Azure AD authentication)..."
kubectl apply -f k8s/monolith/serviceaccount.yaml
if ($LASTEXITCODE -eq 0) {
    Write-Success "Service Account applied"
} else {
    Write-Error "Failed to apply Service Account"
    exit 1
}

# Check for secrets file
Write-Step "Checking secrets..."
$secretsFile = "k8s/monolith/secrets.yaml"

if (-not (Test-Path $secretsFile)) {
    Write-Warning "Secrets file not found: $secretsFile"
    Write-Host ""
    Write-Host "  You need to create the secrets file:" -ForegroundColor Yellow
    Write-Host "  1. Copy k8s/monolith/secrets-template.yaml to k8s/monolith/secrets.yaml"
    Write-Host "  2. Replace BASE64_ENCODED values with your actual credentials"
    Write-Host ""
    Write-Host "  To encode values in PowerShell:"
    Write-Host "    [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes('your-value'))" -ForegroundColor Cyan
    Write-Host ""
    
    $createSecrets = Read-Host "Create secrets file now? (yes/no)"
    if ($createSecrets -eq "yes") {
        Copy-Item "k8s/monolith/secrets-template.yaml" $secretsFile
        Write-Success "Created $secretsFile - please edit it with your credentials"
        
        # Open in default editor
        if (Get-Command code -ErrorAction SilentlyContinue) {
            code $secretsFile
        } elseif (Get-Command notepad -ErrorAction SilentlyContinue) {
            notepad $secretsFile
        }
        
        Write-Host ""
        Write-Host "Edit the secrets file, then run this script again." -ForegroundColor Yellow
        exit 0
    } else {
        Write-Error "Cannot proceed without secrets file"
        exit 1
    }
} else {
    kubectl apply -f $secretsFile
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Secrets applied/updated"
    } else {
        Write-Error "Failed to apply Secrets"
        exit 1
    }
}

# Apply Deployment
Write-Step "Deploying/Updating application..."
kubectl apply -f k8s/monolith/deployment.yaml
if ($LASTEXITCODE -eq 0) {
    Write-Success "Deployment applied"
    
    # Check if this is an update
    Write-Host "  Waiting for rollout to complete..." -ForegroundColor Yellow
    kubectl rollout status deployment/retail-monolith -n $Namespace --timeout=300s 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Rollout completed successfully"
    }
} else {
    Write-Error "Failed to apply Deployment"
    exit 1
}

# Apply Service
Write-Step "Creating/Updating service..."
kubectl apply -f k8s/monolith/service.yaml
if ($LASTEXITCODE -eq 0) {
    Write-Success "Service applied"
} else {
    Write-Error "Failed to apply Service"
    exit 1
}

# Apply Ingress
Write-Step "Creating/Updating ingress..."
kubectl apply -f k8s/monolith/ingress.yaml
if ($LASTEXITCODE -eq 0) {
    Write-Success "Ingress applied"
} else {
    Write-Error "Failed to apply Ingress"
    exit 1
}

# Wait for pods to be ready
if ($WaitForReady) {
    Write-Step "Waiting for pods to be ready..."
    kubectl wait --namespace $Namespace `
        --for=condition=ready pod `
        --selector=app=retail-monolith `
        --timeout=300s
    Write-Success "Pods are ready"
}

# Show deployment status
Write-Step "Deployment Status:"
Write-Host ""
kubectl get all -n $Namespace
Write-Host ""

# Get Ingress IP
Write-Step "Getting Ingress information..."
Write-Host "  Waiting for external IP assignment (this may take 1-2 minutes)..." -ForegroundColor Yellow

$maxAttempts = 30
$attempt = 0
$externalIP = ""

while ($attempt -lt $maxAttempts -and -not $externalIP) {
    Start-Sleep -Seconds 10
    $attempt++
    
    $ingressInfo = kubectl get ingress -n $Namespace -o json | ConvertFrom-Json
    
    if ($ingressInfo.items.Count -gt 0) {
        $loadBalancer = $ingressInfo.items[0].status.loadBalancer.ingress
        if ($loadBalancer) {
            $externalIP = $loadBalancer[0].ip
        }
    }
    
    if (-not $externalIP) {
        Write-Host "  Attempt $attempt/$maxAttempts - waiting for IP..." -ForegroundColor Yellow
    }
}

# Summary
Write-Host @"

╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║                 Deployment Complete! ✓                       ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Green

Write-Host "Access Information:" -ForegroundColor Cyan

if ($externalIP) {
    Write-Host "  Application URL: http://$externalIP" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Opening in browser..." -ForegroundColor Yellow
    Start-Process "http://$externalIP"
} else {
    Write-Warning "External IP not yet assigned. Check later with:"
    Write-Host "    kubectl get ingress -n $Namespace" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Useful Commands:" -ForegroundColor Cyan
Write-Host "  View pods:        kubectl get pods -n $Namespace" -ForegroundColor Yellow
Write-Host "  View logs:        kubectl logs -n $Namespace -l app=retail-monolith --tail=50 -f" -ForegroundColor Yellow
Write-Host "  View services:    kubectl get svc -n $Namespace" -ForegroundColor Yellow
Write-Host "  View ingress:     kubectl get ingress -n $Namespace" -ForegroundColor Yellow
Write-Host "  Describe pod:     kubectl describe pod -n $Namespace <pod-name>" -ForegroundColor Yellow
Write-Host "  Shell into pod:   kubectl exec -n $Namespace -it <pod-name> -- /bin/bash" -ForegroundColor Yellow
Write-Host ""

Write-Host "Monitoring:" -ForegroundColor Cyan
Write-Host "  Watch pods:       kubectl get pods -n $Namespace -w" -ForegroundColor Yellow
Write-Host "  Stream all logs:  kubectl logs -n $Namespace -l app=retail-monolith --all-containers --tail=100 -f" -ForegroundColor Yellow
Write-Host ""

Write-Host "Troubleshooting:" -ForegroundColor Cyan
Write-Host "  If pods are not starting, check:"
Write-Host "    kubectl describe pod -n $Namespace <pod-name>" -ForegroundColor Yellow
Write-Host "    kubectl logs -n $Namespace <pod-name>" -ForegroundColor Yellow
Write-Host ""
Write-Host "  If database connection fails, verify:"
Write-Host "    - SQL Server firewall allows AKS cluster"
Write-Host "    - Connection string in secrets is correct"
Write-Host "    - ConfigMap has correct SQL_SERVER value"
Write-Host ""
