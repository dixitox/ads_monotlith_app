# RetailDecomposed Containerization Plan - True Microservices Architecture

## 📋 Executive Summary

Deploy **RetailDecomposed** as **true microservices** with multiple independent containers. Instead of a single monolith, split the application into 5+ services, each with its own Dockerfile, container, and deployment. Apply proven patterns from RetailMonolith to each service.

**Target Architecture:**
- 🔷 **Products Service** (separate container)
- 🔷 **Cart Service** (separate container)
- 🔷 **Orders Service** (separate container)
- 🔷 **Checkout Service** (separate container)
- 🔷 **Frontend Service** (Razor Pages UI - separate container)
- ☁️ **AI Services** (Azure OpenAI + Azure AI Search - PaaS, not containerized)
- 🗄️ **Database** (Shared Azure SQL or SQL Server container for local dev)

**Key Changes from Original Plan:**
- ❌ **NOT** single container approach
- ✅ Multiple Dockerfiles (one per service)
- ✅ Inter-service communication via REST APIs
- ✅ Service discovery via Kubernetes DNS
- ✅ Independent scaling and deployment per service
- ✅ Distributed system patterns

**Timeline:** ~8-10 hours (vs 5 hours for monolith)  
**Cost:** £240-300/month (multiple AKS nodes + AI services)  
**Risk Level:** High (distributed system complexity)

---

## 🏗️ Current Architecture Analysis

### Current State (Monolith with Internal APIs)

RetailDecomposed is currently a **single ASP.NET Core application** that exposes multiple API endpoints:

- **Products API**: `/api/products` (GET list, GET by ID)
- **Cart API**: `/api/cart/{customerId}` (GET cart, POST add item)
- **Orders API**: `/api/orders` (GET list, GET by ID)
- **Checkout API**: `/api/checkout` (POST process checkout)
- **AI Copilot API**: `/api/chat` (POST chat with AI)
- **Semantic Search API**: `/api/search` (GET search, POST index)
- **Frontend**: Razor Pages UI

**Current Port**: 6068 (HTTPS), 6067 (HTTP)  
**Current Database**: Shared SQL Server with all entities (Products, Cart, Orders, etc.)  
**Current Authentication**: Azure Entra ID (mandatory for AI services)

### Target Microservices Architecture

Split the monolith into **5 independent services**:

#### 1. Products Service (Port 8081)

**Responsibilities**:

- Product catalog management
- Product search and filtering
- Product details retrieval

**API Endpoints**:

- `GET /api/products` - List all active products
- `GET /api/products/{id}` - Get product by ID
- `GET /api/products/category/{category}` - Filter by category (optional)

**Database Tables**: `Products`, `InventoryItems`  
**Dependencies**: None (most independent service)

#### 2. Cart Service (Port 8082)

**Responsibilities**:

- Shopping cart management
- Add/remove items from cart
- Cart persistence per customer

**API Endpoints**:

- `GET /api/cart/{customerId}` - Get customer's cart
- `POST /api/cart/{customerId}/items` - Add item to cart
- `DELETE /api/cart/{customerId}/items/{productId}` - Remove item (optional)
- `DELETE /api/cart/{customerId}` - Clear cart

**Database Tables**: `Carts`, `CartItems`  
**Dependencies**: Calls **Products Service** to validate product IDs and get prices

#### 3. Orders Service (Port 8083)

**Responsibilities**:

- Order history and retrieval
- Order status management
- Customer order queries

**API Endpoints**:

- `GET /api/orders` - List all orders (filtered by user or admin)
- `GET /api/orders/{id}` - Get order by ID
- `GET /api/orders/customer/{customerId}` - Get orders by customer (optional)

**Database Tables**: `Orders`, `OrderLines`  
**Dependencies**: None (read-only service for existing orders)

#### 4. Checkout Service (Port 8084)

**Responsibilities**:

- Process checkout and payment
- Create orders from cart
- Handle payment gateway integration
- Clear cart after successful checkout

**API Endpoints**:

- `POST /api/checkout` - Process checkout (creates order, processes payment, clears cart)

**Database Tables**: None (orchestrates other services)  
**Dependencies**:

- Calls **Cart Service** to get cart
- Calls **Products Service** to validate inventory
- Calls **Orders Service** to create order
- Calls Payment Gateway (mocked)

#### 5. Frontend Service (Port 8080)

**Responsibilities**:

- Razor Pages UI
- Backend-for-Frontend (BFF) pattern
- User authentication (Azure Entra ID)
- Calls all backend microservices

**Pages**:

- `/` - Home page
- `/Products` - Product catalog
- `/Cart` - Shopping cart
- `/Checkout` - Checkout page
- `/Orders` - Order history
- `/Orders/{id}` - Order details

**Database**: None (calls microservices via HTTP)  
**Dependencies**: Calls all 4 backend services

#### 6. AI Services (Azure PaaS - Not Containerized)

**Services**:

- **Azure OpenAI** - Copilot chat (`/api/chat`)
- **Azure AI Search** - Semantic product search (`/api/search`)

**Integration**: Frontend Service calls Azure services directly (not through dedicated microservice initially)

---

## 🎯 Microservices Design Decisions

### Database Strategy: Shared Database (Phase 1)

**Approach**: All services connect to the **same database** but access different tables.

> 📘 **See [DATABASE_STRATEGY.md](./DATABASE_STRATEGY.md) for complete details on local vs Azure database configuration**

**Local Development**:
- 🐳 SQL Server 2022 in Docker container (managed by docker-compose)
- Port: 1433
- Connection: `Server=sqlserver;Database=RetailDecomposedDB;User Id=sa;Password=YourStrong!Passw0rd`
- Volume: Persistent data storage (`sqlserver-data`)
- **Benefits**: Isolated, fast, offline-capable, cost-free

