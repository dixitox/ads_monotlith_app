# Retail Monolith App

A lightweight ASP.NET Core 9 Razor Pages application that simulates a retail monolith before decomposition.  
It includes product listing, shopping cart, checkout, and inventory management — built to demonstrate modernisation and refactoring patterns.

---

## Features

- ASP.NET Core 9 (Razor Pages)
- Entity Framework Core (SQL Server LocalDB)
- Dependency Injection with modular services:
  - `CartService`
  - `CheckoutService`
  - `MockPaymentGateway`
- 50 sample seeded products with random inventory
- End-to-end retail flow:
  - Products → Cart → Checkout → Orders
- Minimal APIs:
  - `POST /api/checkout`
  - `GET /api/orders/{id}`
- Health-check endpoint at `/health`
- Ready for decomposition into microservices

---

## 🏠 Home Page
![Home Page Screenshot](https://github.com/lavann/ads_monotlith_app/blob/main/Images/HomePage.jpg)

## 🛍 Products
![Products Screenshot](https://github.com/lavann/ads_monotlith_app/blob/main/Images/Products.jpg)

## 🧺 Cart
![Cart Screenshot](https://github.com/lavann/ads_monotlith_app/blob/main/Images/Cart.jpg)

## 💳 Checkout
![Checkout Screenshot](https://github.com/lavann/ads_monotlith_app/blob/main/Images/CheckOut.jpg)

## 📦 Orders
![Orders Screenshot](https://github.com/lavann/ads_monotlith_app/blob/main/Images/Orders.jpg)

## 📦 Order Details
![Orders Screenshot](https://github.com/lavann/ads_monotlith_app/blob/main/Images/OrderDetails.jpg)

---

## Development Setup

You can run and edit this application in three different ways:

### 1. Local Development Environment

Run the application directly on your local machine with your preferred IDE or editor.

**Prerequisites:**
- .NET 9 SDK installed ([download](https://dotnet.microsoft.com/download/dotnet/9.0))
- SQL Server LocalDB (included with Visual Studio) or SQL Server instance
- Your favorite code editor (Visual Studio, VS Code, Rider, etc.)

**Steps:**
```bash
git clone https://github.com/lavann/ads_monotlith_app.git
cd ads_monotlith_app
dotnet restore
dotnet ef database update
dotnet run
```

### 2. Docker-Hosted Dev Container

Use a Docker container with a pre-configured development environment. This ensures consistency across different machines without installing dependencies locally.

**Prerequisites:**
- Docker Desktop installed and running
- Visual Studio Code with the Dev Containers extension

**Steps:**
1. Clone the repository
2. Open the folder in VS Code
3. When prompted, click "Reopen in Container" (or use Command Palette: `Dev Containers: Reopen in Container`)
4. VS Code will build and start the dev container with all dependencies pre-installed
5. Run `dotnet ef database update` and `dotnet run` inside the container terminal

### 3. GitHub Codespaces

Develop entirely in the cloud with zero local setup. Codespaces provides a full VS Code environment in your browser.

**Prerequisites:**
- GitHub account with Codespaces access

**Steps:**
1. Navigate to the repository on GitHub
2. Click the green "Code" button
3. Select the "Codespaces" tab
4. Click "Create codespace on main"
5. Wait for the environment to initialize
6. Run `dotnet ef database update` and `dotnet run` in the integrated terminal

All three environments provide the same development experience with the .NET SDK, C# extension, and all necessary tools pre-configured.

---

## Database & Migrations

### Apply existing migrations
dotnet ef database update

### Create a new migration

- If you modify models:
	- `dotnet ef migrations add <MigrationName>`
	- `dotnet ef database update`

- EF Core uses DesignTimeDbContextFactory (Data/DesignTimeDbContextFactory.cs)
with the connection string:
	- `Server=(localdb)\MSSQLLocalDB;Database=RetailMonolith;Trusted_Connection=True;MultipleActiveResultSets=true`

### Seeding Sample Data

At startup, the app automatically runs `await AppDbContext.SeedAsync(db);` which seeds 50 sample products with random categories, prices, and inventory.

To reseed manually:
```bash
dotnet ef database drop -f
dotnet ef database update
dotnet run
```

---

## Running the Application

Start the application:
```bash
dotnet run
```

Access the app at:
- HTTP: `http://localhost:5068`
- HTTPS: `https://localhost:7108`

To specify a launch profile:
```bash
dotnet run --launch-profile http   # HTTP on port 5068
dotnet run --launch-profile https  # HTTPS on port 7108
```

### Available Endpoints

| Path               | Description           |
| ------------------ | --------------------- |
| `/`                | Home Page             |
| `/Products`        | Product catalogue     |
| `/Cart`            | Shopping cart         |
| `/api/checkout`    | Checkout API          |
| `/api/orders/{id}` | Order details API     |
| `/health`          | Health check endpoint |

---

## Environment Variables (optional)
You can override the default connection string by setting the `ConnectionStrings__DefaultConnection` environment variable.
| Variable                               | Description                | Default          |
| -------------------------------------- | -------------------------- | ---------------- |
| `ConnectionStrings__DefaultConnection` | Database connection string | LocalDB instance |
| `ASPNETCORE_ENVIRONMENT`               | Environment mode           | `Development`    |

---

## Azure Kubernetes Service (AKS) Deployment 🚀

**RetailMonolith is now fully containerized and deployed to Azure!**

### Production Deployment
- **URL**: https://145.133.57.234
- **Region**: UK South
- **Authentication**: Azure AD with Managed Identity
- **Security**: HTTPS-only with TLS encryption
- **High Availability**: 2 replicas with auto-healing

### Complete Deployment Guide
See **[MONOLITH_DEPLOYMENT_GUIDE.md](MONOLITH_DEPLOYMENT_GUIDE.md)** for:
- Step-by-step deployment instructions
- Azure infrastructure setup
- Azure AD authentication configuration
- Kubernetes manifests and configuration
- Troubleshooting real issues encountered
- Production hardening recommendations
- Cost estimates and optimization tips

### Quick Deploy Summary
```powershell
# 1. Create Azure infrastructure (ACR, AKS, SQL)
.\setup-azure-infrastructure-monolith.ps1

# 2. Configure Azure AD authentication
.\configure-azure-ad-auth.ps1

# 3. Build and push Docker image
.\build-and-push-monolith.ps1 -AcrName "acrretailmonolith"

# 4. Deploy to AKS
.\deploy-monolith.ps1 -WaitForReady
```

**Total deployment time**: ~30 minutes | **Monthly cost**: ~£115

---

## Testing 🧪

**✅ 100% Passing - All 295 Tests**

Comprehensive testing suite covering unit tests, integration tests, and full Docker deployment validation.

### Quick Start
```powershell
# Run ALL tests (Unit + Integration + Docker + Microservices)
.\Tests\run-all-tests.ps1

# Run only unit/integration tests
dotnet test

# Run only Docker Compose tests (Monolith)
.\Tests\run-local-tests.ps1

# Run only Microservices tests
.\Tests\test-microservices-deployment.ps1
```

### Test Results Summary
| Test Suite | Tests | Status | Duration |
|------------|-------|--------|----------|
| RetailMonolith.Tests | 127 | ✅ 100% | ~3s |
| RetailDecomposed.Tests | 127 | ✅ 100% | ~31s |
| Monolith Docker | 11 | ✅ 100% | ~30s |
| Microservices | 32 | ✅ 100% | ~75s |
| **Total** | **295** | **✅ 100%** | **~140s** |

### Port Configuration
**Both systems run simultaneously!**
- **Monolith SQL Server**: Port 1433
- **Microservices SQL Server**: Port 1434
- **No port conflicts** - Full test suite runs in one go

### What's Tested
- ✅ All pages: Products, Cart, Orders, Checkout, AI Copilot
- ✅ API endpoints (Products, Cart, Orders, Checkout)
- ✅ Authentication & Authorization (Azure AD simulation)
- ✅ Database connectivity and migrations
- ✅ Docker container health checks
- ✅ Inter-service communication (microservices)
- ✅ Response time performance
- ✅ AI Copilot API and UI
- ✅ Observability (OpenTelemetry tracing)

### Documentation
- **[Tests/README.md](Tests/README.md)** - Complete testing guide with port configuration
- **[Tests/TEST_RESULTS.md](Tests/TEST_RESULTS.md)** - Detailed test results and history
- **[Tests/LOCAL_TESTING_GUIDE.md](Tests/LOCAL_TESTING_GUIDE.md)** - Docker testing deep dive

---

## Project Structure

```
ads_monotlith_app/
├── RetailMonolith/              # This monolithic application (root)
│   ├── Program.cs
│   ├── Data/                    # EF Core DbContext
│   ├── Models/                  # Product, Cart, Order entities
│   ├── Services/                # Business logic services
│   ├── Pages/                   # Razor Pages UI
│   ├── Dockerfile.monolith      # Docker containerization
│   └── k8s/monolith/           # Kubernetes manifests
│
├── RetailDecomposed/            # Modernized microservices version
│   ├── (Future deployment)
│   └── (See RetailDecomposed/README.md)
│
└── Tests/                       # Unit and integration tests
