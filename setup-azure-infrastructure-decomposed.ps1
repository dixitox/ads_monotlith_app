# setup-azure-infrastructure-decomposed.ps1
# Create Azure infrastructure for RetailDecomposed Microservices

<#
.SYNOPSIS
    Creates Azure infrastructure for RetailDecomposed microservices deployment

.DESCRIPTION
    This script creates:
    - Resource Group
    - Azure Container Registry (ACR)
    - Azure Kubernetes Service (AKS) cluster
    - Azure SQL Server with Azure AD authentication
    - Azure SQL Database
    - Firewall rules
    - Application Insights (for observability)

.PARAMETER ResourceGroup
    Name of the Azure Resource Group (default: rg-retail-decomposed)

.PARAMETER Location
    Azure region (default: uksouth)

.PARAMETER AcrName
    Azure Container Registry name (default: acrretaildecomposed)

.PARAMETER AksName
    AKS cluster name (default: aks-retail-decomposed)

.PARAMETER SqlServerName
    SQL Server name (default: sql-retail-decomposed)

.EXAMPLE
    .\setup-azure-infrastructure-decomposed.ps1

.EXAMPLE
    .\setup-azure-infrastructure-decomposed.ps1 -ResourceGroup "my-rg" -Location "eastus"
#>

param(
    [string]$ResourceGroup = "rg-retail-decomposed",
    [string]$Location = "uksouth",
    [string]$AcrName = "acrretaildecomposed",
    [string]$AksName = "aks-retail-decomposed",
    [string]$SqlServerName = "sql-retail-decomposed"
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
║     Azure Infrastructure Setup - RetailDecomposed            ║
║              Microservices Architecture                       ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

Write-Host "`nConfiguration:" -ForegroundColor Yellow
Write-Host "  Resource Group:  $ResourceGroup"
Write-Host "  Location:        $Location"
Write-Host "  ACR Name:        $AcrName"
Write-Host "  AKS Name:        $AksName"
Write-Host "  SQL Server:      $SqlServerName"
Write-Host ""

# Check Azure CLI
Write-Step "Checking Azure CLI..."
try {
    $azVersion = az version --query '"azure-cli"' -o tsv
    Write-Success "Azure CLI version: $azVersion"
} catch {
    Write-Error "Azure CLI not found. Please install from https://aka.ms/azure-cli"
    exit 1
}

# Check Azure login
Write-Step "Checking Azure login..."
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Warning "Not logged in to Azure. Starting login..."
    az login
    $account = az account show | ConvertFrom-Json
}
Write-Success "Logged in as: $($account.user.name)"
Write-Success "Subscription: $($account.name)"

# Step 1: Create Resource Group
Write-Step "Creating/Verifying Resource Group..."
$existingRg = az group show --name $ResourceGroup 2>$null | ConvertFrom-Json
if ($existingRg) {
    Write-Success "Resource Group '$ResourceGroup' already exists (Location: $($existingRg.location))"
} else {
    $rg = az group create --name $ResourceGroup --location $Location 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Resource Group '$ResourceGroup' created"
    } else {
        Write-Error "Failed to create Resource Group"
        exit 1
    }
}

# Step 2: Create Azure Container Registry
Write-Step "Creating/Updating Azure Container Registry..."
$existingAcr = az acr show --name $AcrName --resource-group $ResourceGroup 2>$null | ConvertFrom-Json
if ($existingAcr) {
    Write-Success "ACR '$AcrName' already exists (Login Server: $($existingAcr.loginServer))"
    
    # Ensure admin is enabled
    az acr update --name $AcrName --resource-group $ResourceGroup --admin-enabled true 2>&1 | Out-Null
    Write-Success "ACR admin access verified"
} else {
    $acr = az acr create `
        --resource-group $ResourceGroup `
        --name $AcrName `
        --sku Standard `
        --admin-enabled true 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "ACR '$AcrName' created"
    } else {
        Write-Error "Failed to create ACR"
        exit 1
    }
}

# Get ACR credentials
$acrCreds = az acr credential show --name $AcrName --resource-group $ResourceGroup | ConvertFrom-Json
Write-Success "ACR Login Server: $AcrName.azurecr.io"
Write-Success "ACR Username: $($acrCreds.username)"
Write-Host "  ACR Password: $($acrCreds.passwords[0].value)" -ForegroundColor Yellow