**Azure Production**:
- ☁️ Azure SQL Database (PaaS) - **NO SQL Server container in Kubernetes**
- Connection: `Server=sqlserver-retail-decomposed.database.windows.net;Database=RetailDecomposedDB`
- Authentication: Managed Identity (recommended) or SQL authentication
- All microservices connect to same Azure SQL Database
- **Benefits**: Fully managed, 99.99% SLA, automatic backups, scalable

**Rationale**:

- ✅ Simpler to implement (existing schema works)
- ✅ No distributed transactions needed
- ✅ Referential integrity maintained
- ✅ Easier migration from monolith
- ✅ Local dev matches production schema
- ⚠️ Tight coupling through database
- ⚠️ Violates "database per service" pattern

**Future Evolution (Phase 2)**: Split into separate databases per service with eventual consistency patterns.

### Inter-Service Communication: REST APIs

**Approach**: Services communicate via HTTP REST APIs using `HttpClient`.

**Service-to-Service Calls**:

- **Cart Service** → **Products Service**: Validate product IDs, get prices
- **Checkout Service** → **Cart Service**: Get cart contents
- **Checkout Service** → **Products Service**: Validate inventory
- **Checkout Service** → **Orders Service**: Create order
- **Frontend Service** → **All Services**: Retrieve data for UI

**Service Discovery**: Use Kubernetes DNS (e.g., `http://products-service:8081`)

**Resilience Patterns** (future):

- Retry policies with exponential backoff
- Circuit breakers (Polly library)
- Timeouts
- Fallback responses

### Authentication Strategy

**Frontend Service**:

- Uses Azure Entra ID with Cookie Authentication
- User logs in at Frontend
- Frontend authenticates user and manages session

**Inter-Service Authentication** (Phase 1 - Simple):

- Services trust requests from within cluster (no auth between services)
- Kubernetes Network Policies restrict traffic to cluster-internal only

**Inter-Service Authentication** (Phase 2 - Secure):

- Use Azure Entra ID with Managed Identity
- Service-to-service OAuth 2.0 tokens
- Frontend acquires token on behalf of user, passes to backend services

### Deployment Strategy

**Kubernetes Resources per Service**:

- **Deployment**: Manages pod replicas
- **Service**: Exposes service within cluster (ClusterIP)
- **Ingress** (Frontend only): Exposes Frontend to internet (HTTPS)

**Scaling Strategy**:

- **Products Service**: Scale horizontally (2-5 replicas) - high read traffic
- **Cart Service**: Scale horizontally (1-3 replicas)
- **Orders Service**: Scale horizontally (1-3 replicas)
- **Checkout Service**: Scale horizontally (1-2 replicas) - lower traffic
- **Frontend Service**: Scale horizontally (2-5 replicas) - user-facing

---

## 📝 Step-by-Step Execution Plan - Microservices

### ✅ Step 1: Analyze Architecture & Define Service Boundaries

**Status**: ✅ **COMPLETED**

**Tasks**:

- [x] Review RetailDecomposed monolithic structure
- [x] Identify API endpoints and natural service boundaries
- [x] Define 5 microservices (Products, Cart, Orders, Checkout, Frontend)
- [x] Map database tables to services
- [x] Document service dependencies and communication patterns
- [x] Decide on shared database vs database-per-service (chose shared for Phase 1)
- [x] Plan authentication strategy (AAD at Frontend, service-to-service trust initially)

**Deliverable**: This plan document with microservices architecture

---

### 🏗️ Step 2: Refactor Code - Extract Microservices

**Status**: ⏳ **IN PROGRESS**

**Goal**: Split the monolithic `Program.cs` and services into separate projects/services.

#### Option A: Separate Projects (Recommended)

Create separate ASP.NET Core projects for each service:

```
RetailDecomposed/
  ├── RetailDecomposed.Products/        (Products Service)
  │   ├── Program.cs
  │   ├── Dockerfile
  │   ├── Controllers/ProductsController.cs
  │   └── RetailDecomposed.Products.csproj
  ├── RetailDecomposed.Cart/             (Cart Service)
  │   ├── Program.cs
  │   ├── Dockerfile
  │   ├── Controllers/CartController.cs
  │   └── RetailDecomposed.Cart.csproj
  ├── RetailDecomposed.Orders/           (Orders Service)
  ├── RetailDecomposed.Checkout/         (Checkout Service)
  ├── RetailDecomposed.Frontend/         (Frontend Service)
  └── RetailDecomposed.Shared/           (Shared models, DTOs)
      ├── Models/Product.cs
      ├── Models/Cart.cs
      └── RetailDecomposed.Shared.csproj
```

#### Option B: Single Project with Multiple Entry Points (Simpler)

Keep existing project but create multiple `Program*.cs` files:

```
RetailDecomposed/
  ├── Program.Products.cs      (Products Service entry point)
  ├── Program.Cart.cs          (Cart Service entry point)
  ├── Program.Orders.cs        (Orders Service entry point)
  ├── Program.Checkout.cs      (Checkout Service entry point)
  ├── Program.Frontend.cs      (Frontend Service entry point)
  ├── Dockerfile.products      (Builds Products)
  ├── Dockerfile.cart          (Builds Cart)
  ├── Dockerfile.orders        (Builds Orders)
  ├── Dockerfile.checkout      (Builds Checkout)
  └── Dockerfile.frontend      (Builds Frontend)
```

**Tasks**:

- [ ] **Decide on refactoring approach** (Option A or B)
- [ ] Extract Products API logic into separate project/file
- [ ] Extract Cart API logic into separate project/file
- [ ] Extract Orders API logic into separate project/file
- [ ] Extract Checkout API logic into separate project/file
- [ ] Create Frontend service with Razor Pages + HTTP clients to call backend services
- [ ] Create shared library for common models (Product, Cart, Order, etc.)
- [ ] Update namespaces and references

