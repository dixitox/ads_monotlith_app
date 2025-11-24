# setup-azure-infrastructure-monolith.ps1
# Script to create Azure infrastructure for RetailMonolith

<#
.SYNOPSIS
    Creates Azure infrastructure for RetailMonolith deployment

.DESCRIPTION
    This script creates:
    - Resource Group
    - Azure Container Registry (ACR)
    - Azure Kubernetes Service (AKS)
    - Azure SQL Server
    - Azure SQL Database (RetailMonolithDB)
    - Firewall rules for SQL Server

.PARAMETER ResourceGroup
    Name of the Azure Resource Group (default: rg-retail-monolith)

.PARAMETER Location
    Azure region for resources (default: uksouth)

.PARAMETER AcrName
    Name of the Azure Container Registry (default: acrretailmonolith)

.PARAMETER AksName
    Name of the AKS cluster (default: aks-retail-monolith)

.PARAMETER SqlServerName
    Name of the SQL Server (default: sql-retail-monolith)

.EXAMPLE
    .\setup-azure-infrastructure-monolith.ps1

.EXAMPLE
    .\setup-azure-infrastructure-monolith.ps1 -ResourceGroup "rg-my-retail" -Location "ukwest"

.NOTES
    This script creates SQL Server with Azure AD-only authentication.
    No SQL username/password is needed - authentication uses Azure AD.
#>

param(
    [string]$ResourceGroup = "rg-retail-monolith",
    [string]$Location = "uksouth",
    [string]$AcrName = "acrretailmonolith",
    [string]$AksName = "aks-retail-monolith",
    [string]$SqlServerName = "sql-retail-monolith"
)

# Color functions for output
function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

function Write-Step {
    param([string]$Message)
    Write-ColorOutput "`n▶ $Message" "Cyan"
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "  ✓ $Message" "Green"
}

function Write-Error {
    param([string]$Message)
    Write-ColorOutput "  ✗ $Message" "Red"
}

function Write-Warning {
    param([string]$Message)
    Write-ColorOutput "  ⚠ $Message" "Yellow"
}

# Banner
Write-ColorOutput @"
╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║     Azure Infrastructure Setup - RetailMonolith              ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝
"@ "Cyan"

# Check if Azure CLI is installed
Write-Step "Checking prerequisites..."
try {
    $azVersion = az version --output json 2>&1 | ConvertFrom-Json
    Write-Success "Azure CLI version $($azVersion.'azure-cli') installed"
} catch {
    Write-Error "Azure CLI is not installed. Please install from: https://aka.ms/installazurecli"
    exit 1
}

# Check if logged in to Azure
Write-Step "Checking Azure login status..."
$account = az account show 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Not logged in to Azure. Initiating login..."
    az login
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Azure login failed"
        exit 1
    }
}

$accountInfo = az account show | ConvertFrom-Json
Write-Success "Logged in as: $($accountInfo.user.name)"
Write-Success "Subscription: $($accountInfo.name) ($($accountInfo.id))"

Write-ColorOutput "`n✓ Using Azure AD authentication (no SQL passwords needed)" "Green"

# Display configuration
Write-Step "Configuration:"
Write-Host "  Resource Group:  $ResourceGroup"
Write-Host "  Location:        $Location"
Write-Host "  ACR Name:        $AcrName"
Write-Host "  AKS Name:        $AksName"
Write-Host "  SQL Server:      $SqlServerName"
Write-Host "  SQL Auth:        Azure AD only (Managed Identity)"
Write-Host ""

$confirmation = Read-Host "Proceed with infrastructure creation? (yes/no)"
if ($confirmation -ne "yes") {
    Write-Warning "Operation cancelled by user"
    exit 0
}

# Step 1: Create Resource Group
Write-Step "Creating Resource Group..."
$rg = az group create --name $ResourceGroup --location $Location 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Success "Resource Group '$ResourceGroup' created"
} else {
    Write-Warning "Resource Group may already exist"
}

# Step 2: Create Azure Container Registry
Write-Step "Creating Azure Container Registry..."
$acr = az acr create `
    --resource-group $ResourceGroup `
    --name $AcrName `
    --sku Basic `
    --admin-enabled true 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Success "ACR '$AcrName' created"
    
    # Get ACR credentials
    $acrCreds = az acr credential show --name $AcrName --resource-group $ResourceGroup | ConvertFrom-Json
    Write-Success "ACR Login Server: $AcrName.azurecr.io"
    Write-Success "ACR Username: $($acrCreds.username)"
    Write-Host "  ACR Password: $($acrCreds.passwords[0].value)" -ForegroundColor Yellow
} else {
    Write-Warning "ACR may already exist"
}