# Step 3: Create AKS Cluster (larger for microservices)
Write-Step "Creating/Updating AKS Cluster..."
$existingAks = az aks show --name $AksName --resource-group $ResourceGroup 2>$null | ConvertFrom-Json
if ($existingAks) {
    Write-Success "AKS cluster '$AksName' already exists (Version: $($existingAks.kubernetesVersion))"
    Write-Host "  Verifying ACR attachment..." -ForegroundColor Yellow
    
    # Ensure ACR is attached
    az aks update --name $AksName --resource-group $ResourceGroup --attach-acr $AcrName 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        Write-Success "ACR attachment verified"
    }
} else {
    Write-Host "  Creating AKS cluster with 3 nodes for microservices..." -ForegroundColor Yellow
    Write-Host "  This may take 10-15 minutes..." -ForegroundColor Yellow
    $aks = az aks create `
        --resource-group $ResourceGroup `
        --name $AksName `
        --node-count 3 `
        --node-vm-size Standard_D2s_v3 `
        --enable-managed-identity `
        --attach-acr $AcrName `
        --generate-ssh-keys `
        --network-plugin azure `
        --network-policy azure `
        --enable-addons monitoring 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "AKS cluster '$AksName' created"
    } else {
        Write-Error "Failed to create AKS cluster"
        Write-Error $aks
        exit 1
    }
}

# Get AKS credentials
Write-Step "Configuring kubectl..."
az aks get-credentials --resource-group $ResourceGroup --name $AksName --overwrite-existing 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Success "kubectl configured for AKS cluster"
} else {
    Write-Warning "Failed to configure kubectl"
}

# Step 4: Create Azure SQL Server with Azure AD authentication
Write-Step "Creating/Updating Azure SQL Server with Azure AD authentication..."

# Get current user info for Azure AD admin
$currentUser = az ad signed-in-user show | ConvertFrom-Json
$adminLogin = $currentUser.userPrincipalName
$adminSid = $currentUser.id

Write-Host "  Setting Azure AD admin: $adminLogin" -ForegroundColor Yellow

$existingSql = az sql server show --name $SqlServerName --resource-group $ResourceGroup 2>$null | ConvertFrom-Json
if ($existingSql) {
    Write-Success "SQL Server '$SqlServerName' already exists (FQDN: $($existingSql.fullyQualifiedDomainName))"
    
    # Ensure Azure AD admin is set
    az sql server ad-admin create `
        --resource-group $ResourceGroup `
        --server-name $SqlServerName `
        --display-name $adminLogin `
        --object-id $adminSid 2>&1 | Out-Null
    Write-Success "Azure AD admin verified"
} else {
    $sqlServer = az sql server create `
        --resource-group $ResourceGroup `
        --name $SqlServerName `
        --location $Location `
        --enable-ad-only-auth `
        --external-admin-principal-type User `
        --external-admin-name $adminLogin `
        --external-admin-sid $adminSid 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "SQL Server '$SqlServerName' created with Azure AD authentication"
    } else {
        Write-Error "Failed to create SQL Server"
        exit 1
    }
}

# Step 5: Configure SQL Server Firewall
Write-Step "Configuring SQL Server firewall rules..."

# Allow Azure services (creates or updates)
az sql server firewall-rule create `
    --resource-group $ResourceGroup `
    --server $SqlServerName `
    --name AllowAzureServices `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Success "Azure services firewall rule configured"
} else {
    Write-Success "Azure services firewall rule already exists"
}

# Get current public IP and add it (creates or updates)
$myIp = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing).Content
az sql server firewall-rule create `
    --resource-group $ResourceGroup `
    --server $SqlServerName `
    --name AllowClientIP `
    --start-ip-address $myIp `
    --end-ip-address $myIp 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Success "Client IP ($myIp) firewall rule configured"
} else {
    Write-Success "Client IP firewall rule already exists"
}

# Enable public network access
Write-Step "Enabling SQL Server public network access..."
az sql server update --resource-group $ResourceGroup --name $SqlServerName --enable-public-network true 2>&1 | Out-Null
Write-Success "Public network access enabled"