**Deliverable**: Separate projects or entry points for each service

---

### 🐳 Step 3: Create Dockerfiles for Each Microservice

**Status**: ⏳ **NOT STARTED**

**Goal**: Create one Dockerfile per service, applying RetailMonolith learnings.

**Tasks**:

- [ ] **Create `RetailDecomposed/Dockerfile.products`**
  - Multi-stage build (SDK → publish → runtime)
  - Run as non-root user (`appuser`)
  - Expose port 8081
  - Copy only Products service files
  - Health check endpoint: `/health`

- [ ] **Create `RetailDecomposed/Dockerfile.cart`**
  - Similar structure to Products
  - Expose port 8082
  - Include HttpClient configuration for Products Service

- [ ] **Create `RetailDecomposed/Dockerfile.orders`**
  - Expose port 8083
  - Read-only service (no external dependencies)

- [ ] **Create `RetailDecomposed/Dockerfile.checkout`**
  - Expose port 8084
  - Include HttpClient configuration for Cart, Products, Orders services

- [ ] **Create `RetailDecomposed/Dockerfile.frontend`**
  - Include Razor Pages static files (`wwwroot`)
  - Expose port 8080 (user-facing)
  - Include HttpClient configuration for all backend services
  - Azure Entra ID authentication

**Learnings Applied from RetailMonolith**:

- ✅ Multi-stage builds for smaller images (~200MB final)
- ✅ Non-root user (`appuser`) for security
- ✅ Non-privileged ports (8080-8084)
- ✅ Health check support
- ✅ Proper file permissions
- ✅ Optimized layer caching (copy `.csproj` first, then source)

**Deliverable**: 5 Dockerfiles (one per service)

---

### 🐳 Step 4: Create docker-compose.yml for All Services

**Status**: ⏳ **NOT STARTED**

**Goal**: Create docker-compose file with all 6 services (5 microservices + SQL Server) for local development.

**Tasks**:

- [ ] Create `docker-compose.microservices.yml` at repository root
- [ ] Define 6 services:
  1. **sqlserver** - SQL Server 2022 (shared database)
  2. **products-service** - Products API (port 8081)
  3. **cart-service** - Cart API (port 8082)
  4. **orders-service** - Orders API (port 8083)
  5. **checkout-service** - Checkout API (port 8084)
  6. **frontend-service** - Frontend UI (port 8080)
- [ ] Configure service dependencies:
  - All services depend on `sqlserver`
  - `cart-service` depends on `products-service`
  - `checkout-service` depends on `cart-service`, `products-service`, `orders-service`
  - `frontend-service` depends on all backend services
- [ ] Configure inter-service networking:
  - Create custom Docker network (`retail-network`)
  - Use service names as hostnames (e.g., `http://products-service:8081`)
- [ ] Add environment variables for each service:
  - Database connection strings
  - Service URLs (e.g., `ProductsServiceUrl=http://products-service:8081`)
  - Azure AD configuration (Frontend only)
  - Azure AI configuration (Frontend only for now)
- [ ] Add health checks for all services:
  - `/health` endpoint for each service
  - Longer timeouts for SQL Server (60s start period)
- [ ] Add volume for SQL Server data persistence

**Example Structure**:

```yaml
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      SA_PASSWORD: "YourStrong@Passw0rd"
      ACCEPT_EULA: "Y"
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql
    healthcheck:
      test: /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -No -Q "SELECT 1"
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 60s
    networks:
      - retail-network

  products-service:
    build:
      context: ./RetailDecomposed
      dockerfile: Dockerfile.products
    ports:
      - "8081:8081"
    environment:
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=RetailDecomposed;User=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"
      ASPNETCORE_URLS: "http://+:8081"
    depends_on:
      sqlserver:
        condition: service_healthy
    healthcheck:
      test: curl -f http://localhost:8081/health || exit 1
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - retail-network

  cart-service:
    build:
      context: ./RetailDecomposed
      dockerfile: Dockerfile.cart
    ports:
      - "8082:8082"
    environment:
      ConnectionStrings__DefaultConnection: "Server=sqlserver;Database=RetailDecomposed;..."
      ASPNETCORE_URLS: "http://+:8082"
      ProductsServiceUrl: "http://products-service:8081"
    depends_on:
      sqlserver:
        condition: service_healthy
      products-service:
        condition: service_healthy
    networks:
      - retail-network

  # ... orders-service, checkout-service, frontend-service ...

networks:
  retail-network:
    driver: bridge

volumes:
  sqldata:
```

**Learnings Applied from RetailMonolith**:

- ✅ Health checks with appropriate timeouts
- ✅ Service dependencies with `depends_on` + `condition: service_healthy`
- ✅ Custom network for service-to-service communication
- ✅ Environment variables for configuration
- ✅ Volume persistence for database

**Deliverable**: `docker-compose.microservices.yml`

---

### 🧪 Step 5: Create Local Testing Scripts for Microservices

**Status**: ⏳ **NOT STARTED**

**Goal**: Create automated tests for multi-service local deployment.

**Tasks**:

- [ ] Create `Tests/test-microservices-deployment.ps1`
- [ ] Test SQL Server connectivity
- [ ] Test each microservice individually:
  - Products Service: GET `/api/products`
  - Cart Service: GET `/api/cart/{customerId}`, POST add item
  - Orders Service: GET `/api/orders`
  - Checkout Service: POST `/api/checkout`
  - Frontend Service: GET `/`, GET `/Products`, GET `/Cart`
- [ ] Test inter-service communication:
  - Cart calls Products to validate product IDs
  - Checkout orchestrates Cart, Products, Orders
  - Frontend calls all backend services
