# deploy-decomposed.ps1
# Deploy RetailDecomposed Microservices to Azure Kubernetes Service

<#
.SYNOPSIS
    Deploys RetailDecomposed microservices application to AKS

.DESCRIPTION
    This script:
    - Verifies/connects to AKS cluster
    - Checks SQL Server network access
    - Installs NGINX Ingress Controller (if not present)
    - Creates namespace
    - Applies ConfigMap
    - Applies Secrets (you must create secrets.yaml first)
    - Deploys all 5 microservices (Products, Cart, Orders, Checkout, Frontend)
    - Applies Services
    - Applies Ingress
    - Shows deployment status and access information

.PARAMETER ResourceGroup
    Azure Resource Group name (default: rg-retail-decomposed)

.PARAMETER AksName
    Optional: AKS cluster name. If not provided, script will discover it.

.PARAMETER Namespace
    Kubernetes namespace (default: retail-decomposed)

.PARAMETER SkipIngressInstall
    Skip NGINX Ingress Controller installation

.PARAMETER WaitForReady
    Wait for all pods to be ready before exiting

.EXAMPLE
    .\deploy-decomposed.ps1

.EXAMPLE
    .\deploy-decomposed.ps1 -WaitForReady

.EXAMPLE
    .\deploy-decomposed.ps1 -ResourceGroup "my-rg" -AksName "my-aks"
#>

