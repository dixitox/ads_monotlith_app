# Quick Start Guide - Running the Applications

This guide shows you how to quickly run both RetailMonolith and RetailDecomposed applications.

## 🚀 Fastest Way to Start

### Recommended: Run in Docker Containers
```powershell
# Run both apps in Docker containers (RECOMMENDED)
.\run-both-apps.ps1 -Mode container
```

**Frontend Applications:**
- **RetailMonolith**: http://localhost:5068
- **RetailDecomposed**: http://localhost:8080

**Backend Microservices (RetailDecomposed):**
- **Products API**: http://localhost:8081 (health: http://localhost:8081/health)
- **Cart API**: http://localhost:8082 (health: http://localhost:8082/health)
- **Orders API**: http://localhost:8083 (health: http://localhost:8083/health)
- **Checkout API**: http://localhost:8084 (health: http://localhost:8084/health)

**Database Ports:**
- **Monolith SQL Server**: localhost:1433
- **Microservices SQL Server**: localhost:1434

---

### Alternative: Run Locally (dotnet run)
```powershell
# Run both apps with dotnet run
.\run-both-apps.ps1 -Mode local
```

**Access URLs:**
- **RetailMonolith**: http://localhost:5068
- **RetailDecomposed**: http://localhost:6068

---

## 📋 Detailed Instructions

### Option 1: Run Locally with .NET

**Prerequisites:**
- .NET 9.0 SDK installed
- SQL Server running (optional, uses LocalDB by default)

**Steps:**
1. Open PowerShell in the repository root
2. Run: `.\run-both-apps.ps1`
3. Wait for applications to start (~10-15 seconds)
4. Open browser to access the apps
5. Press Ctrl+C to stop both applications

**What happens:**
- RetailMonolith runs on port 5068 using its own database
- RetailDecomposed runs on port 6068 as a monolithic app (decomposed architecture but running as single app)

---

### Option 2: Run in Docker Containers

**Prerequisites:**
- Docker Desktop installed and running
- Docker Compose available

**Steps:**
1. Open PowerShell in the repository root
2. Run: `.\run-both-apps.ps1 -Mode container`
3. Wait for containers to build and start (~2-3 minutes first time)
4. Open browser to access the apps
5. Press Ctrl+C to stop all containers

**What happens:**
- **RetailMonolith**: Runs as 2 containers (app + SQL Server on port 1433)
- **RetailDecomposed**: Runs as 6 containers (5 microservices + SQL Server on port 1434)
  - SQL Server (1434)
  - Products Service (8081)
  - Cart Service (8082)
  - Orders Service (8083)
  - Checkout Service (8084)
  - Frontend Service (8080)

---

## 🔍 Monitoring Containers

When running in container mode, use these commands to monitor:

```powershell
# View all running containers
docker ps

# View Monolith logs
docker-compose -f docker-compose.yml logs -f

# View Microservices logs
docker-compose -f RetailDecomposed/docker-compose.microservices.yml logs -f

# View specific service logs
docker logs retaildecomposed-frontend -f
docker logs retaildecomposed-products -f
docker logs retail-monolith-app -f

# Check container health
docker ps --format "table {{.Names}}\t{{.Status}}"
```

---

## 🛑 Stopping Applications

### Local Mode:
- Press **Ctrl+C** in the terminal running the script
- Both applications will stop gracefully

### Container Mode:
- Press **Ctrl+C** in the terminal running the script
- Script will automatically stop all containers
- Or manually: `docker-compose down` in respective directories

---

## 🔧 Manual Container Management

If you prefer to manage containers manually:

### Start Monolith Only:
```powershell
# Start
docker-compose up -d

# Stop
docker-compose down

# View logs
docker-compose logs -f
```

### Start Microservices Only:
```powershell
# Start
cd RetailDecomposed
docker-compose -f docker-compose.microservices.yml up -d

# Stop
docker-compose -f docker-compose.microservices.yml down

# View logs
docker-compose -f docker-compose.microservices.yml logs -f
```

---

## 📊 Application Features

### RetailMonolith (Port 5068 or 5068)
- Traditional monolithic architecture
- Product browsing
- Shopping cart
- Order management
- Single application, single database

### RetailDecomposed (Port 6068 or 8080)
- Decomposed/microservices architecture
- AI-powered product search (Azure AI integration)
- Semantic search capabilities
- Same features as monolith but decomposed
- **Local mode**: Single app calling decomposed services
- **Container mode**: 5 independent microservices

---

## 🧪 Testing

Run all tests (both unit and integration):
```powershell
.\Tests\run-all-tests.ps1
```

This will run:
- 127 unit tests for RetailMonolith
- 127 unit tests for RetailDecomposed
- 11 Docker deployment tests for Monolith
- 32 Docker deployment tests for Microservices

**Total: 295 tests** ✅

---

## ⚠️ Troubleshooting

### Port Already in Use
If you see port conflict errors:
1. Check what's using the port: `netstat -ano | findstr :5068`
2. Kill the process or change the port in `appsettings.json`

### Docker Containers Won't Start
1. Check Docker Desktop is running
2. Ensure no port conflicts: `docker ps`
3. Remove old containers: `docker-compose down -v`
4. Rebuild images: `docker-compose build --no-cache`

### Database Connection Issues (Local)
- RetailMonolith uses LocalDB by default
- RetailDecomposed uses connection string in `appsettings.Development.json`
- Both can use Docker SQL Server on ports 1433 (Monolith) and 1434 (Microservices)

### Database Connection Issues (Container)
- Wait 30-60 seconds for SQL Server containers to be healthy
- Check health: `docker ps` (should show "healthy" status)
- View logs: `docker logs retaildecomposed-sqlserver`

---

## 📚 Related Documentation

- **Tests**: See `Tests/README.md` for testing documentation
- **Deployment**: See `RetailDecomposed/DEPLOYMENT_GUIDE.md` for Azure deployment
- **AI Features**: See `RetailDecomposed/AI_COPILOT_COMPLETE_GUIDE.md`
- **Authentication**: See `RetailDecomposed/AUTHENTICATION_SETUP.md`

---

## 🎯 Quick Access Checklist

After starting the applications, verify access:

**Local Mode (`-Mode local`):**
- [ ] RetailMonolith: http://localhost:5068
- [ ] RetailDecomposed: http://localhost:6068

**Container Mode (`-Mode container`):**
- [ ] RetailMonolith: http://localhost:5068
- [ ] RetailDecomposed Frontend: http://localhost:8080
- [ ] Products API: http://localhost:8081/api/products
- [ ] Cart API: http://localhost:8082/api/cart
- [ ] Orders API: http://localhost:8083/api/orders
- [ ] Checkout API: http://localhost:8084/api/checkout
- [ ] Monolith SQL Server: localhost:1433
- [ ] Microservices SQL Server: localhost:1434

---

**Need Help?** Check the main `README.md` or individual service documentation in `RetailDecomposed/`.