- [ ] Test service discovery (DNS resolution within Docker network)
- [ ] Test health endpoints for all services
- [ ] Verify database migrations ran successfully
- [ ] Test end-to-end flow: View products → Add to cart → Checkout → View order
- [ ] Generate HTML report with test results

**Learnings Applied from RetailMonolith**:

- ✅ Automated test suite (PowerShell script)
- ✅ 30+ tests covering all endpoints
- ✅ HTML report generation
- ✅ Color-coded console output

**Deliverable**: `Tests/test-microservices-deployment.ps1`, HTML test report

---

### 🔍 Step 6: Test Locally with Docker Compose

**Status**: ⏳ **NOT STARTED**

**Tasks**:

- [ ] Start all services: `docker-compose -f docker-compose.microservices.yml up -d --build`
- [ ] Wait for health checks to pass (can take 1-2 minutes)
- [ ] Run test script: `.\Tests\test-microservices-deployment.ps1`
- [ ] Check logs for each service: `docker-compose logs products-service`, etc.
- [ ] Manually test Frontend UI at http://localhost:8080
- [ ] Test inter-service communication using logs
- [ ] Fix any issues (connection strings, service URLs, etc.)
- [ ] Stop services: `docker-compose -f docker-compose.microservices.yml down`

**Success Criteria**:

- ✅ All 6 containers start and become healthy
- ✅ Database migrations run successfully
- ✅ All microservices can connect to database
- ✅ Inter-service HTTP calls succeed (Cart→Products, Checkout→all)
- ✅ Frontend can call all backend services
- ✅ All automated tests pass (30+ tests)
- ✅ End-to-end flow works (browse → cart → checkout → order)
---

### ☁️ Step 7: Azure Infrastructure Setup for Microservices

**Status**: ⏳ **NOT STARTED**

**Goal**: Create Azure resources for all microservices.

**Tasks**:

- [ ] Create `setup-azure-infrastructure-decomposed-microservices.ps1`
- [ ] Create resources:
  - [ ] Resource Group: `rg-retail-decomposed-microservices`
  - [ ] Azure Container Registry: `acrretaildecomposed` (reuse if exists)
  - [ ] Azure Kubernetes Service: `aks-retail-decomposed` with **3-5 nodes** (more services)
  - [ ] Azure SQL Database: `sqldb-retail-decomposed`
  - [ ] Managed Identity: `mi-retail-decomposed`
  - [ ] Azure OpenAI (if not exists): `openai-retail`
  - [ ] Azure AI Search (if not exists): `search-retail`
- [ ] Configure RBAC roles for Managed Identity:
  - [ ] SQL DB Contributor
  - [ ] Cognitive Services OpenAI User (Frontend service needs this)
  - [ ] Search Index Data Contributor (Frontend service needs this)
  - [ ] Search Service Contributor (Frontend service needs this)
  - [ ] ACR Pull (all services)
- [ ] Configure AKS cluster:
  - [ ] Enable workload identity
  - [ ] Attach ACR
  - [ ] Configure network policies (optional - for service isolation)

**Cost Considerations**:

- **AKS**: 3-5 nodes × Standard_B2s = £45-75/month
- **ACR**: Basic tier = £4/month
- **Azure SQL**: Basic tier = £4/month
- **Azure OpenAI**: Pay-per-use = ~£50/month
- **Azure AI Search**: Basic tier = £60/month
- **Total**: ~£240-300/month (vs £185-225 for monolith)

**Deliverable**: `setup-azure-infrastructure-decomposed-microservices.ps1`

---

### 🔐 Step 8: Configure Azure AD Authentication

**Status**: ⏳ **NOT STARTED**

**Goal**: Configure Azure AD for Frontend service only (other services trust internal cluster traffic).

**Tasks**:

- [ ] Create `configure-azure-ad-auth-decomposed.ps1`
- [ ] Create App Registration: `retail-decomposed-frontend`
- [ ] Configure redirect URIs for Frontend service (e.g., `https://retail-decomposed.uksouth.cloudapp.azure.com/signin-oidc`)
- [ ] Set API permissions for Microsoft Graph (if needed)
- [ ] Create Kubernetes secret with AAD config (for Frontend service only)
- [ ] Document inter-service authentication strategy (trust-based initially)

**Note**: Backend services (Products, Cart, Orders, Checkout) do NOT require AAD authentication initially. They trust requests from within the cluster. Can add service-to-service auth in Phase 2.

**Deliverable**: `configure-azure-ad-auth-decomposed.ps1`, Kubernetes secret `azure-ad-secret`

---

### 📦 Step 9: Build and Push All Microservice Images

**Status**: ⏳ **NOT STARTED**

**Goal**: Build and push Docker images for all 5 microservices to ACR.

**Tasks**:

- [ ] Create `build-and-push-microservices.ps1` script
- [ ] Build and tag each service:
  1. `acrretaildecomposed.azurecr.io/products-service:latest` (and `:v1.0.0`)
  2. `acrretaildecomposed.azurecr.io/cart-service:latest` (and `:v1.0.0`)
  3. `acrretaildecomposed.azurecr.io/orders-service:latest` (and `:v1.0.0`)
  4. `acrretaildecomposed.azurecr.io/checkout-service:latest` (and `:v1.0.0`)
  5. `acrretaildecomposed.azurecr.io/frontend-service:latest` (and `:v1.0.0`)
- [ ] Push all images to ACR
- [ ] Verify all images in Azure Portal (5 repositories)
- [ ] Test pulling images from ACR (using AKS service principal)

**Build Strategy**:

- Build locally using Docker BuildKit
- Tag with both `latest` and semantic version (e.g., `v1.0.0`)
- Use ACR Tasks for CI/CD later (optional)

