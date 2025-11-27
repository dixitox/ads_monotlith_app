# Microsoft Entra ID Authentication Setup Guide

This guide explains how to configure Microsoft Entra ID (formerly Azure AD) authentication for the RetailDecomposed application.

## Related Documentation

- **[SEMANTIC_SEARCH_GUIDE.md](SEMANTIC_SEARCH_GUIDE.md)** - Complete guide for semantic search with RBAC and Azure AI services
- **[README.md](README.md)** - Project overview and getting started
- **[DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)** - Production deployment guide

## Overview

The RetailDecomposed application now includes:
- ✅ Secure sign-in with Microsoft Entra ID
- ✅ Role-based access control (Admin/Customer roles)
- ✅ Protected API endpoints
- ✅ Authenticated service-to-service calls
- ✅ User-friendly login/logout UI

## Prerequisites

- Azure subscription
- Permissions to create App Registrations in Microsoft Entra ID
- .NET 9.0 SDK installed
- Visual Studio 2022 or VS Code

## Step 1: Create Azure App Registration

### 1.1 Sign in to Azure Portal
1. Navigate to [Azure Portal](https://portal.azure.com)
2. Go to **Microsoft Entra ID** (formerly Azure Active Directory)
3. Select **App registrations** from the left menu
4. Click **+ New registration**

### 1.2 Configure App Registration
1. **Name**: `RetailDecomposed-App`
2. **Supported account types**: 
   - Select "Accounts in this organizational directory only" for single tenant
   - OR "Accounts in any organizational directory" for multi-tenant
3. **Redirect URI**:
   - Platform: **Web**
   - URI: `https://localhost:6068/signin-oidc` (adjust port if needed)
4. Click **Register**

### 1.3 Note Important Values
After registration, note these values from the **Overview** page:
- **Application (client) ID**: `your-client-id-here`
- **Directory (tenant) ID**: `your-tenant-id-here`
- **Directory (tenant) name**: `your-tenant-name-here`

**⚠️ Important**: Keep these values secure. You'll use them to configure your application in Step 3.

### 1.4 Configure Authentication Settings
1. Go to **Authentication** in the left menu
2. Under **Front-channel logout URL**, add: `https://localhost:6068/signout-callback-oidc`
3. Under **Implicit grant and hybrid flows**, check:
   - ☑️ **ID tokens** (used for implicit and hybrid flows)
4. Click **Save**

### 1.5 (Optional) Create Client Secret
If you need to call downstream APIs that require application permissions:
1. Go to **Certificates & secrets**
2. Click **+ New client secret**
3. Add description: `RetailDecomposed-Secret`
4. Select expiration period
5. Click **Add**
6. **Important**: Copy the secret value immediately (you won't see it again)

### 1.6 Configure API Permissions
1. Go to **API permissions**
2. The following permission should already be present:
   - **Microsoft Graph** → **User.Read** (Delegated)
3. For role-based access, you may need additional permissions
4. Click **Grant admin consent** if required by your organization

## Step 2: Configure App Roles (for Role-Based Access Control)

### 2.1 Create App Roles
1. In your App Registration, go to **App roles**
2. Click **+ Create app role**

**Admin Role:**
- **Display name**: `Admin`
- **Allowed member types**: ☑️ Users/Groups
- **Value**: `Admin`
- **Description**: `Administrators can view all orders and manage the system`
- **Enable this app role**: ☑️ Checked
- Click **Apply**

**Customer Role (Optional):**
- **Display name**: `Customer`
- **Allowed member types**: ☑️ Users/Groups
- **Value**: `Customer`
- **Description**: `Customers can browse products, manage cart, and place orders`
- **Enable this app role**: ☑️ Checked
- Click **Apply**

### 2.2 Assign Roles to Users
1. Go back to **Microsoft Entra ID** → **Enterprise applications**
2. Find and select your app: `RetailDecomposed-App`
3. Go to **Users and groups**
4. Click **+ Add user/group**
5. Select a user
6. Select a role (Admin or Customer)
7. Click **Assign**

## Step 3: Configure Application Settings

### 3.1 Configure Using User Secrets (Recommended for Local Development)

**✅ This is the recommended approach** to keep your Azure AD credentials secure and out of source control.

The appsettings.json files in the repository contain placeholder values. Instead of editing these files directly, use .NET User Secrets to store your actual credentials locally:

1. **Initialize User Secrets** (if not already initialized):
   ```powershell
   cd RetailDecomposed
   dotnet user-secrets init
   ```

2. **Set Your Azure AD Configuration**:
   ```powershell
   dotnet user-secrets set "AzureAd:ClientId" "your-client-id-here"
   dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id-here"
   dotnet user-secrets set "AzureAd:Domain" "your-domain.onmicrosoft.com"
   ```

   Replace the values with your actual values from Step 1.3:
   - `your-client-id-here` → Your Application (client) ID
   - `your-tenant-id-here` → Your Directory (tenant) ID
   - `your-domain.onmicrosoft.com` → Your Azure AD tenant domain

3. **Verify Your Secrets** (optional):
   ```powershell
   dotnet user-secrets list
   ```

**How It Works:**
- User Secrets are stored outside your project directory in a JSON file on your local machine
- They override the placeholder values in appsettings.json at runtime
- They are never checked into source control
- Each developer can have their own secrets without conflicts

### 3.2 Alternative: Update appsettings.Development.json (Not Recommended)

If you prefer not to use User Secrets, you can update `appsettings.Development.json` directly. However, **make sure this file is in .gitignore** to prevent accidentally committing credentials.

Update the development settings file with your values:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "your-domain.onmicrosoft.com",
    "TenantId": "your-tenant-id-here",
    "ClientId": "your-client-id-here",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  },
  "DetailedErrors": true,
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ApplicationDB;Integrated Security=true;TrustServerCertificate=True;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.Identity": "Debug"
    }
  }
}
```

**⚠️ Security Note:** The `appsettings.json` file should always contain placeholder values only. Never commit real credentials to source control.

### 3.3 Docker Container Configuration

When running the application in Docker containers, you need to provide Azure AD credentials as environment variables. The Docker Compose file uses placeholder values by default, which causes the app to run in **no-auth mode**.

#### Why Docker Containers Don't Use Entra ID by Default

The `docker-compose.microservices.yml` file includes these placeholder environment variables:

```yaml
- AzureAd__TenantId=your-tenant-id
- AzureAd__ClientId=your-client-id
- AzureAd__Domain=your-domain.onmicrosoft.com
```

When the app starts, it validates these values. Since `"your-tenant-id"` and `"your-client-id"` are not valid GUIDs, the validation fails and the app automatically falls back to no-auth mode:

```
Azure AD not configured - using no-auth mode
```

#### Option A: Use Environment File (Recommended)

Create a `.env` file in the `RetailDecomposed` directory (this file is in `.gitignore`):

```bash
# .env file for Docker Compose
AZURE_AD_TENANT_ID=your-actual-tenant-id-here
AZURE_AD_CLIENT_ID=your-actual-client-id-here
AZURE_AD_DOMAIN=yourdomain.onmicrosoft.com
```

Then update `docker-compose.microservices.yml` to use these variables:

```yaml
frontend-service:
  environment:
    - AzureAd__TenantId=${AZURE_AD_TENANT_ID}
    - AzureAd__ClientId=${AZURE_AD_CLIENT_ID}
    - AzureAd__Domain=${AZURE_AD_DOMAIN}
