# GitHub Copilot Instructions for ads_monotlith_app Repository

## Project Structure

This repository contains TWO separate ASP.NET Core applications:

### 1. RetailMonolith (Legacy Monolithic Application)
- **Location**: Root directory (`/`)
- **Port**: http://localhost:5068
- **Purpose**: Original monolithic retail application
- **Status**: Legacy - DO NOT MODIFY unless explicitly requested

### 2. RetailDecomposed (Modern Decomposed Application)
- **Location**: `/RetailDecomposed/` directory
- **Port**: http://localhost:6068 (HTTPS)
- **Purpose**: Modernized application with decomposed architecture and AI features
- **Status**: ACTIVE DEVELOPMENT - **ALL CHANGES SHOULD BE MADE HERE**

## Critical Rules

### ⚠️ Default Working Application
**ALWAYS work on the RetailDecomposed application unless the user explicitly specifies RetailMonolith.**

When the user says:
- "the app" → RetailDecomposed
- "run the app" → `cd RetailDecomposed; dotnet run`
- "update the configuration" → RetailDecomposed/appsettings.Development.json
- "modify Program.cs" → RetailDecomposed/Program.cs
- "add a service" → RetailDecomposed/Services/
- "update a page" → RetailDecomposed/Pages/

### Port Reference
- **RetailMonolith**: http://localhost:5068 (DO NOT USE unless explicitly requested)
- **RetailDecomposed**: http://localhost:6068 (DEFAULT - ALWAYS USE)

### File Paths
When editing files, always use paths in the RetailDecomposed directory:
- ✅ `RetailDecomposed/Program.cs`
- ✅ `RetailDecomposed/appsettings.Development.json`
- ✅ `RetailDecomposed/Services/CopilotService.cs`
- ❌ `Program.cs` (this is the monolith)
- ❌ `appsettings.Development.json` (this is the monolith)

### Running the Application
**Default command**:
```powershell
cd RetailDecomposed; dotnet run
```

**NOT**:
```powershell
dotnet run  # This runs RetailMonolith from root
```

### Configuration Files
- **Active**: `RetailDecomposed/appsettings.Development.json`
- **Ignore**: `appsettings.Development.json` (root - monolith)

## Current Features in RetailDecomposed

### Active Features
- ✅ Azure AI Foundry integration with Entra ID authentication
- ✅ AI Copilot chat service (`/api/copilot/chat`)
- ✅ Decomposed architecture with separate API clients
- ✅ Products API, Cart API, Orders API clients
- ✅ Entity Framework Core with SQL Server
- ✅ ASP.NET Core Identity with cookie authentication

### Key Services
- `ICopilotService` / `CopilotService` - AI chat functionality
- `IProductsApiClient` / `ProductsApiClient` - Products module API client
- `ICartApiClient` / `CartApiClient` - Cart module API client
- `IOrdersApiClient` / `OrdersApiClient` - Orders module API client

## Azure AI Configuration

**Endpoint Format**: 
- ✅ `https://<resource>.openai.azure.com/`
- ✅ `https://<project>.services.ai.azure.com/openai`
- ❌ `https://<project>.services.ai.azure.com/api/projects/<project-name>`

**Authentication**: Entra ID only (no API keys)

## Development Workflow

1. **Before any code changes**: Verify you're working in `RetailDecomposed/`
2. **When running the app**: Use `cd RetailDecomposed; dotnet run`
3. **When testing**: Navigate to http://localhost:6068 (HTTPS)
4. **When debugging**: Monitor logs from RetailDecomposed process

## Documentation Best Practices

### 📋 Avoid Duplication - Reuse Content
**CRITICAL**: Always check for existing documentation before creating new files.

#### Rules for Markdown Files:
1. **Single Source of Truth**: Maintain ONE authoritative document per topic
   - ✅ `Tests/TEST_RESULTS.md` - Consolidated test results (all sessions)
   - ❌ `Tests/TEST_RESULTS_SESSION_10.md`, `Tests/TEST_RESULTS_SESSION_11.md` - Don't create session-specific duplicates

2. **Merge, Don't Multiply**:
   - When updating test results, update the existing `TEST_RESULTS.md`
   - Add new sections or update existing sections
   - Include session information within the main document

3. **Reference, Don't Duplicate**:
   - Use links to refer to other documentation
   - Example: Link to existing docs instead of copying content

4. **Update Existing Documents**:
   - Add "Development History" sections to track changes over time
   - Use "Last Updated" dates at the top
   - Include session notes within existing structure

5. **Before Creating New .md Files**:
   - Check if content can be added to existing documentation
   - Search for related files: `file_search` for `*.md` files
   - Ask: "Does this information fit in an existing document?"

#### Existing Documentation Structure:
- `/AI_COPILOT_COMPLETE_GUIDE.md` - AI Copilot implementation guide
- `/Tests/TEST_RESULTS.md` - Consolidated test results (all sessions)
- `/Tests/README.md` - Test documentation overview
- `/RetailDecomposed/AUTHENTICATION_SETUP.md` - Authentication configuration
- `/.github/copilot-instructions.md` - This file (project guidelines)

## Production Deployment Considerations

### Configuration Management
- **Development**: Uses `appsettings.Development.json` with local settings
- **Production**: Uses `appsettings.json` as base configuration
- **Best Practice**: Override production settings using Azure App Service Configuration or environment variables (never commit secrets to source control)

### Production Settings to Configure

1. **Azure AD Authentication**:
   - Update `AzureAd:TenantId`, `AzureAd:ClientId`, `AzureAd:Domain` in Azure App Service Configuration
   - Use production Azure AD app registration

2. **Azure AI Foundry**:
   - Update `AzureAI:Endpoint` to production Azure AI resource
   - Enable Managed Identity on App Service and assign "Cognitive Services OpenAI User" role
   - Remove API keys (use Entra ID authentication)

3. **Database Connection**:
   - Production uses Azure SQL Database (not LocalDB)
   - Recommended: Use Managed Identity for database access:
     ```
     Server=your-server.database.windows.net;Database=ApplicationDB;Authentication=Active Directory Default;
     ```
   - Alternative: Store connection string in Azure Key Vault or App Service Configuration

4. **Managed Identity Setup**:
   - Enable System-Assigned or User-Assigned Managed Identity on Azure App Service/Container App
   - Assign required roles:
     - `Cognitive Services OpenAI User` (for Azure AI)
     - `SQL DB Contributor` or custom role (for database)

5. **Security**:
   - Set `DetailedErrors: false` in production
   - Configure appropriate CORS policies
   - Use HTTPS only
   - Review `AllowedHosts` setting

### Deployment Checklist
- [ ] Configure Azure App Service settings (don't hardcode in appsettings.json)
- [ ] Enable and configure Managed Identity
- [ ] Assign RBAC roles for Azure AI and SQL Database
- [ ] Update database connection string for Azure SQL
- [ ] Configure production Azure AD app registration
- [ ] Test authentication flow in production environment
- [ ] Verify AI Copilot connectivity with production endpoint
- [ ] Set up Application Insights for monitoring
- [ ] Configure logging levels (reduce verbosity in production)

---

**Remember**: Unless explicitly stated otherwise by the user, ALL work is on **RetailDecomposed** running at **http://localhost:6068**.