**Deliverable**: `build-and-push-microservices.ps1`, 5 container images in ACR

---

### ☸️ Step 10: Create Kubernetes Manifests for All Services

**Status**: ⏳ **NOT STARTED**

**Goal**: Create K8s manifests (Deployment, Service, Ingress) for each microservice.

**Tasks**:

- [ ] Create directory: `k8s/decomposed-microservices/`
- [ ] Create shared manifests:
  - [ ] `namespace.yaml` - Namespace: `retail-decomposed`
  - [ ] `configmap.yaml` - Shared configuration (database server, service URLs)
  - [ ] `secrets.yaml` - Database password, AAD secrets (Frontend only)

- [ ] **Products Service** (`k8s/decomposed-microservices/products/`):
  - [ ] `deployment-products.yaml` - 2 replicas, image: `acrretaildecomposed.azurecr.io/products-service:latest`
  - [ ] `service-products.yaml` - ClusterIP on port 8081 (internal only)
  - [ ] Environment variables: Database connection string
  - [ ] Health check: `GET /health`
  - [ ] Resource limits: 256Mi memory, 250m CPU

- [ ] **Cart Service** (`k8s/decomposed-microservices/cart/`):
  - [ ] `deployment-cart.yaml` - 2 replicas
  - [ ] `service-cart.yaml` - ClusterIP on port 8082
  - [ ] Environment variables: Database connection, `ProductsServiceUrl=http://products-service:8081`
  - [ ] Depends on Products Service (use init container or readiness probe)

- [ ] **Orders Service** (`k8s/decomposed-microservices/orders/`):
  - [ ] `deployment-orders.yaml` - 2 replicas
  - [ ] `service-orders.yaml` - ClusterIP on port 8083
  - [ ] Environment variables: Database connection

- [ ] **Checkout Service** (`k8s/decomposed-microservices/checkout/`):
  - [ ] `deployment-checkout.yaml` - 1-2 replicas
  - [ ] `service-checkout.yaml` - ClusterIP on port 8084
  - [ ] Environment variables: URLs for Cart, Products, Orders services
  - [ ] Depends on all backend services

- [ ] **Frontend Service** (`k8s/decomposed-microservices/frontend/`):
  - [ ] `deployment-frontend.yaml` - 2 replicas, includes Azure AD config
  - [ ] `service-frontend.yaml` - ClusterIP on port 8080
  - [ ] `ingress-frontend.yaml` - **ONLY** Frontend exposed to internet (HTTPS)
  - [ ] Environment variables: URLs for all backend services, Azure AD config, Azure OpenAI endpoint, Azure AI Search endpoint
  - [ ] Managed Identity binding (for Azure AI services)

**Kubernetes Networking**:

- **ClusterIP Services**: Products, Cart, Orders, Checkout (internal only, not exposed to internet)
- **Ingress**: Only Frontend service exposed via Ingress with HTTPS
- **Service Discovery**: Services call each other using K8s DNS (e.g., `http://products-service:8081`)

**Example Service Manifest** (`service-products.yaml`):

```yaml
apiVersion: v1
kind: Service
metadata:
  name: products-service
  namespace: retail-decomposed
spec:
  selector:
    app: products-service
  ports:
    - protocol: TCP
      port: 8081
      targetPort: 8081
  type: ClusterIP  # Internal only
```

**Deliverable**: 
- `k8s/decomposed-microservices/` directory with manifests for all 5 services
- `namespace.yaml`, `configmap.yaml`, `secrets.yaml`
- Deployment + Service manifests for each microservice
- `ingress-frontend.yaml` (only Frontend exposed)

---

### 🚀 Step 11: Deploy All Microservices to AKS

**Status**: ⏳ **NOT STARTED**

**Goal**: Deploy all 5 microservices to Azure Kubernetes Service in correct order.

**Tasks**:

- [ ] Create `deploy-microservices.ps1` script
- [ ] Deploy in order (respecting dependencies):
  1. Apply namespace: `kubectl apply -f namespace.yaml`
  2. Apply configmap: `kubectl apply -f configmap.yaml`
  3. Apply secrets: `kubectl apply -f secrets.yaml`
  4. Deploy Products Service (no dependencies)
  5. Deploy Cart Service (depends on Products)
  6. Deploy Orders Service (no dependencies)
  7. Deploy Checkout Service (depends on Cart, Products, Orders)
  8. Deploy Frontend Service (depends on all backend services)
  9. Apply Ingress for Frontend
- [ ] Wait for all deployments to become ready
- [ ] Get external IP from Ingress: `kubectl get ingress -n retail-decomposed`
- [ ] Test each service individually (internal endpoints)
- [ ] Test Frontend service (external endpoint)

**Validation Steps**:

1. **Check all pods**: `kubectl get pods -n retail-decomposed`
   - Should see 2x Products, 2x Cart, 2x Orders, 1-2x Checkout, 2x Frontend = ~9-10 pods
2. **Check all services**: `kubectl get svc -n retail-decomposed`
   - Should see 5 ClusterIP services (Products, Cart, Orders, Checkout, Frontend)
3. **Check Ingress**: `kubectl get ingress -n retail-decomposed`
   - Should have external IP assigned
4. **Test inter-service communication**:
   - Check Frontend logs: `kubectl logs -n retail-decomposed -l app=frontend-service`
   - Should see successful HTTP calls to backend services
5. **Test end-to-end**:
   - Browse to `https://<external-ip>`
   - Test: View products → Add to cart → Checkout → View order
6. **Test AI features** (if Azure AI configured):
   - Test Copilot chat
   - Test semantic search

**Learnings Applied from RetailMonolith**:

- ✅ Deploy in order respecting dependencies
- ✅ Wait for rollout completion before testing
- ✅ Automated health checks
- ✅ Retry logic for transient failures
- ✅ Comprehensive logging

**Deliverable**: `deploy-microservices.ps1`, all services running in AKS

---

### 📚 Step 12: Documentation & Testing

**Status**: ⏳ **NOT STARTED**

**Goal**: Update all documentation and create comprehensive testing guide.

**Tasks**:

- [ ] **Update `CONTAINERIZATION_PLAN.md`**:
  - Mark all steps as completed
  - Document any issues encountered and solutions
  - Update cost estimates with actual costs
  - Update timeline with actual time taken

- [ ] **Create `RetailDecomposed/DEPLOYMENT_GUIDE_MICROSERVICES.md`**:
  - Architecture diagram showing all 5 services
  - Local testing with Docker Compose
  - Azure deployment steps (step-by-step)
  - Inter-service communication patterns
  - Troubleshooting guide
  - Cost breakdown per service

- [ ] **Create `Tests/MICROSERVICES_TESTING_GUIDE.md`**:
  - How to test each service individually
  - How to test inter-service communication
  - How to test end-to-end flows
  - How to debug distributed system issues
  - Common issues and solutions

- [ ] **Update `RetailDecomposed/README.md`**:
  - Add microservices architecture section
  - Update running instructions
  - Add links to new documentation

- [ ] **Update `Tests/run-all-tests.ps1`**:
  - Include microservices deployment tests
  - Test each service endpoint
  - Test inter-service calls
  - Generate HTML report

**Comparison Table** (add to docs):

| Aspect | RetailMonolith | RetailDecomposed (Microservices) |
|--------|----------------|----------------------------------|
| Architecture | Single container | 5 independent containers |
| Deployment | 1 K8s deployment | 5 K8s deployments |
| Scaling | Scale entire app | Scale each service independently |
| Database | Single database | Shared database (Phase 1) |
| Complexity | Low | High (distributed system) |
| Development | Simple | More complex (inter-service contracts) |
| Cost | £120/month | £240-300/month |
| Fault Isolation | None (all or nothing) | Per-service (Products down ≠ Orders down) |
| Technology Flexibility | None | Can use different tech per service (future) |

**Deliverable**: 
- Updated `CONTAINERIZATION_PLAN.md`
- `DEPLOYMENT_GUIDE_MICROSERVICES.md`
- `MICROSERVICES_TESTING_GUIDE.md`
- Updated `README.md`
- Updated test scripts
  - [ ] `service.yaml` - ClusterIP service
  - [ ] `ingress.yaml` - Ingress for external access
  - [ ] `configmap.yaml` - Non-sensitive config
  - [ ] `secrets.yaml.template` - Secret template

**Deployment Configuration**:
```yaml
replicas: 2
image: acrretaildecomposed.azurecr.io/retail-decomposed:latest
port: 8080
resources:
  requests:
    memory: "512Mi"
    cpu: "250m"
  limits:
    memory: "1Gi"
    cpu: "500m"
healthCheck:
  livenessProbe: /health
  readinessProbe: /health
```

**Learnings Applied**:
- ✅ Port 8080 (fixed previous port issues)
- ✅ Managed Identity for SQL Server
- ✅ Proper health checks
- ✅ Resource limits
- ✅ 2 replicas for HA

**Deliverable**: `k8s/decomposed/*.yaml` files

---

### 🚀 Step 10: Deploy to AKS
**Tasks**:
- [ ] Modify `deploy-monolith.ps1` → `deploy-decomposed.ps1`
- [ ] Apply namespace
- [ ] Apply configmap
- [ ] Apply secrets
- [ ] Apply deployment
- [ ] Apply service
- [ ] Apply ingress
- [ ] Wait for rollout
- [ ] Get external IP
- [ ] Test deployment

**Validation Steps**:
1. Check pod status: `kubectl get pods -n retail-decomposed`
2. Check logs: `kubectl logs -n retail-decomposed -l app=retail-decomposed`
3. Test health: `curl http://<external-ip>/health`
4. Access application: `https://<external-ip>`

**Learnings Applied**:
- ✅ Wait for rollout completion
- ✅ Automated health checks
- ✅ Error handling and logging
- ✅ Retry logic

**Deliverable**: `deploy-decomposed.ps1`

---

### 📚 Step 11: Update Documentation
**Tasks**:
- [ ] Create `RetailDecomposed/DEPLOYMENT_GUIDE_DOCKER.md`
- [ ] Update `RetailDecomposed/README.md`
- [ ] Create `Tests/DECOMPOSED_TESTING_GUIDE.md`
- [ ] Update main `README.md`
- [ ] Update `Tests/run-all-tests.ps1` to include decomposed tests

**Documentation Sections**:
- Architecture diagram
- Local testing with Docker
- Azure deployment steps
- Troubleshooting guide
- Cost estimates
- Comparison: Monolith vs Decomposed

**Learnings Applied**:
- ✅ Single source of truth (no duplicate docs)
- ✅ Reuse existing documentation patterns
- ✅ Clear step-by-step instructions
- ✅ Real issues documented

**Deliverable**: Complete documentation set

---

## 🎨 Architecture Comparison

### Before: Monolith
```
┌─────────────────────────────────┐
│     RetailMonolith              │
│  ┌──────────────────────────┐   │
│  │   All Services In One    │   │
│  │  - Products              │   │
│  │  - Cart                  │   │
│  │  - Orders                │   │
│  │  - Checkout              │   │
│  └──────────────────────────┘   │
└─────────────────────────────────┘
           ↓
    ┌─────────────┐
    │  SQL Server │
    └─────────────┘
```