```

Docker Compose will automatically load the `.env` file.

#### Option B: Set Environment Variables Before Running

Set environment variables in your PowerShell session before starting containers:

```powershell
$env:AZURE_AD_TENANT_ID = "your-actual-tenant-id-here"
$env:AZURE_AD_CLIENT_ID = "your-actual-client-id-here"
$env:AZURE_AD_DOMAIN = "yourdomain.onmicrosoft.com"

# Then run the containers
.\run-both-apps.ps1 -Mode container
```

#### Option C: Edit Docker Compose File Directly (Not Recommended for Shared Repos)

If you're working alone, you can edit the values directly in `docker-compose.microservices.yml`:

```yaml
frontend-service:
  environment:
    - AzureAd__TenantId=12345678-1234-1234-1234-123456789abc
    - AzureAd__ClientId=87654321-4321-4321-4321-cba987654321
    - AzureAd__Domain=yourdomain.onmicrosoft.com
```

**⚠️ Warning:** Make sure not to commit real credentials if the file is in source control!

#### Important: Update Redirect URIs for Container Mode

When running in containers, the app is accessible at `http://localhost:8080` instead of `https://localhost:6068`. Update your Azure App Registration:

1. Go to **Azure Portal** → **App registrations** → Your app
2. Go to **Authentication**
3. Add redirect URI: `http://localhost:8080/signin-oidc`
4. Add front-channel logout URL: `http://localhost:8080/signout-callback-oidc`
5. Click **Save**

#### Verify Authentication in Containers

After starting containers with real credentials:

```powershell
# Check the logs to verify authentication is enabled
docker logs retaildecomposed-frontend --tail 50
```

You should see:
```
Using Azure AD authentication with TenantId: your-tenant-id
```

Instead of:
```
Azure AD not configured - using no-auth mode
```

#### Testing Container Authentication

1. Start containers with real Azure AD credentials
2. Open `http://localhost:8080`
3. Click **Sign in** button
4. You should be redirected to Microsoft login page
5. After sign-in, you'll be redirected back to the app

## Step 4: Run and Test the Application

### 4.1 Restore NuGet Packages
```powershell
cd RetailDecomposed
dotnet restore
```

### 4.2 Build the Application
```powershell
dotnet build
```

### 4.3 Run the Application
```powershell
dotnet run
```

