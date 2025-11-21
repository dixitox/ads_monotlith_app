# Microsoft Entra ID Authentication Setup Guide

This guide explains how to configure Microsoft Entra ID (formerly Azure AD) authentication for the RetailDecomposed application.

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
- **Application (client) ID**: `cddeea42-e4cc-4986-994f-5b842a63e4b5`
- **Directory (tenant) ID**: `714deb98-bf8b-47eb-8625-7fff26845f87`
- **Directory (tenant) name**: `MngEnvMCAP802465.onmicrosoft.com`

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

### 3.1 Update appsettings.json
Update the `appsettings.json` file in the RetailDecomposed project with your Azure App Registration values:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "your-domain.onmicrosoft.com",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.Identity": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

### 3.2 Update appsettings.Development.json
Update the development settings file with the same values:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "Domain": "your-domain.onmicrosoft.com",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
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

**Replace the following values:**
- `your-domain.onmicrosoft.com` → Your Azure AD tenant domain
- `your-tenant-id` → Your Directory (tenant) ID from Step 1.3
- `your-client-id` → Your Application (client) ID from Step 1.3

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
- **Orders Page** (`/Orders`): Requires Admin role (AdminOnly policy)
- **Order Details** (`/Orders/Details`): Requires authentication (CustomerAccess policy)

**Testing Admin Access:**
1. Sign in as a user with Admin role assigned
2. Navigate to `/Orders/Index`
3. You should see the orders list (only Admins can access this)

**Testing Customer Access:**
1. Sign in as a user without Admin role
2. Try to navigate to `/Orders/Index`
3. You should receive an Access Denied error

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

1. **Never commit secrets**: Keep appsettings.json out of source control if it contains real credentials
2. **Use User Secrets**: For local development, use .NET User Secrets:
   ```powershell
   dotnet user-secrets init
   dotnet user-secrets set "AzureAd:ClientId" "your-client-id"
   dotnet user-secrets set "AzureAd:TenantId" "your-tenant-id"
   ```
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