### Now: Decomposed (Phase 1 - Single Container)
```
┌─────────────────────────────────┐
│    RetailDecomposed             │
│  ┌──────────────────────────┐   │
│  │   API Clients (Internal) │   │
│  │  - ProductsApiClient     │   │
│  │  - CartApiClient         │   │
│  │  - OrdersApiClient       │   │
│  │  - CheckoutApiClient     │   │
│  └──────────────────────────┘   │
│  ┌──────────────────────────┐   │
│  │   AI Features            │   │
│  │  - Copilot               │   │
│  │  - Semantic Search       │   │
│  └──────────────────────────┘   │
└─────────────────────────────────┘
     ↓                ↓
┌─────────────┐  ┌──────────────┐
│ SQL Server  │  │ Azure AI     │
└─────────────┘  │ - OpenAI     │
                 │ - AI Search  │
                 └──────────────┘
```

### Future: True Microservices (Phase 2)
```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  Products    │  │    Cart      │  │   Orders     │
│   Service    │  │   Service    │  │   Service    │
└──────────────┘  └──────────────┘  └──────────────┘
       ↓                 ↓                  ↓
    ┌─────────────────────────────────────────┐
    │            API Gateway                   │
    └─────────────────────────────────────────┘
                      ↓
            ┌──────────────────┐
            │   Web Frontend   │
            └──────────────────┘
```

---

## 📊 Key Differences: Monolith vs Decomposed

| Aspect | RetailMonolith | RetailDecomposed |
|--------|----------------|------------------|
| **Architecture** | Monolithic | Decomposed (API Clients) |
| **AI Features** | None | Copilot + Semantic Search |
| **Authentication** | Azure AD (optional) | Azure AD (required) |
| **Port** | 5068 → 8080 | 6068 → 8080 |
| **Database** | RetailMonolith DB | RetailDecomposed DB |
| **Azure Services** | ACR + AKS + SQL | ACR + AKS + SQL + AI |
| **Complexity** | Low | Medium |
| **RBAC Roles** | 2 (SQL, ACR) | 5 (SQL, ACR, OpenAI, Search) |

---

## 🔧 Technical Considerations

### Azure AI Services Configuration
**Challenge**: AI features require Azure OpenAI and AI Search

**Solutions**:
1. **Local Testing**: Mock AI responses or skip AI tests
2. **Development**: Use personal Azure subscription
3. **Production**: Use Managed Identity with RBAC

### Database Strategy
**Options**:
1. **Separate Database**: `RetailDecomposed` DB (recommended)
2. **Shared Database**: Same as RetailMonolith (not recommended)

**Decision**: Use separate database for clean separation.

### Authentication Strategy
**Azure Entra ID is mandatory** for RetailDecomposed (AI services require it).

**Configuration**:
- Local: Azure CLI authentication
- Production: Managed Identity

### Inter-Service Communication (Future)
When splitting into true microservices:
- Use HTTP/REST for communication
- Implement circuit breakers (Polly)
- Add service mesh (Istio/Linkerd) for observability
- Use message queue (Azure Service Bus) for async

---

## 💰 Cost Estimates

### Additional Costs vs Monolith
| Resource | Monthly Cost (UK South) |
|----------|-------------------------|
| **Azure OpenAI** | £10-50 (pay-per-use) |
| **Azure AI Search** | £60 (Basic tier) |
| **Total Additional** | **£70-110/month** |

**Monolith Cost**: ~£115/month  
**Decomposed Cost**: ~£185-225/month

---

## ✅ Success Criteria

### Local Testing
- [ ] All containers start successfully
- [ ] Database migrations apply
- [ ] Health checks pass
- [ ] Core features work (Products, Cart, Orders)
- [ ] 9+/10 automated tests pass

### Azure Deployment
- [ ] Pods running and healthy
- [ ] External IP accessible
- [ ] HTTPS working
- [ ] Azure AD authentication working
- [ ] AI features working (Copilot, Search)
- [ ] Database connected
- [ ] Logs showing no errors

---

## 🚧 Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Azure AI costs escalate | High | Set spending limits, monitor usage |
| AI services unavailable | Medium | Graceful degradation, mock responses |
| Complex authentication | Medium | Use proven AAD patterns from docs |
| Port conflicts (6068 vs 5068) | Low | Use different ports, update configs |
| Database migration issues | Medium | Test locally first, backup before deploy |

---

## 📅 Timeline Estimate - Microservices Architecture

| Phase | Duration | Tasks |
|-------|----------|-------|
| **Step 1** | ✅ 30 mins | Architecture Analysis & Service Boundaries (COMPLETED) |
| **Step 2** | 2 hours (A) / 30 mins (B) | Refactor Code - Extract Microservices |
| **Step 3** | 1.5 hours | Create 5 Dockerfiles (one per service) |
| **Step 4** | 1 hour | Create docker-compose with All Services |
| **Step 5** | 1 hour | Create Testing Scripts for Microservices |
| **Step 6** | 1 hour | Test Locally (6 containers: 5 services + DB) |
| **Step 7** | 1 hour | Azure Infrastructure Setup (ACR, AKS, SQL, AI) |
| **Step 8** | 30 mins | Configure Azure AD Authentication |
| **Step 9** | 1 hour | Build and Push 5 Images to ACR |
| **Step 10** | 1.5 hours | Create K8s Manifests (5 deployments + services) |
| **Step 11** | 1 hour | Deploy All Microservices to AKS |
| **Step 12** | 1 hour | Documentation & Final Testing |
| **Total** | **~12 hours (Option A)** or **~10 hours (Option B)** | End-to-end microservices deployment |

**Comparison to Monolith**: ~5 hours (single container) vs ~10-12 hours (5 microservices) = **2-2.5x longer**

---

## 💰 Cost Estimate - Microservices vs Monolith

### Microservices Architecture (5 Services)