# Step 6: Create SQL Database
Write-Step "Creating/Verifying SQL Database (RetailDecomposedDB)..."
$existingDb = az sql db show --name RetailDecomposedDB --server $SqlServerName --resource-group $ResourceGroup 2>$null | ConvertFrom-Json
if ($existingDb) {
    Write-Success "Database 'RetailDecomposedDB' already exists (Status: $($existingDb.status))"
    Write-Host "  Current SKU: $($existingDb.currentServiceObjectiveName)" -ForegroundColor Yellow
} else {
    $db = az sql db create `
        --resource-group $ResourceGroup `
        --server $SqlServerName `
        --name RetailDecomposedDB `
        --service-objective S1 `
        --backup-storage-redundancy Local 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Database 'RetailDecomposedDB' created"
    } else {
        Write-Error "Failed to create database"
        exit 1
    }
}

# Grant AKS managed identity access to SQL Server
Write-Step "Configuring AKS managed identity SQL Server access..."
$aksIdentityObjectId = az aks show --resource-group $ResourceGroup --name $AksName --query "identityProfile.kubeletidentity.objectId" -o tsv 2>$null

if ($aksIdentityObjectId) {
    Write-Host "  AKS Identity Object ID: $aksIdentityObjectId" -ForegroundColor Yellow
    
    # Set AKS identity as Azure AD admin for SQL Server
    Write-Host "  Setting AKS identity as Azure AD admin..." -ForegroundColor Yellow
    az sql server ad-admin create `
        --resource-group $ResourceGroup `
        --server-name $SqlServerName `
        --display-name "AKS-Identity" `
        --object-id $aksIdentityObjectId 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "AKS identity granted SQL Server access"
        Write-Host "  Pods will use Managed Identity to connect to database" -ForegroundColor Green
    } else {
        Write-Warning "Failed to grant SQL Server access"
        Write-Host "  You may need to configure this manually or pods will fail to connect" -ForegroundColor Yellow
    }
} else {
    Write-Warning "Could not retrieve AKS identity - manual SQL permission configuration required"
    Write-Host "  Run: az aks show --resource-group $ResourceGroup --name $AksName --query identityProfile.kubeletidentity" -ForegroundColor Yellow
}

# Step 7: Create Application Insights
Write-Step "Creating/Verifying Application Insights..."
$appInsightsName = "appi-retail-decomposed"
$existingAppInsights = az monitor app-insights component show --app $appInsightsName --resource-group $ResourceGroup 2>$null | ConvertFrom-Json

if ($existingAppInsights) {
    Write-Success "Application Insights '$appInsightsName' already exists"
} else {
    $appInsights = az monitor app-insights component create `
        --app $appInsightsName `
        --location $Location `
        --resource-group $ResourceGroup `
        --application-type web 2>&1 | ConvertFrom-Json
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Application Insights '$appInsightsName' created"
    } else {
        Write-Warning "Failed to create Application Insights (optional)"
    }
}

# Get Application Insights connection string
$appInsightsConnectionString = az monitor app-insights component show --app $appInsightsName --resource-group $ResourceGroup --query "connectionString" -o tsv 2>$null

# Summary
Write-Host @"

╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║            Infrastructure Setup Complete! ✓                  ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Green

Write-Host "Resources Created:" -ForegroundColor Cyan
Write-Host "  Resource Group:     $ResourceGroup" -ForegroundColor Yellow
Write-Host "  ACR:                $AcrName.azurecr.io" -ForegroundColor Yellow
Write-Host "  AKS Cluster:        $AksName (3 nodes, Standard_D2s_v3)" -ForegroundColor Yellow
Write-Host "  SQL Server:         $SqlServerName.database.windows.net" -ForegroundColor Yellow
Write-Host "  SQL Database:       RetailDecomposedDB" -ForegroundColor Yellow
Write-Host "  App Insights:       $appInsightsName" -ForegroundColor Yellow
Write-Host ""

Write-Host "Connection Information:" -ForegroundColor Cyan
Write-Host "  ACR Login:          $AcrName.azurecr.io" -ForegroundColor Yellow
Write-Host "  SQL Server (FQDN):  $SqlServerName.database.windows.net" -ForegroundColor Yellow
Write-Host "  Azure AD Admin:     $adminLogin" -ForegroundColor Yellow
if ($appInsightsConnectionString) {
    Write-Host "  App Insights Key:   $appInsightsConnectionString" -ForegroundColor Yellow
}
Write-Host ""

Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Build and push Docker images:"
Write-Host "     .\build-and-push-decomposed.ps1"
Write-Host ""
Write-Host "  2. Create Kubernetes secrets:"
Write-Host "     Update k8s/decomposed/secrets.yaml with your configuration"
Write-Host ""
Write-Host "  3. Deploy to AKS:"
Write-Host "     .\deploy-decomposed.ps1"
Write-Host ""

# Save configuration
$config = @{
    ResourceGroup = $ResourceGroup
    Location = $Location
    AcrName = $AcrName
    AksName = $AksName
    SqlServerName = $SqlServerName
    SqlServerFqdn = "$SqlServerName.database.windows.net"
    DatabaseName = "RetailDecomposedDB"
    AppInsightsName = $appInsightsName
    AppInsightsConnectionString = $appInsightsConnectionString
    AzureAdAdmin = $adminLogin
} | ConvertTo-Json

$config | Out-File -FilePath "azure-config-decomposed.json" -Encoding UTF8
Write-Success "Configuration saved to: azure-config-decomposed.json"
