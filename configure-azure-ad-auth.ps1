# configure-azure-ad-auth.ps1
# Configure Azure AD authentication for RetailMonolith SQL Database access

<#
.SYNOPSIS
    Configures Azure AD authentication and Managed Identity for SQL Database access

.DESCRIPTION
    This script:
    - Creates a User-Assigned Managed Identity
    - Enables Workload Identity on AKS
    - Creates federated identity credential
    - Grants SQL access to the Managed Identity
    - Updates Kubernetes service account

.PARAMETER ResourceGroup
    Resource Group name

.PARAMETER AksName
    AKS cluster name

.PARAMETER SqlServerName
    SQL Server name

.PARAMETER Namespace
    Kubernetes namespace

.EXAMPLE
    .\configure-azure-ad-auth.ps1 -ResourceGroup "rg-retail-monolith" -AksName "aks-retail-monolith" -SqlServerName "sql-retail-monolith"
#>

param(
    [string]$ResourceGroup = "rg-retail-monolith",
    [string]$AksName = "aks-retail-monolith",
    [string]$SqlServerName = "sql-retail-monolith",
    [string]$Namespace = "retail-monolith",
    [string]$ServiceAccountName = "retail-monolith-sa",
    [string]$ManagedIdentityName = "mi-retail-monolith"
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "`n▶ $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "  ✓ $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "  ⚠ $Message" -ForegroundColor Yellow
}

Write-Host @"
╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║     Configure Azure AD Authentication for SQL Database       ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# Get AKS resource group and subscription
Write-Step "Getting AKS cluster information..."
$aksInfo = az aks show --resource-group $ResourceGroup --name $AksName | ConvertFrom-Json
$location = $aksInfo.location
$subscriptionId = az account show --query id -o tsv
Write-Success "AKS cluster: $AksName in $location"

# Enable Workload Identity and OIDC issuer on AKS
Write-Step "Enabling Workload Identity on AKS..."
az aks update `
    --resource-group $ResourceGroup `
    --name $AksName `
    --enable-workload-identity `
    --enable-oidc-issuer 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Success "Workload Identity enabled"
} else {
    Write-Warning "Workload Identity may already be enabled"
}

# Get OIDC issuer URL
$oidcIssuer = az aks show --resource-group $ResourceGroup --name $AksName --query "oidcIssuerProfile.issuerUrl" -o tsv
Write-Success "OIDC Issuer: $oidcIssuer"