| Resource | SKU | Monthly Cost (GBP) | Notes |
|----------|-----|-------------------|-------|
| **AKS Cluster** | 3-5 × Standard_B2s nodes | £45-75 | More nodes for 5 services |
| **Azure Container Registry** | Basic | £4 | 5 container images |
| **Azure SQL Database** | Basic (5 DTU) | £4 | Shared by all services |
| **Azure OpenAI** | Pay-per-use | £50 | Copilot + Embeddings |
| **Azure AI Search** | Basic | £60 | Semantic search |
| **Azure AD** | Free tier | £0 | Frontend authentication |
| **Load Balancer / Ingress** | Standard | £15 | Public IP + traffic |
| **Managed Identity** | Free | £0 | Service authentication |
| **Key Vault** (optional) | Standard | £3 | Secrets management |
| **Total** | | **£240-300/month** | Microservices |

### Monolith Architecture (1 Service - For Comparison)

| Resource | Monthly Cost (GBP) |
|----------|-------------------|
| **AKS Cluster** (1-2 nodes) | £20-40 |
| **Other services** (same) | £140 |
| **Total** | **£185-225/month** |

**Cost Difference**: Microservices = **£50-75/month more** (+30-40% increase)

**Why More Expensive?**

- More AKS nodes needed (5 services vs 1)
- More network traffic between services
- More load balancer rules (if needed)

**Cost Optimization Tips**:

- Use Spot VMs for non-production (save 70-90%)
- Scale down non-critical services (Cart, Orders) to 1 replica
- Use Horizontal Pod Autoscaler (HPA) to scale based on load
- Consider Azure Container Apps instead of AKS (cheaper for small workloads)

---

## 🎓 Lessons from RetailMonolith

### ✅ What Worked Well
1. **Port 8080**: Non-privileged port avoided issues
2. **Multi-stage builds**: Smaller images, faster deploys
3. **Non-root user**: Better security posture
4. **Health checks**: Proper timing (60s start_period)
5. **Automated testing**: Caught issues early
6. **Managed Identity**: No secrets to manage
7. **Documentation**: Single source of truth

### ❌ Issues Encountered (Now Fixed)
1. ~~CrashLoopBackOff due to port 80~~ → Use port 8080
2. ~~Ingress 404 errors~~ → Correct service/pod configuration
3. ~~SQL password prompts~~ → Use Managed Identity
4. ~~Old sqlcmd path~~ → Use `/opt/mssql-tools18/bin/sqlcmd`

### 🎯 Apply to Decomposed
- Use all proven patterns from monolith
- Add AI-specific RBAC roles
- Handle AI service failures gracefully
- Test locally before Azure deployment

---

## 🎯 Next Steps & Decision Point

### Plan Status

**We have completed:**

- ✅ **Step 1**: Architecture analysis and microservices boundary definition
- ✅ **Plan Updated**: Revised for TRUE microservices (5 containers, not 1)

**Ready to proceed with:**

- ⏳ **Step 2**: Refactor code - Extract microservices into separate projects or entry points

---

### 🤔 Decision Required: Code Refactoring Approach

**Option A: Separate Projects (Recommended for True Microservices)**

Create separate ASP.NET Core projects:

```
RetailDecomposed/
  ├── RetailDecomposed.Products/        ← New project
  ├── RetailDecomposed.Cart/            ← New project
  ├── RetailDecomposed.Orders/          ← New project
  ├── RetailDecomposed.Checkout/        ← New project
  ├── RetailDecomposed.Frontend/        ← New project
  └── RetailDecomposed.Shared/          ← Shared models
```

**Pros**:

- ✅ Clean separation of concerns
- ✅ Independent versioning and deployment
- ✅ Can use different tech stacks per service later
- ✅ Clearer ownership boundaries
- ✅ Industry standard microservices approach

**Cons**:

- ⚠️ More initial setup (5 `.csproj` files + shared library)
- ⚠️ More complexity managing multiple projects
- ⚠️ Longer initial setup time (~2 hours)

---

**Option B: Single Project with Multiple Entry Points (Simpler)**

Keep existing project, create multiple `Program*.cs` files:

```
RetailDecomposed/
  ├── Program.Products.cs      ← Products entry point
  ├── Program.Cart.cs          ← Cart entry point
  ├── Program.Orders.cs        ← Orders entry point
  ├── Program.Checkout.cs      ← Checkout entry point
  ├── Program.Frontend.cs      ← Frontend entry point
  └── RetailDecomposed.csproj  ← Single project
```

**Pros**:

- ✅ Faster initial setup (~30 minutes)
- ✅ Easier to share code between services
- ✅ Single `.csproj` to manage

**Cons**:

- ⚠️ Less clean separation (all services in one project)
- ⚠️ Harder to version services independently
- ⚠️ All services must use same .NET version
- ⚠️ Not true microservices pattern

---

### 💡 My Recommendation

**Choose Option A (Separate Projects)** if:

- You want true microservices architecture
- You plan to scale/deploy services independently
- You want different teams to own different services
- You want to follow industry best practices

**Choose Option B (Single Project)** if:

- You want to containerize quickly
- You're okay with tighter coupling
- You want simpler project structure
- You may revert to monolith later

**I recommend Option A** - More work now, but proper microservices foundation.

---

### 🚀 What to Do Next

**Reply with your choice:**

- Type **`A`** or **`Option A`** → Create separate projects (true microservices)
- Type **`B`** or **`Option B`** → Single project with multiple entry points
- Type **`Review`** → Pause and review the plan first

Once you decide, I'll proceed with Step 2 (code refactoring) immediately.

---

**Plan Status**: ✅ **UPDATED FOR TRUE MICROSERVICES - AWAITING YOUR DECISION**
