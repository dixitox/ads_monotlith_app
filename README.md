# Retail Monolith App

A lightweight ASP.NET Core 9 Razor Pages application demonstrating microservices decomposition patterns.  
The application includes product listing, shopping cart, and order management. The **Checkout Service** has been extracted into a standalone microservice API.

---

## 🏗️ Architecture

This repository demonstrates the **Decomposition Pattern** for transitioning from a monolithic architecture to microservices.

### Current Architecture

```
┌─────────────────────┐      ┌──────────────────┐
│  Retail Monolith    │      │  Checkout API    │
│  (Razor Pages)      │─────→│  (REST API)      │
│                     │ HTTP │                  │
│ • Product Catalog   │      │ • Checkout Logic │
│ • Shopping Cart     │      │ • Payment        │
│ • Order Viewing     │      │ • Order Creation │
└─────────────────────┘      └──────────────────┘
           │                          │
           └──────────┬───────────────┘
                      ↓
              ┌───────────────┐
              │   SQL Server  │
              │   (Shared DB) │
              └───────────────┘
```

### Service Boundaries

| Service | Responsibilities |
|---------|-----------------|
| **Retail Monolith** | Product catalog, cart management, order viewing, UI |
| **Checkout API** | Checkout orchestration, inventory reservation, payment processing, order creation |
| **Database** | Shared database (transitional pattern) |

## Features

### Retail Monolith
- ASP.NET Core 9 (Razor Pages)
- Entity Framework Core (SQL Server)
- Dependency Injection with modular services:
  - `CartService` - Shopping cart management
- 50 sample seeded products with random inventory
- End-to-end retail flow:
  - Products → Cart → **Checkout API** → Orders
- Minimal APIs:
  - `GET /api/orders/{id}` - Order details
- Health-check endpoint at `/health`
- HttpClient integration for calling Checkout API

### Checkout API
- ASP.NET Core 9 (Web API)
- RESTful API with OpenAPI/Swagger
- Services:
  - `CheckoutService` - Checkout orchestration
  - `MockPaymentGateway` - Payment simulation
- API Endpoints:
  - `POST /api/checkout` - Process checkout
  - `GET /health` - Health check
- Structured logging and error handling
- Docker support with multi-stage builds

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

## 🐳 Running with Docker Compose

The easiest way to run the entire microservices architecture locally is with Docker Compose.

**Prerequisites:**
- Docker Desktop installed and running

**Steps:**

```bash
# Clone the repository
git clone https://github.com/lavann/ads_monotlith_app.git
cd ads_monotlith_app

# Start all services (database, checkout-api, monolith)
docker-compose up --build

# Wait for services to become healthy (check logs)
# Access the application at http://localhost:5000
# Checkout API is available at http://localhost:5001
```