param(
    [string]$ResourceGroup = "rg-retail-decomposed",
    [string]$AksName,
    [string]$Namespace = "retail-decomposed",
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
║       Deploy RetailDecomposed to AKS                         ║
║          Microservices Architecture                           ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# Discover AKS cluster if not provided
if ([string]::IsNullOrEmpty($AksName)) {
    Write-Step "Discovering AKS cluster..."
    
    # Check if resource group exists
    $rgExists = az group show --name $ResourceGroup 2>$null
    if (-not $rgExists) {
        Write-Error "Resource group '$ResourceGroup' not found. Please run setup-azure-infrastructure-decomposed.ps1 first."
        exit 1
    }
    
    # Get AKS cluster from resource group
    $aksList = az aks list --resource-group $ResourceGroup --query "[].name" -o tsv
    
    if ([string]::IsNullOrEmpty($aksList)) {
        Write-Error "No AKS cluster found in resource group '$ResourceGroup'"
        Write-Host "  Please run setup-azure-infrastructure-decomposed.ps1 first or provide -AksName parameter" -ForegroundColor Yellow
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
    
    # Grant AKS managed identity access to SQL Server
    Write-Step "Configuring AKS managed identity SQL Server access..."
    $aksIdentityObjectId = az aks show --resource-group $ResourceGroup --name $AksName --query "identityProfile.kubeletidentity.objectId" -o tsv 2>$null
    
    if ($aksIdentityObjectId) {
        Write-Host "  AKS Identity Object ID: $aksIdentityObjectId" -ForegroundColor Yellow
        
        # Check if identity is already an admin
        $currentAdmin = az sql server ad-admin show --resource-group $ResourceGroup --server-name $sqlServerName --query "sid" -o tsv 2>$null
        
        if ($currentAdmin -ne $aksIdentityObjectId) {
            Write-Host "  Setting AKS identity as Azure AD admin for SQL Server..." -ForegroundColor Yellow
            az sql server ad-admin create --resource-group $ResourceGroup --server-name $sqlServerName --display-name "AKS-Identity" --object-id $aksIdentityObjectId 2>&1 | Out-Null
            
            if ($LASTEXITCODE -eq 0) {
                Write-Success "AKS identity granted SQL Server access"
            } else {
                Write-Warning "Failed to grant SQL Server access - pods may fail to connect to database"
            }
        } else {
            Write-Success "AKS identity already has SQL Server access"
        }
    } else {
        Write-Warning "Could not retrieve AKS identity - manual SQL permission configuration may be needed"
    }
} else {
    Write-Warning "No SQL Server found in resource group"
}

# Grant AKS managed identity access to Azure AI resources
Write-Step "Configuring Azure AI permissions..."
if ($aksIdentityObjectId) {
    # Find Azure AI Foundry (Cognitive Services account)
    Write-Host "  Searching for Azure AI resources..." -ForegroundColor Yellow
    $aiAccount = az cognitiveservices account list --query "[?contains(name, 'foundry') || contains(name, 'retail-app')].{name:name, resourceGroup:resourceGroup}" -o json 2>$null | ConvertFrom-Json | Select-Object -First 1
    
    if ($aiAccount) {
        Write-Host "  Found Azure AI account: $($aiAccount.name) in $($aiAccount.resourceGroup)" -ForegroundColor Yellow
        
        # Grant Cognitive Services OpenAI User role
        $aiScope = "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$($aiAccount.resourceGroup)/providers/Microsoft.CognitiveServices/accounts/$($aiAccount.name)"
        
        $existingRole = az role assignment list --assignee $aksIdentityObjectId --scope $aiScope --role "Cognitive Services OpenAI User" --query "[].id" -o tsv 2>$null
        
        if (-not $existingRole) {
            Write-Host "  Granting 'Cognitive Services OpenAI User' role..." -ForegroundColor Yellow
            az role assignment create --assignee $aksIdentityObjectId --role "Cognitive Services OpenAI User" --scope $aiScope 2>&1 | Out-Null
            
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Azure AI OpenAI permissions granted"
            } else {
                Write-Warning "Failed to grant Azure AI permissions - AI features may not work"
            }
        } else {
            Write-Success "Azure AI OpenAI permissions already configured"
        }
    } else {
        Write-Warning "No Azure AI Foundry account found - AI features may not work"
    }
    
    # Find Azure AI Search service
    $searchService = az search service list --query "[?contains(name, 'retail')].{name:name, resourceGroup:resourceGroup}" -o json 2>$null | ConvertFrom-Json | Select-Object -First 1
    
    if ($searchService) {
        Write-Host "  Found Azure AI Search: $($searchService.name) in $($searchService.resourceGroup)" -ForegroundColor Yellow
        
        # Grant Search Index Data Contributor role
        $searchScope = "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$($searchService.resourceGroup)/providers/Microsoft.Search/searchServices/$($searchService.name)"
        
        $existingSearchRole = az role assignment list --assignee $aksIdentityObjectId --scope $searchScope --role "Search Index Data Contributor" --query "[].id" -o tsv 2>$null
        
        if (-not $existingSearchRole) {
            Write-Host "  Granting 'Search Index Data Contributor' role..." -ForegroundColor Yellow
            az role assignment create --assignee $aksIdentityObjectId --role "Search Index Data Contributor" --scope $searchScope 2>&1 | Out-Null
            
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Azure AI Search permissions granted"
            } else {
                Write-Warning "Failed to grant Search permissions - semantic search may not work"
            }
        } else {
            Write-Success "Azure AI Search permissions already configured"
        }
    } else {
        Write-Warning "No Azure AI Search service found - semantic search may not work"
    }
} else {
    Write-Warning "Could not configure Azure AI permissions - AKS identity not found"
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

# Install NGINX Ingress Controller
if (-not $SkipIngressInstall) {
    Write-Step "Checking NGINX Ingress Controller..."
    $nginxInstalled = kubectl get namespace ingress-nginx --ignore-not-found 2>$null
    
    if (-not $nginxInstalled) {
        Write-Host "  Installing NGINX Ingress Controller..." -ForegroundColor Yellow
        Write-Host "  This may take 2-3 minutes..." -ForegroundColor Yellow
        
        kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.2/deploy/static/provider/cloud/deploy.yaml
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "NGINX Ingress Controller installed"
            
            # Wait for ingress controller to be ready
            Write-Host "  Waiting for ingress controller to be ready..." -ForegroundColor Yellow
            kubectl wait --namespace ingress-nginx `
                --for=condition=ready pod `
                --selector=app.kubernetes.io/component=controller `
                --timeout=180s 2>&1 | Out-Null
            
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Ingress controller is ready"
            } else {
                Write-Warning "Ingress controller may still be initializing"
            }
        } else {
            Write-Error "Failed to install NGINX Ingress Controller"
            exit 1
        }
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
    kubectl apply -f k8s/decomposed/namespace.yaml
    Write-Success "Namespace '$Namespace' created"
}

# Apply ConfigMap
Write-Step "Applying/Updating ConfigMap..."
kubectl apply -f k8s/decomposed/configmap.yaml
if ($LASTEXITCODE -eq 0) {
    Write-Success "ConfigMap applied"
} else {
    Write-Error "Failed to apply ConfigMap"
    exit 1
}

# Check for secrets file
$secretsFile = "k8s/decomposed/secrets.yaml"
if (-not (Test-Path $secretsFile)) {
    Write-Warning "Secrets file not found: $secretsFile"
    Write-Host @"
  
  You need to create k8s/decomposed/secrets.yaml from the template:
  
  1. Copy: k8s/decomposed/secrets-template.yaml → k8s/decomposed/secrets.yaml
  
  2. Encode your values with PowerShell:
     [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("your-value"))
  
  3. Update secrets.yaml with encoded values
  
  4. Run this script again
  
  Alternatively, run: .\get-secrets-values.ps1 for detailed instructions
  
"@ -ForegroundColor Yellow
    exit 1
} else {
    # Validate critical secrets are not empty
    Write-Host "  Validating secrets..." -ForegroundColor Yellow
    $secretContent = Get-Content $secretsFile -Raw
    $hasEmptySecrets = $false
    
    # Check for empty critical values (just looking for empty quotes after colon)
    if ($secretContent -match 'ConnectionStrings__DefaultConnection:\s*""') {
        Write-Warning "Database connection string is empty in secrets.yaml"
        $hasEmptySecrets = $true
    }
    if ($secretContent -match 'AzureAd__ClientId:\s*""') {
        Write-Warning "Azure AD ClientId is empty - authentication may not work"
        Write-Host "  App will run but sign-in will fail. Update secrets.yaml with real app registration." -ForegroundColor Yellow
    }
    if ($secretContent -match 'AzureAd__ClientSecret:\s*""') {
        Write-Warning "Azure AD ClientSecret is empty - authentication may not work"
        Write-Host "  App will run but sign-in will fail. Update secrets.yaml with real app registration." -ForegroundColor Yellow
    }
    
    if ($hasEmptySecrets) {
        Write-Error "Critical secrets are empty. Please update secrets.yaml"
        Write-Host "  Run: .\get-secrets-values.ps1 for help getting values" -ForegroundColor Yellow
        exit 1
    }
    
    # Validate Azure AI endpoint format
    Write-Host "  Validating Azure AI configuration..." -ForegroundColor Yellow
    if ($secretContent -match 'AzureAI__Endpoint:\s*"([^"]+)"') {
        $encodedEndpoint = $matches[1]
        try {
            $decodedEndpoint = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($encodedEndpoint))
            
            # Check if using incorrect AI Foundry project endpoint
            if ($decodedEndpoint -match '\.services\.ai\.azure\.com/api/projects/') {
                Write-Warning "Azure AI endpoint uses project URL format - this may cause authentication issues"
                Write-Host "  Current: $decodedEndpoint" -ForegroundColor Yellow
                
                # Try to suggest correct endpoint
                $aiAccount = az cognitiveservices account list --query "[?contains(name, 'foundry') || contains(name, 'retail-app')].{name:name, endpoint:properties.endpoint}" -o json 2>$null | ConvertFrom-Json | Select-Object -First 1
                
                if ($aiAccount -and $aiAccount.endpoint) {
                    Write-Host "  Recommended: $($aiAccount.endpoint)" -ForegroundColor Green
                    Write-Host "  " -NoNewline
                    Write-Host "⚠ " -ForegroundColor Red -NoNewline
                    Write-Host "Use Cognitive Services endpoint (.cognitiveservices.azure.com) not project endpoint" -ForegroundColor Yellow
                    Write-Host ""
                    Write-Host "  To fix, update secrets.yaml AzureAI__Endpoint with:" -ForegroundColor Yellow
                    $correctEncoded = [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($aiAccount.endpoint))
                    Write-Host "  $correctEncoded" -ForegroundColor Green
                    Write-Host ""
                }
            } elseif ($decodedEndpoint -match '\.cognitiveservices\.azure\.com') {
                Write-Success "Azure AI endpoint format is correct"
            } else {
                Write-Warning "Azure AI endpoint format not recognized: $decodedEndpoint"
            }
        } catch {
            Write-Warning "Could not decode Azure AI endpoint for validation"
        }
    }
    
    kubectl apply -f $secretsFile
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Secrets applied/updated"
    } else {
        Write-Error "Failed to apply Secrets"
        exit 1
    }
}

# Deploy microservices in order
$services = @(
    @{Name="Products"; File="products-deployment.yaml"},
    @{Name="Orders"; File="orders-deployment.yaml"},
    @{Name="Cart"; File="cart-deployment.yaml"},
    @{Name="Checkout"; File="checkout-deployment.yaml"},
    @{Name="Frontend"; File="frontend-deployment.yaml"}
)

Write-Host "`n$('=' * 70)" -ForegroundColor Cyan
Write-Host "  Deploying Microservices" -ForegroundColor Cyan
Write-Host "$('=' * 70)" -ForegroundColor Cyan

foreach ($svc in $services) {
    Write-Step "Deploying $($svc.Name) service..."
    kubectl apply -f "k8s/decomposed/$($svc.File)"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "$($svc.Name) service deployed"
    } else {
        Write-Error "Failed to deploy $($svc.Name) service"
        exit 1
    }
}

# Apply Ingress
Write-Step "Creating/Updating ingress..."
kubectl apply -f k8s/decomposed/ingress.yaml
if ($LASTEXITCODE -eq 0) {
    Write-Success "Ingress applied"
} else {
    Write-Error "Failed to apply Ingress"
    exit 1
}

# Wait for deployments
if ($WaitForReady) {
    Write-Step "Waiting for all deployments to be ready..."
    foreach ($svc in $services) {
        $deploymentName = ($svc.Name).ToLower() + "-service"
        Write-Host "  Waiting for $deploymentName..." -ForegroundColor Yellow
        kubectl rollout status deployment/$deploymentName -n $Namespace --timeout=300s 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Success "$deploymentName is ready"
        } else {
            Write-Warning "$deploymentName may still be starting"
        }
    }
}

# Get deployment status
Write-Step "Deployment Status..."
kubectl get pods -n $Namespace
Write-Host ""

# Get ingress info
Write-Step "Getting ingress information..."
Start-Sleep -Seconds 5
$ingressInfo = kubectl get ingress -n $Namespace -o json | ConvertFrom-Json

if ($ingressInfo.items.Count -gt 0) {
    $ingress = $ingressInfo.items[0]
    $ingressIp = $ingress.status.loadBalancer.ingress[0].ip
    
    if ($ingressIp) {
        Write-Success "Ingress IP: $ingressIp"
        
        # Configure Azure AD redirect URIs
        Write-Step "Configuring Azure AD redirect URIs..."
        
        # Get ClientId from secrets
        $secretData = kubectl get secret retail-decomposed-secrets -n $Namespace -o json 2>$null | ConvertFrom-Json
        if ($secretData) {
            $encodedClientId = $secretData.data.'AzureAd__ClientId'
            if ($encodedClientId) {
                $clientId = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($encodedClientId))
                
                # Check if ClientId is valid (not placeholder)
                if ($clientId -match '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$' -and $clientId -ne "00000000-0000-0000-0000-000000000000") {
                    Write-Host "  Found Azure AD Client ID: $clientId" -ForegroundColor Yellow
                    
                    # Get existing redirect URIs
                    $existingUris = az ad app show --id $clientId --query "web.redirectUris" -o json 2>$null
                    if ($existingUris) {
                        $uriList = $existingUris | ConvertFrom-Json
                        $httpUri = "http://$ingressIp/signin-oidc"
                        $httpsUri = "https://$ingressIp/signin-oidc"
                        
                        # Check if URIs need to be added
                        $needsUpdate = $false
                        if ($uriList -notcontains $httpUri) {
                            Write-Host "  Adding redirect URI: $httpUri" -ForegroundColor Yellow
                            $uriList += $httpUri
                            $needsUpdate = $true
                        }
                        if ($uriList -notcontains $httpsUri) {
                            Write-Host "  Adding redirect URI: $httpsUri" -ForegroundColor Yellow
                            $uriList += $httpsUri
                            $needsUpdate = $true
                        }
                        
                        if ($needsUpdate) {
                            $uriArgs = $uriList | ForEach-Object { "`"$_`"" }
                            $uriString = $uriArgs -join " "
                            $updateCmd = "az ad app update --id $clientId --web-redirect-uris $uriString"
                            Invoke-Expression $updateCmd 2>&1 | Out-Null
                            
                            if ($LASTEXITCODE -eq 0) {
                                Write-Success "Azure AD redirect URIs configured"
                            } else {
                                Write-Warning "Failed to update redirect URIs - you may need to add them manually in Azure Portal"
                            }
                        } else {
                            Write-Success "Azure AD redirect URIs already configured"
                        }
                    } else {
                        Write-Warning "Could not retrieve app registration - verify ClientId in secrets.yaml"
                    }
                } else {
                    Write-Warning "Invalid or placeholder Azure AD Client ID - skipping redirect URI configuration"
                    Write-Host "  Update secrets.yaml with real Azure AD app registration details" -ForegroundColor Yellow
                }
            }
        }
    } else {
        Write-Warning "Ingress IP not yet assigned (this may take 2-3 minutes)"
        Write-Host "  Run: kubectl get ingress -n $Namespace" -ForegroundColor Yellow
        Write-Host "  Then manually add redirect URIs to Azure AD app registration" -ForegroundColor Yellow
    }
}

# Summary
Write-Host @"

╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║            Deployment Complete! ✓                            ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Green

Write-Host "Microservices Deployed:" -ForegroundColor Cyan
Write-Host "  ✓ Products Service  (Port 8081)" -ForegroundColor Green
Write-Host "  ✓ Cart Service      (Port 8082)" -ForegroundColor Green
Write-Host "  ✓ Orders Service    (Port 8083)" -ForegroundColor Green
Write-Host "  ✓ Checkout Service  (Port 8084)" -ForegroundColor Green
Write-Host "  ✓ Frontend Service  (Port 8080)" -ForegroundColor Green
Write-Host ""

Write-Host "Access your application:" -ForegroundColor Cyan
if ($ingressIp) {
    Write-Host "  Frontend: http://$ingressIp" -ForegroundColor Yellow
} else {
    Write-Host "  Get IP: kubectl get ingress -n $Namespace" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "Useful Commands:" -ForegroundColor Cyan
Write-Host "  View pods:          kubectl get pods -n $Namespace" -ForegroundColor Yellow
Write-Host "  View services:      kubectl get services -n $Namespace" -ForegroundColor Yellow
Write-Host "  View ingress:       kubectl get ingress -n $Namespace" -ForegroundColor Yellow
Write-Host "  View logs:          kubectl logs -l tier=frontend -n $Namespace --tail=50" -ForegroundColor Yellow
Write-Host "  Describe pod:       kubectl describe pod <pod-name> -n $Namespace" -ForegroundColor Yellow
Write-Host "  Restart service:    kubectl rollout restart deployment/<service-name> -n $Namespace" -ForegroundColor Yellow
Write-Host ""