# Step 3: Create AKS Cluster
Write-Step "Creating AKS Cluster (this may take 5-10 minutes)..."
$aks = az aks create `
    --resource-group $ResourceGroup `
    --name $AksName `
    --node-count 2 `
    --node-vm-size Standard_B2s `
    --enable-managed-identity `
    --attach-acr $AcrName `
    --generate-ssh-keys `
    --network-plugin azure `
    --network-policy azure 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Success "AKS cluster '$AksName' created"
} else {
    Write-Error "Failed to create AKS cluster"
    Write-Error $aks
    exit 1
}

# Get AKS credentials
Write-Step "Configuring kubectl..."
az aks get-credentials --resource-group $ResourceGroup --name $AksName --overwrite-existing
if ($LASTEXITCODE -eq 0) {
    Write-Success "kubectl configured for cluster '$AksName'"
    
    # Verify kubectl connection
    $nodes = kubectl get nodes
    Write-Success "Cluster nodes:"
    Write-Host $nodes
} else {
    Write-Error "Failed to configure kubectl"
}

# Step 4: Create Azure SQL Server with Azure AD Admin only
Write-Step "Creating Azure SQL Server with Azure AD authentication..."

# Get current user info for Azure AD admin
$currentUser = az ad signed-in-user show | ConvertFrom-Json
$adminLogin = $currentUser.userPrincipalName
$adminSid = $currentUser.id

Write-Host "  Setting Azure AD admin: $adminLogin" -ForegroundColor Yellow

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
    Write-Warning "SQL Server may already exist"
}

# Step 5: Configure SQL Server Firewall
Write-Step "Configuring SQL Server firewall rules..."

# Allow Azure services
az sql server firewall-rule create `
    --resource-group $ResourceGroup `
    --server $SqlServerName `
    --name AllowAzureServices `
    --start-ip-address 0.0.0.0 `
    --end-ip-address 0.0.0.0 2>&1 | Out-Null

Write-Success "Azure services firewall rule created"

# Get current public IP and add it
$myIp = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing).Content
az sql server firewall-rule create `
    --resource-group $ResourceGroup `
    --server $SqlServerName `
    --name AllowClientIP `
    --start-ip-address $myIp `
    --end-ip-address $myIp 2>&1 | Out-Null

Write-Success "Client IP ($myIp) firewall rule created"

# Step 6: Create SQL Database
Write-Step "Creating SQL Database (RetailMonolithDB)..."
$db = az sql db create `
    --resource-group $ResourceGroup `
    --server $SqlServerName `
    --name RetailMonolithDB `
    --service-objective S1 `
    --backup-storage-redundancy Local 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Success "Database 'RetailMonolithDB' created"
} else {
    Write-Warning "Database may already exist"
}

# Summary
Write-ColorOutput @"

╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║              Infrastructure Creation Complete! ✓              ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝

"@ "Green"

Write-ColorOutput "Resource Summary:" "Cyan"
Write-Host "  Resource Group:     $ResourceGroup"
Write-Host "  Location:           $Location"
Write-Host "  ACR:                $AcrName.azurecr.io"
Write-Host "  AKS Cluster:        $AksName"
Write-Host "  SQL Server:         $SqlServerName.database.windows.net"
Write-Host "  Database:           RetailMonolithDB"
Write-Host ""

Write-ColorOutput "Connection Strings:" "Cyan"
Write-Host "  SQL Connection (Azure AD / Managed Identity):"
Write-Host "    Server=tcp:$SqlServerName.database.windows.net,1433;Database=RetailMonolithDB;Authentication=Active Directory Default;Encrypt=True;" -ForegroundColor Yellow
Write-Host ""

Write-ColorOutput "Azure AD Admin configured: $adminLogin" "Green"
Write-Host ""

Write-ColorOutput "Next Steps:" "Cyan"
Write-Host "  1. Configure Azure AD authentication:"
Write-Host "     .\configure-azure-ad-auth.ps1" -ForegroundColor Yellow
Write-Host ""
Write-Host "  2. Grant SQL permissions (follow prompts from step 1)"
Write-Host ""
Write-Host "  3. Create secrets file:"
Write-Host "     Copy-Item k8s/monolith/secrets-template.yaml k8s/monolith/secrets.yaml"
Write-Host "     # Edit secrets.yaml with connection string" -ForegroundColor Yellow
Write-Host ""
Write-Host "  4. Build and push Docker image:"
Write-Host "     .\build-and-push-monolith.ps1 -AcrName $AcrName" -ForegroundColor Yellow
Write-Host ""
Write-Host "  5. Deploy to AKS:"
Write-Host "     .\deploy-monolith.ps1" -ForegroundColor Yellow
Write-Host ""

Write-ColorOutput "For detailed instructions, see: MONOLITH_DEPLOYMENT_GUIDE.md" "Cyan"
Write-Host ""