**Services started:**
- **SQL Server** on port 1433
- **Checkout API** on port 5001 (http://localhost:5001/health)
- **Retail Monolith** on port 5000 (http://localhost:5000)

**To stop:**
```bash
docker-compose down

# To also remove volumes (database data)
docker-compose down -v
```

**Viewing logs:**
```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f monolith
docker-compose logs -f checkout-api
```

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

### Local Development (Monolith only)
Start the monolith application:
```bash
dotnet run
```

Access the app at `https://localhost:5001` or `http://localhost:5000`.

**Note:** When running locally without Docker, the Checkout API must be running separately on port 5001, or you can configure the monolith to use a different Checkout API URL in `appsettings.json`.

### Available Endpoints

#### Retail Monolith (Port 5000)
| Path               | Description           |
| ------------------ | --------------------- |
| `/`                | Home Page             |
| `/Products`        | Product catalogue     |
| `/Cart`            | Shopping cart         |
| `/Checkout`        | Checkout page (calls Checkout API) |
| `/Orders`          | Orders list           |
| `/Orders/Details`  | Order details         |
| `/api/orders/{id}` | Order details API     |
| `/health`          | Health check endpoint |

#### Checkout API (Port 5001)
| Path               | Method | Description           |
| ------------------ | ------ | --------------------- |
| `/api/checkout`    | POST   | Process checkout      |
| `/health`          | GET    | Health check endpoint |
| `/openapi/v1.json` | GET    | OpenAPI specification |

For detailed Checkout API documentation, see [CheckoutApi/README.md](CheckoutApi/README.md).

---

## Environment Variables

### Retail Monolith
| Variable                               | Description                | Default          |
| -------------------------------------- | -------------------------- | ---------------- |
| `ConnectionStrings__DefaultConnection` | Database connection string | LocalDB instance |
| `CheckoutApi__BaseUrl`                 | Checkout API base URL      | `http://localhost:5001` |
| `ASPNETCORE_ENVIRONMENT`               | Environment mode           | `Development`    |

### Checkout API
| Variable                               | Description                | Default          |
| -------------------------------------- | -------------------------- | ---------------- |
| `ConnectionStrings__DefaultConnection` | Database connection string | LocalDB instance |
| `ASPNETCORE_ENVIRONMENT`               | Environment mode           | `Development`    |
| `ASPNETCORE_URLS`                      | URLs to listen on          | `http://+:8080`  |

---

## 📚 Microservices Decomposition Journey

This repository demonstrates the **Decomposition Pattern** for breaking down a monolithic application into microservices.

### What Was Extracted?

The **Checkout Service** was extracted from the monolith into a standalone API because:

1. **Clear Bounded Context**: Checkout has well-defined inputs (cart, payment) and outputs (order)
2. **Independent Scaling**: Checkout is a high-traffic operation that benefits from independent scaling
3. **Technology Flexibility**: Checkout logic can evolve independently (e.g., add new payment gateways)
4. **Team Ownership**: Different teams can own cart management vs. checkout processing

### Communication Pattern

**Synchronous REST API**: The monolith calls the Checkout API via HTTP using `HttpClient`.

**Pros:**
- Simple to implement and understand
- Immediate response (no eventual consistency concerns)
- Easy to debug and trace requests

**Cons:**
- Tight temporal coupling (monolith waits for Checkout API)
- Single point of failure if Checkout API is down
- Network latency impacts user experience

**Future Enhancement**: Consider adding a message queue (Azure Service Bus) for asynchronous order processing.

### Database Strategy

**Shared Database** (Transitional Pattern)

Both services currently access the same database. This is a pragmatic first step in decomposition.

**Pros:**
- No data migration required
- Transactions span both services
- Simple to implement initially

**Cons:**
- Services are coupled through shared schema
- Can't independently scale database
- Schema changes affect both services

**Future Enhancement**: Migrate to **Database per Service** pattern with eventual consistency.

### What Remains in the Monolith?

- **Product Catalog**: Managing product inventory and pricing
- **Shopping Cart**: Managing cart state and line items
- **Order Viewing**: Displaying order history and details
- **UI/Frontend**: Razor Pages for the user interface

### Key Learnings

1. **Service Boundaries**: Define clear contracts (DTOs) between services
2. **Error Handling**: Handle network failures gracefully with retries and fallbacks
3. **Observability**: Add health checks, logging, and monitoring
4. **Containerization**: Use Docker for consistent deployment across environments
5. **Testing**: Test inter-service communication thoroughly

### Next Steps

Potential future decompositions:

- **Inventory Service**: Extract stock management and reservation
- **Product Catalog Service**: Extract product search and browsing
- **Order Service**: Extract order management and tracking
- **Notification Service**: Extract email/SMS notifications
- **API Gateway**: Add YARP or Ocelot for unified API entry point

---

## 🚀 Docker Best Practices Applied

This implementation follows Docker and containerization best practices:

### Multi-Stage Builds
- **Build stage**: Uses full SDK image to compile application
- **Runtime stage**: Uses smaller Alpine-based runtime image
- **Result**: Smaller image size and reduced attack surface

### Security
- **Non-root user**: Containers run as `appuser` (not root)
- **Minimal base image**: Alpine Linux reduces vulnerabilities
- **No secrets in images**: Configuration via environment variables

### Health Checks
- All containers have health checks for orchestration
- Docker Compose uses health checks for startup ordering
- Kubernetes-ready (health probes)

### Observability
- Structured logging with `ILogger<T>`
- Health check endpoints for monitoring
- Container logs accessible via `docker-compose logs`

---

## 🧪 Testing the Microservices Architecture

### End-to-End Flow

1. **Start services**: `docker-compose up --build`
2. **Navigate to**: http://localhost:5000
3. **Browse products** and add items to cart
4. **Proceed to checkout** - This triggers a call to the Checkout API
5. **Complete checkout** - Observe order creation
6. **View order details** - See the completed order

### Verify Service Communication

```bash
# Check Checkout API is healthy
curl http://localhost:5001/health

# Check Monolith is healthy  
curl http://localhost:5000/health

# View Checkout API logs
docker-compose logs -f checkout-api

# Trigger checkout via API directly
curl -X POST http://localhost:5001/api/checkout \
  -H "Content-Type: application/json" \
  -d '{"customerId":"guest","paymentToken":"tok_test","cartId":0}'
```

### Load Testing

Use a tool like [k6](https://k6.io/) or [Apache Bench](https://httpd.apache.org/docs/2.4/programs/ab.html) to test:

```bash
# Install k6
# brew install k6  # macOS
# choco install k6 # Windows
# apt install k6   # Linux

# Run load test (requires k6 script)
k6 run loadtest.js
```

---

## 📖 Additional Resources

- [Microservices Architecture Patterns](https://microservices.io/patterns/microservices.html)
- [ASP.NET Core Web API](https://learn.microsoft.com/en-us/aspnet/core/web-api/)
- [Docker Multi-Stage Builds](https://docs.docker.com/build/building/multi-stage/)
- [Docker Compose](https://docs.docker.com/compose/)
- [Decomposition Patterns](https://microservices.io/patterns/decomposition/)
- [Health Checks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)

---

## 🤝 Contributing

This is a learning resource for demonstrating microservices patterns. Feel free to fork and experiment with:

- Different communication patterns (message queues, gRPC)
- Database per service pattern
- API Gateway implementation
- Circuit breakers with Polly
- Distributed tracing with OpenTelemetry
- Event sourcing and CQRS patterns

---

## 📄 License

This project is provided as-is for educational purposes.