The application should start at `https://localhost:6068` (or the port specified in launchSettings.json)

### 4.4 Test Authentication
1. Navigate to `https://localhost:6068`
2. Click **Sign in** in the navigation bar
3. You'll be redirected to Microsoft login page
4. Sign in with your Microsoft/Azure AD account
5. After successful authentication, you'll be redirected back to the application
6. You should see your name and role displayed in the navigation bar
7. Click **Sign out** to test logout functionality

### 4.5 Test Role-Based Access
- **Products Page** (`/Products`): Requires authentication (CustomerAccess policy)
- **Cart Page** (`/Cart`): Requires authentication (CustomerAccess policy)
- **Checkout Page** (`/Checkout`): Requires authentication (CustomerAccess policy)
- **Orders Page** (`/Orders`): Requires authentication (CustomerAccess policy). Admin users see all orders; non-admin users see only their own orders.
- **Order Details** (`/Orders/Details`): Requires authentication (CustomerAccess policy)

**Testing Admin Access:**
1. Sign in as a user with Admin role assigned
2. Navigate to `/Orders/Index`
3. You should see the orders list (all orders are visible to Admins)

**Testing Customer Access:**
1. Sign in as a user without Admin role
2. Navigate to `/Orders/Index`
3. You should see only your own orders listed (non-admin users do not see other users' orders)

## Step 5: API Endpoints Security

All API endpoints are now protected with authentication:

| Endpoint | Method | Authorization Policy |
|----------|--------|---------------------|
| `/api/cart/{customerId}` | GET | CustomerAccess |
| `/api/cart/{customerId}/items` | POST | CustomerAccess |
| `/api/products` | GET | CustomerAccess |
| `/api/products/{id}` | GET | CustomerAccess |
| `/api/orders` | GET | AdminOnly |
| `/api/orders/{id}` | GET | CustomerAccess |
| `/api/checkout` | POST | CustomerAccess |

## Troubleshooting

### Issue: "AADSTS50011: The redirect URI does not match"
**Solution**: Ensure the redirect URI in your App Registration matches exactly: `https://localhost:6068/signin-oidc`

### Issue: "AADSTS700016: Application not found in directory"
**Solution**: Double-check that the ClientId and TenantId in appsettings.json match your App Registration

### Issue: User can't access Orders page even with Admin role
**Solution**: 
1. Verify the user has the Admin role assigned in Enterprise Applications
2. Sign out and sign back in to refresh the token with updated roles
3. Check the role claim in the token by adding logging to see User.Claims

### Issue: "Unable to obtain configuration from" error
**Solution**: Check internet connectivity and ensure the TenantId is correct

### Issue: Certificate validation errors in development
**Solution**: Trust the development certificate:
```powershell
dotnet dev-certs https --trust
```

## Security Best Practices

1. **Never commit secrets**: Always use placeholder values in appsettings.json files that are committed to source control
2. **Use User Secrets**: For local development, use .NET User Secrets (see Step 3.1 above)
3. **Production Configuration**: Use Azure Key Vault or Azure App Configuration for production
4. **HTTPS Only**: Always use HTTPS in production
5. **Role Validation**: Validate roles on both client and server sides
6. **Token Lifetime**: Configure appropriate token lifetime in Azure AD
7. **Audit Logging**: Enable audit logging for authentication events

## Additional Resources

- [Microsoft Identity Platform Documentation](https://docs.microsoft.com/azure/active-directory/develop/)
- [Microsoft.Identity.Web Documentation](https://docs.microsoft.com/azure/active-directory/develop/microsoft-identity-web)
- [ASP.NET Core Security](https://docs.microsoft.com/aspnet/core/security/)
- [Azure AD Authentication Flows](https://docs.microsoft.com/azure/active-directory/develop/authentication-flows-app-scenarios)

## Architecture Overview

The authentication implementation includes:

1. **Program.cs**: 
   - Configures Microsoft.Identity.Web authentication
   - Defines authorization policies (AdminOnly, CustomerAccess)
   - Adds authentication middleware
   - Configures HttpClients with token propagation

2. **Razor Pages**:
   - Protected with `[Authorize]` attributes
   - Role-based policies applied via `Policy` parameter

3. **API Endpoints**:
   - Secured with `.RequireAuthorization()` extension
   - Different policies for different access levels

4. **UI Components**:
   - `_LoginPartial.cshtml`: Sign-in/Sign-out buttons and user info
   - `_Layout.cshtml`: Integrated login partial

5. **API Clients**:
   - HttpClients configured with `.AddUserAccessTokenHandler()`
   - Automatically propagate authentication tokens to API calls

## Next Steps

- Configure Azure Key Vault for production secrets
- Implement custom claims and policies
- Add multi-factor authentication (MFA)
- Configure conditional access policies
- Implement API scopes for fine-grained permissions
- Set up monitoring and alerts for authentication events
