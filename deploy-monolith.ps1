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
#>

param(
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

# Check kubectl
Write-Step "Checking kubectl configuration..."
try {
    $currentContext = kubectl config current-context
    Write-Success "kubectl context: $currentContext"
} catch {
    Write-Error "kubectl not configured. Run 'az aks get-credentials' first."
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
Write-Step "Creating namespace..."
kubectl apply -f k8s/monolith/namespace.yaml
Write-Success "Namespace '$Namespace' ready"

# Apply ConfigMap
Write-Step "Applying ConfigMap..."
kubectl apply -f k8s/monolith/configmap.yaml
Write-Success "ConfigMap applied"

# Apply Service Account
Write-Step "Applying Service Account (for Azure AD authentication)..."
kubectl apply -f k8s/monolith/serviceaccount.yaml
Write-Success "Service Account applied"

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
    Write-Success "Secrets applied"
}

# Apply Deployment
Write-Step "Deploying application..."
kubectl apply -f k8s/monolith/deployment.yaml
Write-Success "Deployment applied"

# Apply Service
Write-Step "Creating service..."
kubectl apply -f k8s/monolith/service.yaml
Write-Success "Service applied"

# Apply Ingress
Write-Step "Creating ingress..."
kubectl apply -f k8s/monolith/ingress.yaml
Write-Success "Ingress applied"

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