# Create User-Assigned Managed Identity
Write-Step "Creating Managed Identity..."
$managedIdentity = az identity create `
    --resource-group $ResourceGroup `
    --name $ManagedIdentityName `
    --location $location | ConvertFrom-Json

$managedIdentityClientId = $managedIdentity.clientId
$managedIdentityPrincipalId = $managedIdentity.principalId
Write-Success "Managed Identity created: $ManagedIdentityName"
Write-Success "Client ID: $managedIdentityClientId"
Write-Success "Principal ID: $managedIdentityPrincipalId"

# Create federated identity credential
Write-Step "Creating federated identity credential..."
az identity federated-credential create `
    --resource-group $ResourceGroup `
    --identity-name $ManagedIdentityName `
    --name "${AksName}-${Namespace}-${ServiceAccountName}" `
    --issuer $oidcIssuer `
    --subject "system:serviceaccount:${Namespace}:${ServiceAccountName}" `
    --audiences "api://AzureADTokenExchange" 2>&1 | Out-Null

Write-Success "Federated identity credential created"

# Get tenant ID
$tenantId = az account show --query tenantId -o tsv

# Update service account YAML
Write-Step "Updating Kubernetes service account..."
$serviceAccountFile = "k8s/monolith/serviceaccount.yaml"
$serviceAccountContent = Get-Content $serviceAccountFile -Raw
$serviceAccountContent = $serviceAccountContent -replace 'YOUR_MANAGED_IDENTITY_CLIENT_ID', $managedIdentityClientId
$serviceAccountContent = $serviceAccountContent -replace 'YOUR_TENANT_ID', $tenantId
$serviceAccountContent | Set-Content $serviceAccountFile
Write-Success "Service account updated"

# Grant SQL Database access to Managed Identity
Write-Step "Granting SQL Database access to Managed Identity..."
Write-Host "  This requires running SQL commands against the database..." -ForegroundColor Yellow

$sqlCommands = @"
-- Create user for Managed Identity
CREATE USER [$ManagedIdentityName] FROM EXTERNAL PROVIDER;

-- Grant db_datareader and db_datawriter roles
ALTER ROLE db_datareader ADD MEMBER [$ManagedIdentityName];
ALTER ROLE db_datawriter ADD MEMBER [$ManagedIdentityName];
ALTER ROLE db_ddladmin ADD MEMBER [$ManagedIdentityName];

-- Additional permissions for Entity Framework migrations
GRANT CREATE TABLE TO [$ManagedIdentityName];
GRANT ALTER ON SCHEMA::dbo TO [$ManagedIdentityName];
"@

# Save SQL commands to file
$sqlFile = "temp-grant-sql-access.sql"
$sqlCommands | Set-Content $sqlFile

Write-Host ""
Write-Host "  SQL commands saved to: $sqlFile" -ForegroundColor Yellow
Write-Host ""
Write-Host "  To grant SQL access, run ONE of the following:" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Option 1 - Using Azure CLI (recommended):" -ForegroundColor Yellow
Write-Host "    az login" -ForegroundColor White
Write-Host "    sqlcmd -S $SqlServerName.database.windows.net -d RetailMonolithDB -G -i $sqlFile" -ForegroundColor White
Write-Host ""
Write-Host "  Option 2 - Using Azure Portal:" -ForegroundColor Yellow
Write-Host "    1. Go to Azure Portal -> SQL Database -> RetailMonolithDB" -ForegroundColor White
Write-Host "    2. Click 'Query editor'" -ForegroundColor White
Write-Host "    3. Login with Azure AD" -ForegroundColor White
Write-Host "    4. Paste and run the SQL commands from $sqlFile" -ForegroundColor White
Write-Host ""
Write-Host "  Option 3 - Using SQL Server Management Studio (SSMS):" -ForegroundColor Yellow
Write-Host "    1. Connect to: $SqlServerName.database.windows.net" -ForegroundColor White
Write-Host "    2. Authentication: Azure Active Directory - Universal with MFA" -ForegroundColor White
Write-Host "    3. Open and execute: $sqlFile" -ForegroundColor White
Write-Host ""

$executeNow = Read-Host "Would you like to execute the SQL commands now using sqlcmd? (yes/no)"

if ($executeNow -eq "yes") {
    Write-Step "Executing SQL commands..."
    
    # Check if sqlcmd is available
    $sqlcmdExists = Get-Command sqlcmd -ErrorAction SilentlyContinue
    
    if (-not $sqlcmdExists) {
        Write-Warning "sqlcmd not found. Please install SQL Server command-line tools:"
        Write-Host "  https://learn.microsoft.com/en-us/sql/tools/sqlcmd/sqlcmd-utility" -ForegroundColor Yellow
    } else {
        sqlcmd -S "$SqlServerName.database.windows.net" -d "RetailMonolithDB" -G -i $sqlFile
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "SQL permissions granted successfully"
        } else {
            Write-Warning "Failed to execute SQL commands. Please run manually."
        }
    }
} else {
    Write-Warning "Please run the SQL commands manually before deploying the application"
}

# Update ConfigMap with SQL Server name
Write-Step "Updating ConfigMap with SQL Server name..."
$configMapFile = "k8s/monolith/configmap.yaml"
$configMapContent = Get-Content $configMapFile -Raw
$configMapContent = $configMapContent -replace 'YOUR_SQL_SERVER_NAME', $SqlServerName
$configMapContent | Set-Content $configMapFile
Write-Success "ConfigMap updated"

# Update secrets template with connection string
Write-Step "Preparing connection string for secrets..."
$connectionString = "Server=tcp:$SqlServerName.database.windows.net,1433;Database=RetailMonolithDB;Authentication=Active Directory Default;Encrypt=True;"
$connectionStringBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($connectionString))

Write-Host ""
Write-Host "  Connection String (base64 encoded):" -ForegroundColor Yellow
Write-Host "  $connectionStringBase64" -ForegroundColor White
Write-Host ""
Write-Host "  Add this to k8s/monolith/secrets.yaml:" -ForegroundColor Cyan
Write-Host "    ConnectionStrings__DefaultConnection: $connectionStringBase64" -ForegroundColor White
Write-Host ""

# Summary
Write-Host @"

╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║        Azure AD Authentication Configuration Complete!        ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Green

Write-Host "Configuration Summary:" -ForegroundColor Cyan
Write-Host "  Managed Identity:     $ManagedIdentityName" -ForegroundColor White
Write-Host "  Client ID:            $managedIdentityClientId" -ForegroundColor White
Write-Host "  Principal ID:         $managedIdentityPrincipalId" -ForegroundColor White
Write-Host "  Service Account:      $ServiceAccountName" -ForegroundColor White
Write-Host "  Namespace:            $Namespace" -ForegroundColor White
Write-Host ""

Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Ensure SQL permissions are granted (see above)" -ForegroundColor White
Write-Host "  2. Create secrets.yaml with the connection string" -ForegroundColor White
Write-Host "  3. Deploy service account: kubectl apply -f k8s/monolith/serviceaccount.yaml" -ForegroundColor White
Write-Host "  4. Deploy application: .\deploy-monolith.ps1" -ForegroundColor White
Write-Host ""

Write-Host "Files Updated:" -ForegroundColor Cyan
Write-Host "  ✓ k8s/monolith/serviceaccount.yaml" -ForegroundColor Green
Write-Host "  ✓ k8s/monolith/configmap.yaml" -ForegroundColor Green
Write-Host ""

if (Test-Path $sqlFile) {
    Write-Host "Temporary SQL file: $sqlFile (can be deleted after running SQL commands)" -ForegroundColor Yellow
}
