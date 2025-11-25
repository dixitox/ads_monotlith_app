# Database Strategy: Local Development vs Azure Production

## Overview

RetailDecomposed microservices use a **shared database approach** (Phase 1) where all services connect to the same database but access different tables. The database infrastructure differs between local development and Azure production environments.

---

## 🐳 Local Development: SQL Server in Docker

### Configuration

**Database**: SQL Server 2022 Express in Docker container  
**Management**: Orchestrated by `docker-compose.microservices.yml`  
**Port**: 1433 (mapped to host)  
**Persistence**: Docker volume (`sqlserver-data`)

### Connection String

```plaintext
Server=sqlserver;Database=RetailDecomposedDB;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true
```

### Container Configuration

- **Image**: `mcr.microsoft.com/mssql/server:2022-latest`
- **Container Name**: `retaildecomposed-sqlserver`
- **Network**: `retail-network` (custom bridge)
- **Static IP**: 172.28.0.10
- **Health Check**: sqlcmd validation every 10s
- **Restart Policy**: unless-stopped

### Services Configuration

All 5 microservices (Products, Cart, Orders, Checkout, Frontend) are configured in `docker-compose.microservices.yml` with:

```yaml
environment:
  - ConnectionStrings__DefaultConnection=Server=sqlserver;Database=RetailDecomposedDB;...
depends_on:
  sqlserver:
    condition: service_healthy
```

### Benefits

✅ **Isolated environment**: No impact on production  
✅ **Easy reset**: `docker-compose down -v` removes all data  
✅ **Fast startup**: No Azure dependencies  
✅ **Offline development**: Works without internet  
✅ **Cost-free**: No Azure charges during development  
✅ **Matches production schema**: Same database structure

### Local Development Commands

```powershell
# Start all services (including SQL Server container)
docker-compose -f docker-compose.microservices.yml up -d

# View SQL Server logs
docker-compose -f docker-compose.microservices.yml logs sqlserver

# Connect to SQL Server (from host)
sqlcmd -S localhost,1433 -U sa -P "YourStrong!Passw0rd" -C

# Stop and remove everything (including data)
docker-compose -f docker-compose.microservices.yml down -v

# Stop but keep data
docker-compose -f docker-compose.microservices.yml down
```

---

## ☁️ Azure Production: Azure SQL Database (PaaS)

### Configuration

**Database**: Azure SQL Database (Platform-as-a-Service)  
**Management**: Fully managed by Azure  
**No SQL Container**: SQL Server will NOT run in Kubernetes  
**Endpoint**: `<servername>.database.windows.net`

### Azure Resources

**SQL Server**: `sqlserver-retail-decomposed.database.windows.net`  
**Database**: `RetailDecomposedDB`  
**Tier**: Basic (5 DTU) initially, can scale up  
**Cost**: ~£4/month for Basic tier

### Connection String (Production)

**With Managed Identity (Recommended)**:

```plaintext
Server=sqlserver-retail-decomposed.database.windows.net;Database=RetailDecomposedDB;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=true
```

**With SQL Authentication (Alternative)**:

```plaintext
Server=sqlserver-retail-decomposed.database.windows.net;Database=RetailDecomposedDB;User Id=<username>;Password=<password>;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=true
```

### Kubernetes Configuration

All 5 microservice Deployments will have connection strings configured via:

**Option 1: ConfigMap + Secret** (Recommended):

```yaml
# ConfigMap for non-sensitive config
apiVersion: v1
kind: ConfigMap
metadata:
  name: database-config
  namespace: retail-decomposed
data:
  DB_SERVER: "sqlserver-retail-decomposed.database.windows.net"
  DB_NAME: "RetailDecomposedDB"

---
# Secret for sensitive data
apiVersion: v1
kind: Secret
metadata:
  name: database-secret
  namespace: retail-decomposed
type: Opaque
stringData:
  DB_PASSWORD: "<password-from-keyvault>"

---
# Deployment example (Products Service)
apiVersion: apps/v1
kind: Deployment
metadata:
  name: products-service
spec:
  template:
    spec:
      containers:
      - name: products
        env:
        - name: ConnectionStrings__DefaultConnection
          value: "Server=$(DB_SERVER);Database=$(DB_NAME);User Id=sqladmin;Password=$(DB_PASSWORD);Encrypt=True;"
        envFrom:
        - configMapRef:
            name: database-config
        - secretRef:
            name: database-secret
```

**Option 2: Azure Key Vault Integration** (Most Secure):

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: products-service
spec:
  template:
    spec:
      serviceAccountName: workload-identity-sa
      containers:
      - name: products
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretProviderClass:
              name: azure-keyvault-secrets
              key: sql-connection-string
