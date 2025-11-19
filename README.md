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

Access the app at `https://localhost:5001` or `http://localhost:5000`.

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

### Required Environment Variables for Azure Services

The application uses Azure Cognitive Search and Azure OpenAI services. These credentials must be configured as environment variables for the application to function properly:

| Variable                      | Description                                    | Required |
| ----------------------------- | ---------------------------------------------- | -------- |
| `AZURE_SEARCH_ENDPOINT`       | Azure Cognitive Search service endpoint        | Yes*     |
| `AZURE_SEARCH_API_KEY`        | Azure Cognitive Search API key                 | Yes*     |
| `AZURE_OPENAI_ENDPOINT`       | Azure OpenAI service endpoint                  | Yes*     |
| `AZURE_OPENAI_API_KEY`        | Azure OpenAI service API key                   | Yes*     |
| `ConnectionStrings__DefaultConnection` | Database connection string            | No       |
| `ASPNETCORE_ENVIRONMENT`      | Environment mode                               | No       |

\* Required only if you plan to use Azure Search or Azure OpenAI features (chat assistant, product search).

### Setting Environment Variables

#### For Local Development

**Linux/macOS:**
```bash
export AZURE_SEARCH_ENDPOINT="https://your-search-service.search.windows.net"
export AZURE_SEARCH_API_KEY="your-search-api-key"
export AZURE_OPENAI_ENDPOINT="https://your-openai-service.openai.azure.com/"
export AZURE_OPENAI_API_KEY="your-openai-api-key"
```

**Windows (Command Prompt):**
```cmd
set AZURE_SEARCH_ENDPOINT=https://your-search-service.search.windows.net
set AZURE_SEARCH_API_KEY=your-search-api-key
set AZURE_OPENAI_ENDPOINT=https://your-openai-service.openai.azure.com/
set AZURE_OPENAI_API_KEY=your-openai-api-key
```

**Windows (PowerShell):**
```powershell
$env:AZURE_SEARCH_ENDPOINT="https://your-search-service.search.windows.net"
$env:AZURE_SEARCH_API_KEY="your-search-api-key"
$env:AZURE_OPENAI_ENDPOINT="https://your-openai-service.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY="your-openai-api-key"
```

#### For GitHub Codespaces

GitHub Codespaces automatically provides access to repository secrets as environment variables. To configure:

1. Navigate to your repository on GitHub
2. Go to **Settings** → **Secrets and variables** → **Codespaces** (not Actions!)
3. Add the following secrets:
   - `AZURE_SEARCH_ENDPOINT`
   - `AZURE_SEARCH_API_KEY`
   - `AZURE_OPENAI_ENDPOINT`
   - `AZURE_OPENAI_API_KEY`

**Important:** Make sure to add secrets under the **Codespaces** tab, not the **Actions** tab. Codespaces secrets are separate from Actions secrets.

These secrets will be automatically available as environment variables when you create or restart a Codespace.

#### For Docker/Dev Containers

Create a `.env` file in the root directory (this file is git-ignored):

```env
AZURE_SEARCH_ENDPOINT=https://your-search-service.search.windows.net
AZURE_SEARCH_API_KEY=your-search-api-key
AZURE_OPENAI_ENDPOINT=https://your-openai-service.openai.azure.com/
AZURE_OPENAI_API_KEY=your-openai-api-key
```

Then update `docker-compose.yml` or `.devcontainer/devcontainer.json` to load these variables.

**Note:** Never commit the `.env` file or any files containing API keys to source control.
