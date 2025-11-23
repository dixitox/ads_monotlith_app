# Deployment Guide - RetailDecomposed

This guide covers deploying the RetailDecomposed application to Azure App Service with production-ready configuration.

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Azure Resources Setup](#azure-resources-setup)
4. [RBAC Configuration](#rbac-configuration)
5. [Application Deployment](#application-deployment)
6. [Post-Deployment Verification](#post-deployment-verification)
7. [Troubleshooting](#troubleshooting)

## Overview

The RetailDecomposed application requires the following Azure resources:

- **Azure App Service**: Host the web application
- **Azure SQL Database**: Store application data
- **Azure AI Foundry / OpenAI**: AI Copilot and embeddings
- **Azure AI Search**: Semantic search functionality
- **Azure Entra ID**: User authentication and RBAC

**Related Documentation**:
- **[SEMANTIC_SEARCH_GUIDE.md](SEMANTIC_SEARCH_GUIDE.md)** - Detailed RBAC setup for semantic search
- **[AUTHENTICATION_SETUP.md](AUTHENTICATION_SETUP.md)** - User authentication configuration

**Key Features**:
- ✅ Zero secrets in code (uses Managed Identity)
- ✅ RBAC-based authentication to all Azure services
- ✅ Production-ready security configuration
- ✅ HTTPS enforced

## Prerequisites

- Azure Subscription with Owner or Contributor + User Access Administrator roles
- Azure CLI installed (`az --version` >= 2.50.0)
- .NET 9.0 SDK
- PowerShell 7+ or Bash

## Azure Resources Setup

### Step 1: Create Resource Group

```bash
# Set variables
$location = "eastus"
$resourceGroup = "rg-retail-app"
$appName = "retail-decomposed-app"
$sqlServerName = "retail-sql-server-unique123"  # Must be globally unique
$sqlDbName = "RetailDecomposed"
$searchServiceName = "retail-search-unique123"  # Must be globally unique

# Create resource group
az group create `
  --name $resourceGroup `
  --location $location
```

### Step 2: Create Azure SQL Database

```bash
# Create SQL Server
az sql server create `
  --name $sqlServerName `
  --resource-group $resourceGroup `
  --location $location `
  --admin-user sqladmin `
  --admin-password "YourSecurePassword123!" `
  --enable-ad-only-auth false

# Create SQL Database
az sql db create `
  --name $sqlDbName `
  --server $sqlServerName `
  --resource-group $resourceGroup `
  --service-objective S0 `
  --backup-storage-redundancy Local

# Allow Azure services to access SQL Server
az sql server firewall-rule create `
  --name AllowAzureServices `
  --server $sqlServerName `
  --resource-group $resourceGroup `
  --start-ip-address 0.0.0.0 `
  --end-ip-address 0.0.0.0
```

**Security Note**: In production, consider using Azure AD authentication only and removing SQL authentication.

### Step 3: Create Azure AI Search Service

```bash
# Create search service (Free tier)
az search service create `
  --name $searchServiceName `
  --resource-group $resourceGroup `
  --location $location `
  --sku free `
  --partition-count 1 `
  --replica-count 1

# CRITICAL: Enable RBAC authentication
az search service update `
  --name $searchServiceName `
  --resource-group $resourceGroup `
  --auth-options aadOrApiKey `
  --aad-auth-failure-mode http401WithBearerChallenge
```

### Step 4: Create Azure AI Foundry Resource

**Option A: Using Azure Portal** (Recommended for Azure AI Foundry)

1. Navigate to [Azure AI Foundry](https://ai.azure.com)
2. Create new project or use existing
3. Deploy models:
   - `gpt-4o` (chat)
   - `text-embedding-3-small` (embeddings)
4. Note the project endpoint (e.g., `https://your-project.services.ai.azure.com/`)

**Option B: Using Azure OpenAI** (Alternative)

```bash
# Create Azure OpenAI resource
az cognitiveservices account create `
  --name retail-openai-service `
  --resource-group $resourceGroup `
  --location $location `
  --kind OpenAI `
  --sku S0

# Deploy models (requires additional Azure OpenAI Studio steps)
```

### Step 5: Create App Service

```bash
# Create App Service Plan
az appservice plan create `
  --name "${appName}-plan" `
  --resource-group $resourceGroup `
  --location $location `
  --sku B1 `
  --is-linux

# Create App Service
az webapp create `
  --name $appName `
  --resource-group $resourceGroup `
  --plan "${appName}-plan" `
  --runtime "DOTNET|9.0"

# Enable HTTPS only
az webapp update `
  --name $appName `
  --resource-group $resourceGroup `
  --https-only true
```

### Step 6: Enable Managed Identity

```bash
# Enable System-Assigned Managed Identity
az webapp identity assign `
  --name $appName `
  --resource-group $resourceGroup

# Get the managed identity principal ID
$managedIdentityId = az webapp identity show `
  --name $appName `
  --resource-group $resourceGroup `
  --query principalId -o tsv

echo "Managed Identity Principal ID: $managedIdentityId"
```

## RBAC Configuration

### Step 1: SQL Database Access (Managed Identity)

**Option A: Using Azure AD Authentication** (Recommended)

1. Set Azure AD Admin for SQL Server:
   ```bash
   # Get your user object ID
   $userObjectId = az ad signed-in-user show --query id -o tsv
   
   az sql server ad-admin create `
     --resource-group $resourceGroup `
     --server-name $sqlServerName `
     --display-name "SQL Admin" `
     --object-id $userObjectId
   ```

2. Grant Managed Identity database access (run in SQL Server):
   ```sql
   -- Connect to your database using Azure AD authentication
   CREATE USER [retail-decomposed-app] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [retail-decomposed-app];
   ALTER ROLE db_datawriter ADD MEMBER [retail-decomposed-app];
   ALTER ROLE db_ddladmin ADD MEMBER [retail-decomposed-app];
   ```

**Option B: Using SQL Authentication**

Use connection string with SQL username/password (less secure, not recommended for production).

### Step 2: Azure AI Search RBAC

```bash
# Search Index Data Contributor (read/write documents)
az role assignment create `
  --role "Search Index Data Contributor" `
  --assignee $managedIdentityId `
  --scope "/subscriptions/<subscription-id>/resourceGroups/$resourceGroup/providers/Microsoft.Search/searchServices/$searchServiceName"

# Search Service Contributor (manage indexes)
az role assignment create `
  --role "Search Service Contributor" `
  --assignee $managedIdentityId `
  --scope "/subscriptions/<subscription-id>/resourceGroups/$resourceGroup/providers/Microsoft.Search/searchServices/$searchServiceName"
```

### Step 3: Azure AI Foundry / OpenAI RBAC

```bash
# Get AI Foundry resource ID (adjust based on your setup)
$aiResourceId = "/subscriptions/<subscription-id>/resourceGroups/$resourceGroup/providers/Microsoft.CognitiveServices/accounts/<ai-foundry-name>"

# Cognitive Services OpenAI User (generate embeddings and chat)
az role assignment create `
  --role "Cognitive Services OpenAI User" `
  --assignee $managedIdentityId `
  --scope $aiResourceId
```

### Step 4: Verify Role Assignments

```bash
# List all role assignments for the managed identity
az role assignment list `
  --assignee $managedIdentityId `
  --output table
```

You should see three roles:
- Search Index Data Contributor
- Search Service Contributor
- Cognitive Services OpenAI User

## Application Deployment

### Step 1: Configure App Settings

```bash
# Get connection string (if using SQL authentication)
$sqlConnectionString = az sql db show-connection-string `
  --name $sqlDbName `
  --server $sqlServerName `
  --client ado.net `
  --output tsv

# Replace placeholders in connection string
$sqlConnectionString = $sqlConnectionString -replace "<username>", "sqladmin"
$sqlConnectionString = $sqlConnectionString -replace "<password>", "YourSecurePassword123!"

# Configure app settings
az webapp config appsettings set `
  --name $appName `
  --resource-group $resourceGroup `
  --settings `
    ASPNETCORE_ENVIRONMENT="Production" `
    AzureAd__TenantId="<your-tenant-id>" `
    AzureAd__ClientId="<your-app-registration-client-id>" `
    AzureAd__Domain="<your-domain>.onmicrosoft.com" `
    AzureAd__Instance="https://login.microsoftonline.com/" `
    AzureAd__CallbackPath="/signin-oidc" `
    AzureAI__Endpoint="https://<your-foundry-project>.services.ai.azure.com/" `
    AzureAI__ChatDeploymentName="gpt-4o" `
    AzureAI__ChatModelName="gpt-4o" `
    AzureSearch__Endpoint="https://$searchServiceName.search.windows.net" `
    AzureSearch__IndexName="products-index" `
    AzureSearch__EmbeddingDeploymentName="text-embedding-3-small" `
    ConnectionStrings__DefaultConnection="$sqlConnectionString"
```

**Using Managed Identity for SQL** (Alternative):
```bash
# Connection string for Managed Identity
$sqlConnectionStringMI = "Server=$sqlServerName.database.windows.net;Database=$sqlDbName;Authentication=Active Directory Default;"

az webapp config appsettings set `
  --name $appName `
  --resource-group $resourceGroup `
  --settings `
    ConnectionStrings__DefaultConnection="$sqlConnectionStringMI"
```

### Step 2: Build and Publish Application

```bash
# Navigate to project directory
cd RetailDecomposed

# Publish application
dotnet publish -c Release -o ./publish

# Create deployment package
Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force

# Deploy to Azure
az webapp deployment source config-zip `
  --name $appName `
  --resource-group $resourceGroup `
  --src ./publish.zip
```

### Step 3: Configure Azure Entra ID Redirect URI

1. Navigate to [Azure Portal](https://portal.azure.com)
2. Go to **Azure Entra ID** → **App registrations**
3. Select your app registration
4. Go to **Authentication**
5. Add redirect URI: `https://$appName.azurewebsites.net/signin-oidc`
6. Add logout URI: `https://$appName.azurewebsites.net/signout-callback-oidc`
7. Click **Save**

### Step 4: Run Database Migrations

**Option A: From Local Machine** (Temporary firewall rule)

```bash
# Add your IP to SQL Server firewall
$myIp = (Invoke-WebRequest -Uri "https://api.ipify.org").Content
az sql server firewall-rule create `
  --name TempLocalAccess `
  --server $sqlServerName `
  --resource-group $resourceGroup `
  --start-ip-address $myIp `
  --end-ip-address $myIp

# Run migrations
dotnet ef database update --connection "$sqlConnectionString"

# Remove firewall rule
az sql server firewall-rule delete `
  --name TempLocalAccess `
  --server $sqlServerName `
  --resource-group $resourceGroup
```

**Option B: Using Azure Cloud Shell**

1. Upload migration scripts to Cloud Shell
2. Run migrations from Cloud Shell (has Azure network access)

## Post-Deployment Verification

### Step 1: Test Application Access

```bash
# Get app URL
$appUrl = "https://$appName.azurewebsites.net"
echo "Application URL: $appUrl"

# Open in browser
start $appUrl
```

### Step 2: Test Authentication

1. Click **Sign in**
2. Authenticate with Azure Entra ID
3. Verify your name appears in navigation bar

### Step 3: Initialize Semantic Search

1. Navigate to `/Search` page
2. Click **Create Index** (Admin only)
3. Click **Index Products** (Admin only)
4. Perform test search: "comfortable outdoor clothing"

### Step 4: Test AI Copilot

1. Navigate to `/Copilot` page
2. Send message: "Show me running shoes"
3. Verify AI responds with product recommendations

### Step 5: Check Logs

```bash
# Stream application logs
az webapp log tail `
  --name $appName `
  --resource-group $resourceGroup
```

Look for:
- ✅ "Successfully created SearchClient with Entra ID authentication"
- ✅ "Successfully created AzureOpenAIClient with Entra ID authentication"
- ❌ No 403 Forbidden errors

## Troubleshooting

### Issue: 403 Forbidden when accessing Azure Search

**Solution**: Verify RBAC enabled on Azure Search
```bash
az search service show `
  --name $searchServiceName `
  --resource-group $resourceGroup `
  --query authOptions
```

Should show `aadOrApiKey`. If not, run:
```bash
az search service update `
  --name $searchServiceName `
  --resource-group $resourceGroup `
  --auth-options aadOrApiKey `
  --aad-auth-failure-mode http401WithBearerChallenge
```

### Issue: Database connection fails

**Solution**: Check connection string and firewall rules
```bash
# List firewall rules
az sql server firewall-rule list `
  --server $sqlServerName `
  --resource-group $resourceGroup `
  --output table

# Test connection from Cloud Shell
sqlcmd -S $sqlServerName.database.windows.net -d $sqlDbName -U sqladmin
```

### Issue: Managed Identity not working

**Solution**: Verify role assignments
```bash
# Get managed identity principal ID
$managedIdentityId = az webapp identity show `
  --name $appName `
  --resource-group $resourceGroup `
  --query principalId -o tsv

# List role assignments
az role assignment list `
  --assignee $managedIdentityId `
  --all `
  --output table
```

Wait 5-10 minutes for RBAC propagation after assignment.

### Issue: Authentication redirect fails

**Solution**: Verify redirect URIs match exactly
- Check App Service URL: `https://$appName.azurewebsites.net`
- Check Azure Entra ID redirect URI: must match exactly including `/signin-oidc`

## Security Best Practices

1. **Use Managed Identity**: Never store secrets in App Settings
2. **Enable HTTPS Only**: Always enforce HTTPS
3. **Restrict Firewall**: Only allow necessary IP addresses to SQL Server
4. **Regular Updates**: Keep runtime and packages up to date
5. **Monitor Logs**: Enable Application Insights for monitoring
6. **Least Privilege**: Assign minimum required RBAC roles
7. **Network Security**: Consider using VNet integration and Private Endpoints

## Cost Optimization

- **App Service**: B1 tier ~$13/month (scale to Free F1 for testing)
- **Azure SQL**: S0 tier ~$15/month (use serverless for variable workloads)
- **Azure Search**: Free tier (basic ~$75/month for production)
- **Azure OpenAI**: Pay-per-use (embeddings + chat tokens)

**Total Estimated Cost**: ~$30-50/month (without Azure OpenAI usage)

## Additional Resources

- [Azure App Service Documentation](https://learn.microsoft.com/azure/app-service/)
- [Azure SQL Database Documentation](https://learn.microsoft.com/azure/azure-sql/)
- [Azure RBAC Documentation](https://learn.microsoft.com/azure/role-based-access-control/)
- [Managed Identity Documentation](https://learn.microsoft.com/azure/active-directory/managed-identities-azure-resources/)

---

**Last Updated**: 2025-01-23  
**Tested With**: Azure CLI 2.66.0, .NET 9.0
