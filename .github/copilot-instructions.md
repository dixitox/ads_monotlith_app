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

## Documentation
- Main AI Copilot Guide: `/AI_COPILOT_COMPLETE_GUIDE.md`
- Test Documentation: `/Tests/README.md`
- Authentication Setup: `/RetailDecomposed/AUTHENTICATION_SETUP.md`

---

**Remember**: Unless explicitly stated otherwise by the user, ALL work is on **RetailDecomposed** running at **http://localhost:6068**.
