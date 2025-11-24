# Local Testing with Docker Compose

## Overview
This guide helps you test RetailMonolith locally using Docker Compose before deploying to Azure.

## Prerequisites
- Docker Desktop installed and running
- .NET 9.0 SDK (for running tests)
- At least 4GB RAM available for Docker

## Quick Start

### 1. Start the Application
```powershell
# Build and start all services
docker-compose up --build

# Or run in detached mode
docker-compose up -d --build
```

**What this does:**
- Starts SQL Server 2022 in a container
- Builds the RetailMonolith Docker image
- Runs the application on http://localhost:5068
- Applies database migrations automatically
- Seeds sample data (50 products)

### 2. Access the Application
Open your browser to:
- **Application**: http://localhost:5068
- **Health Check**: http://localhost:5068/health

### 3. View Logs
```powershell
# View all logs
docker-compose logs -f

# View app logs only
docker-compose logs -f retail-monolith

# View SQL Server logs only
docker-compose logs -f sqlserver
```

### 4. Stop the Application
```powershell
# Stop and remove containers (keeps data)
docker-compose down

# Stop and remove containers + volumes (deletes database)
docker-compose down -v
```

## Testing Scenarios

### Test 1: Basic Functionality
1. Navigate to http://localhost:5068
2. Click "Products" - should see 50 products
3. Click "Add to Cart" on any product
4. Go to "Cart" - should see item in cart
5. Click "Checkout"
6. Fill in checkout form and submit
7. Go to "Orders" - should see your order
8. Click on order to see details

### Test 2: Health Check
```powershell
# Test health endpoint
curl http://localhost:5068/health
```

Expected response:
```
Healthy
```

### Test 3: Database Connection
```powershell
# Connect to SQL Server
docker exec -it retail-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd"

# In sqlcmd, run:
USE RetailMonolith;
GO
SELECT COUNT(*) FROM Products;
GO
SELECT COUNT(*) FROM Orders;
GO
```

### Test 4: Container Restart (Data Persistence)
```powershell
# Stop containers
docker-compose down

# Start again (data should persist)
docker-compose up -d

# Check if data is still there
curl http://localhost:5068/Products
```

### Test 5: Container Logs
```powershell
# Check for errors in application logs
docker-compose logs retail-monolith | Select-String -Pattern "error|exception|fail" -CaseSensitive:$false

# Check SQL Server logs
docker-compose logs sqlserver | Select-String -Pattern "error|fail" -CaseSensitive:$false
```

## Running Automated Tests

### Unit Tests
```powershell
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test project
dotnet test Tests/RetailMonolith.Tests/RetailMonolith.Tests.csproj
```

### Integration Tests (requires running containers)
```powershell
# Start the application
docker-compose up -d

# Wait for services to be healthy
Start-Sleep -Seconds 10

# Run integration tests
dotnet test Tests/RetailMonolith.Tests/ --filter "Category=Integration"

# Stop containers
docker-compose down
```

## Troubleshooting

### Issue: SQL Server Won't Start
**Symptom**: Container exits immediately or health check fails

**Solution**:
```powershell
# Check logs
docker-compose logs sqlserver

# Common fix: Remove old volume and restart
docker-compose down -v
docker-compose up -d sqlserver

# Wait and check health
docker-compose ps
```

### Issue: Application Can't Connect to Database
**Symptom**: Application logs show connection errors

**Solution**:
```powershell
# Ensure SQL Server is healthy
docker-compose ps

# Should show:
# NAME                 STATUS
# retail-sqlserver     Up (healthy)
# retail-monolith-app  Up (healthy)

# If SQL Server is not healthy, restart it
docker-compose restart sqlserver
```

### Issue: Port Already in Use
**Symptom**: "Bind for 0.0.0.0:5068 failed: port is already allocated"

**Solution**:
```powershell
# Find process using the port
netstat -ano | findstr :5068

# Kill the process (replace PID)
taskkill /PID <PID> /F

# Or change the port in docker-compose.yml:
# ports:
#   - "5069:8080"  # Changed from 5068 to 5069
```

### Issue: Out of Memory
**Symptom**: Containers crash or Docker becomes unresponsive

**Solution**:
1. Open Docker Desktop
2. Go to Settings → Resources
3. Increase Memory to at least 4GB
4. Click "Apply & Restart"

## Development Workflow

### Make Code Changes and Test
```powershell
# 1. Make your code changes

# 2. Rebuild and restart
docker-compose up -d --build

# 3. View logs to check for errors
docker-compose logs -f retail-monolith

# 4. Test the changes in browser
# Navigate to http://localhost:5068
```

### Reset Database
```powershell
# Stop everything and delete volumes
docker-compose down -v

# Start fresh
docker-compose up -d
```

### Connect with SQL Client (Optional)
You can connect with any SQL client tool:
- **Server**: localhost,1433
- **User**: sa
- **Password**: YourStrong!Passw0rd
- **Database**: RetailMonolith

## Comparing Local vs AKS

| Aspect | Local (Docker Compose) | AKS Deployment |
|--------|------------------------|----------------|
| Database | SQL Server container | Azure SQL Database |
| Authentication | SQL username/password | Azure AD Managed Identity |
| Port | 5068 (HTTP) | 443 (HTTPS) |
| SSL/TLS | No encryption | Full TLS encryption |
| Replicas | 1 | 2+ with auto-scaling |
| Load Balancer | None | Azure Load Balancer |
| DNS | localhost | Public IP or domain |
| Cost | Free (local resources) | ~£115/month |

## Performance Testing (Optional)

### Load Test with curl
```powershell
# Test home page (100 requests)
1..100 | ForEach-Object {
    $response = Invoke-WebRequest -Uri "http://localhost:5068" -TimeoutSec 5
    Write-Host "Request $_: $($response.StatusCode)"
}
```

### Load Test with Apache Bench (if installed)
```bash
# 1000 requests, 10 concurrent
ab -n 1000 -c 10 http://localhost:5068/
```

## Clean Up

### Remove Everything
```powershell
# Stop containers and remove volumes
docker-compose down -v

# Remove images (optional)
docker rmi retail-monolith-app
docker rmi mcr.microsoft.com/mssql/server:2022-latest
```

### Check Disk Space
```powershell
# See Docker disk usage
docker system df

# Clean up unused resources
docker system prune -a --volumes
```

## Next Steps

Once local testing is complete:
1. ✅ Verify all features work
2. ✅ Check logs for errors
3. ✅ Run automated tests
4. ✅ Ready to deploy to AKS!

See [MONOLITH_DEPLOYMENT_GUIDE.md](MONOLITH_DEPLOYMENT_GUIDE.md) for Azure deployment.

---

**Quick Test Checklist:**
- [ ] Docker Compose starts successfully
- [ ] SQL Server is healthy
- [ ] Application is healthy (http://localhost:5068/health)
- [ ] Home page loads
- [ ] Products page shows items
- [ ] Can add items to cart
- [ ] Can complete checkout
- [ ] Orders page shows order history
- [ ] No errors in logs
- [ ] Database persists after restart