```

### Benefits

✅ **Fully managed**: No patching, backups, or maintenance  
✅ **High availability**: 99.99% SLA with built-in redundancy  
✅ **Automatic backups**: Point-in-time restore up to 35 days  
✅ **Scalability**: Easy to scale up/down based on load  
✅ **Security**: Built-in threat detection and encryption  
✅ **Managed Identity support**: No passwords in code  
✅ **Cost-effective**: Pay for what you use, no VM overhead  
✅ **Geo-replication**: Optional for disaster recovery

### Azure Deployment Steps

1. **Provision Azure SQL Database**:

```powershell
# Create SQL Server
az sql server create \
  --name sqlserver-retail-decomposed \
  --resource-group rg-retail-decomposed-microservices \
  --location uksouth \
  --admin-user sqladmin \
  --admin-password "<StrongPassword>"

# Create Database
az sql db create \
  --resource-group rg-retail-decomposed-microservices \
  --server sqlserver-retail-decomposed \
  --name RetailDecomposedDB \
  --service-objective Basic

# Configure firewall (allow Azure services)
az sql server firewall-rule create \
  --resource-group rg-retail-decomposed-microservices \
  --server sqlserver-retail-decomposed \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

2. **Configure Managed Identity** (Recommended for production):

```powershell
# Enable Managed Identity on AKS
az aks update \
  --resource-group rg-retail-decomposed-microservices \
  --name aks-retail-decomposed \
  --enable-workload-identity

# Grant SQL permissions to Managed Identity
# (Requires Azure AD admin configured on SQL Server)
```

3. **Run Database Migrations**:

```powershell
# Option 1: From local machine (one-time setup)
cd RetailDecomposed
dotnet ef database update --connection "Server=sqlserver-retail-decomposed.database.windows.net;..."

# Option 2: From init container in Kubernetes (automated)
# See frontend-service deployment with initContainer
```

4. **Deploy Microservices**:

```powershell
# Apply Kubernetes manifests (connection string via ConfigMap/Secret)
kubectl apply -f k8s/decomposed-microservices/
```

---

## 🔄 Migration Path: Local → Azure

### Step 1: Test Locally with Docker

```powershell
cd RetailDecomposed
docker-compose -f docker-compose.microservices.yml up -d
# Test all services at localhost:8080-8084
```

### Step 2: Provision Azure SQL Database

```powershell
.\setup-azure-infrastructure-decomposed-microservices.ps1
# Creates Azure SQL Database and other resources
```

### Step 3: Migrate Schema to Azure SQL

```powershell
# Export schema from local SQL Server
docker exec retaildecomposed-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -Q "SELECT * FROM sys.tables" -o schema.sql

# Or use Entity Framework migrations
cd RetailDecomposed
dotnet ef database update --connection "<Azure-SQL-Connection-String>"
```

### Step 4: Update Kubernetes Manifests

Update `k8s/decomposed-microservices/configmap.yaml` with Azure SQL connection details.

### Step 5: Deploy to AKS

```powershell
.\deploy-microservices.ps1
```

---

## 📊 Comparison: Local vs Azure

| Aspect | Local Development (Docker) | Azure Production (PaaS) |
|--------|---------------------------|------------------------|
| **Database** | SQL Server 2022 container | Azure SQL Database |
| **Management** | docker-compose | Azure Portal / CLI |
| **High Availability** | None (single container) | 99.99% SLA |
| **Backups** | Manual (volume snapshots) | Automatic (35 days) |
| **Scalability** | Fixed (container limits) | Dynamic (up to 4TB) |
| **Cost** | Free (local resources) | ~£4/month (Basic tier) |
| **Internet Required** | No | Yes |
| **Security** | Local only | Azure AD, encryption, TDE |
| **Monitoring** | Docker logs | Azure Monitor, Insights |
| **Connection String** | `Server=sqlserver;...` | `Server=*.database.windows.net;...` |
| **Authentication** | SQL (sa) | Managed Identity or SQL |

---

## 🎯 Key Takeaways

1. **Local Development**:
   - Use SQL Server container in docker-compose
   - Fast iteration, no Azure dependencies
   - Perfect for development and testing

2. **Azure Production**:
   - Use Azure SQL Database (PaaS)
   - No SQL Server container in Kubernetes
   - Fully managed, scalable, secure

3. **Same Schema, Different Infrastructure**:
   - All microservices use identical database schema
   - Only connection string changes between environments
   - Migrations run the same in both environments

4. **No SQL Container in AKS**:
   - Kubernetes manifests will NOT include SQL Server deployment
   - All services connect to Azure SQL Database
   - Connection string provided via ConfigMap/Secret/KeyVault

5. **Migration is Seamless**:
   - Develop locally with Docker
   - Deploy to Azure with same code
   - Only configuration changes (connection string)

---

## 📝 Next Steps

- ✅ Local development uses docker-compose with SQL Server container
- ✅ Azure deployment uses Azure SQL Database (no SQL container)
- ⏳ Create Azure infrastructure setup script
- ⏳ Create Kubernetes manifests with Azure SQL configuration
- ⏳ Test migration from local to Azure
