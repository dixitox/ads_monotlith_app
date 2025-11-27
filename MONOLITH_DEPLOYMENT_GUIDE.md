# RetailMonolith - Complete AKS Deployment Guide

## Overview
This is the **complete, consolidated guide** for deploying RetailMonolith to Azure Kubernetes Service (AKS) with Azure AD authentication, HTTPS security, and all lessons learned from the deployment process.

> **Note**: For deploying the modernized RetailDecomposed application, see [RetailDecomposed/DEPLOYMENT_GUIDE.md](RetailDecomposed/DEPLOYMENT_GUIDE.md)

## What You'll Deploy

### Architecture
```
Internet (HTTPS Only)
   ↓
[NGINX Ingress Controller]
   │ - TLS Termination
   │ - SSL Redirect (HTTP→HTTPS)
   │ - IP: 145.133.57.234
   ↓
[ClusterIP Service]
   │ - Port 80 → 8080
   ↓
┌─────────────────────────────────────────┐
│         AKS Cluster (UK South)          │
│                                         │
│  ┌───────────────────────────────────┐  │
│  │   RetailMonolith Pod #1           │  │
│  │   - Port: 8080 (non-privileged)   │  │
│  │   - Non-root user: appuser        │  │
│  │   - Workload Identity enabled     │  │
│  └───────────────────────────────────┘  │
│                                         │
│  ┌───────────────────────────────────┐  │
│  │   RetailMonolith Pod #2           │  │
│  │   - Port: 8080 (non-privileged)   │  │
│  │   - Non-root user: appuser        │  │
│  │   - Workload Identity enabled     │  │
│  └───────────────────────────────────┘  │
│                                         │
│  ┌───────────────────────────────────┐  │
│  │   Service Account                 │  │
│  │   retail-monolith-sa              │  │
│  │   (Federated Credential)          │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
               ↓
    ┌──────────────────────────────────┐
    │  User-Assigned Managed Identity  │
    │  mi-retail-monolith              │
    └──────────────────────────────────┘
               ↓
    ┌──────────────────────────────────┐
    │  Azure SQL Server (AD-Only Auth) │
    │  sql-retail-monolith             │
    │  ├─ RetailMonolithDB             │
    │  └─ Azure AD Admin (your user)   │
    └──────────────────────────────────┘
```

### Key Features
- ✅ **Azure AD Authentication** - No SQL passwords, Managed Identity only
- ✅ **HTTPS-Only Access** - Automatic HTTP→HTTPS redirect
- ✅ **Non-Root Containers** - Port 8080 (non-privileged)
- ✅ **Workload Identity** - Secure Azure AD integration
- ✅ **UK South Region** - All resources in uksouth
- ✅ **High Availability** - 2 replicas with health checks
- ✅ **Auto-Scaling Ready** - Resource limits configured

## Prerequisites

### Required Tools
- **Azure CLI**: https://aka.ms/installazurecli
- **kubectl**: https://kubernetes.io/docs/tasks/tools/
- **Docker Desktop**: https://www.docker.com/products/docker-desktop
- **.NET 9.0 SDK**: https://dotnet.microsoft.com/download
- **PowerShell 7+**: https://aka.ms/powershell

### Azure Subscription
- Active Azure subscription
- Sufficient permissions to create resources

## Step-by-Step Deployment

### Step 1: Azure Infrastructure Setup

Run the infrastructure setup script:

```powershell
.\setup-azure-infrastructure-monolith.ps1
```

This script will:
- ✅ Create Resource Group
- ✅ Create Azure Container Registry (ACR)
- ✅ Create AKS Cluster (2 nodes)
- ✅ Create Azure SQL Server
- ✅ Create RetailMonolithDB database
- ✅ Configure firewall rules
- ✅ Connect kubectl to AKS

**Expected Duration**: 10-15 minutes (AKS creation is the longest step)

**Default Resources Created**:
- Resource Group: `rg-retail-monolith`
- ACR: `acrretailmonolith.azurecr.io`
- AKS: `aks-retail-monolith`
- SQL Server: `sql-retail-monolith.database.windows.net`
- Database: `RetailMonolithDB`


### Step 2: Configure Azure AD Authentication

#### 2.1 Configure Managed Identity and Azure AD Access

Run the Azure AD authentication configuration script:

```powershell
.\configure-azure-ad-auth.ps1
```

This script will:
- ✅ Enable Workload Identity on AKS
- ✅ Create User-Assigned Managed Identity
- ✅ Create federated identity credential
- ✅ Update service account with identity details
- ✅ Update ConfigMap with SQL Server name
- ✅ Generate SQL commands to grant database access

**Expected Duration**: 2-3 minutes

**Follow the prompts to grant SQL permissions.** You can use:
- Azure Portal Query Editor (easiest)
- sqlcmd with Azure AD authentication
- SQL Server Management Studio (SSMS)

#### 2.2 Create Secrets File

1. Copy the template:
```powershell
Copy-Item k8s/monolith/secrets-template.yaml k8s/monolith/secrets.yaml
```

2. Get the connection string (Azure AD authentication):

**PowerShell**:
```powershell
# Connection string for Azure AD authentication
$connString = "Server=tcp:sql-retail-monolith.database.windows.net,1433;Database=RetailMonolithDB;Authentication=Active Directory Default;Encrypt=True;"
[Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($connString))
```

3. Edit `k8s/monolith/secrets.yaml` and set the `ConnectionStrings__DefaultConnection` value

**✅ Note**: With Azure AD authentication, no SQL passwords are needed! The connection uses Managed Identity.

**⚠️ IMPORTANT**: Never commit `secrets.yaml` to Git! It's in `.gitignore`.

### Step 3: Build and Push Docker Image

Build the Docker image and push to ACR:

```powershell
.\build-and-push-monolith.ps1 -AcrName "acrretailmonolith"
```

This will:
- ✅ Build multi-stage Docker image
- ✅ Tag with version and `latest`
- ✅ Push to Azure Container Registry
- ✅ Use port 8080 (non-privileged port for non-root user)

**Expected Duration**: 3-5 minutes (first build is slower)

**Note**: The container runs on port 8080 (not port 80) because we use a non-root user (`appuser`) for security. Ports below 1024 require root privileges.

### Step 4: Deploy to AKS

Deploy the application to Kubernetes:

```powershell
.\deploy-monolith.ps1 -WaitForReady
```

This will:
- ✅ Install NGINX Ingress Controller (if needed)
- ✅ Create namespace
- ✅ Apply ConfigMap
- ✅ Apply Secrets
- ✅ Deploy application (2 replicas)
- ✅ Create Service
- ✅ Create Ingress
- ✅ Wait for pods to be ready

**Expected Duration**: 3-5 minutes

### Step 5: Verify Deployment

#### Check Pods
```powershell
kubectl get pods -n retail-monolith
```

Expected output:
```
NAME                               READY   STATUS    RESTARTS   AGE
retail-monolith-xxxxxxxxx-xxxxx    1/1     Running   0          2m
retail-monolith-xxxxxxxxx-xxxxx    1/1     Running   0          2m
```

#### Check Service
```powershell
kubectl get svc -n retail-monolith
```

#### Check Ingress
```powershell
kubectl get ingress -n retail-monolith
```

Look for the external IP address.

#### View Logs
```powershell
kubectl logs -n retail-monolith -l app=retail-monolith --tail=50 -f
```

### Step 6: Access the Application (HTTPS)

Once the ingress has an external IP, access your application:

```
https://<EXTERNAL-IP>
```

**HTTPS is enforced:**
- HTTP requests automatically redirect to HTTPS (308 Permanent Redirect)
- TLS certificate is self-signed (browser will show security warning)
- Click "Advanced" and "Proceed" to access the site

**Current URL**: https://145.133.57.234

The deployment script will automatically open it in your browser.

**For production**: Replace self-signed certificate with Let's Encrypt (see Production section below).

## Database Migrations

The application automatically runs migrations on startup. Check logs:

```powershell
kubectl logs -n retail-monolith -l app=retail-monolith --tail=100 | Select-String -Pattern "migration"
```

## Troubleshooting

### Pods Not Starting

**Check pod status:**
```powershell
kubectl describe pod -n retail-monolith <pod-name>
```

**Common issues:**
- Image pull errors → Check ACR authentication
- CrashLoopBackOff → Check application logs
- Pending → Check resource availability

### Database Connection Errors

**Check connection string:**
```powershell
kubectl get secret -n retail-monolith retail-monolith-secrets -o jsonpath="{.data.ConnectionStrings__DefaultConnection}" | ForEach-Object { [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($_)) }
```

**Verify Azure AD authentication:**
1. Check that Managed Identity has SQL permissions:
```powershell
# Check if identity exists
az identity show --resource-group rg-retail-monolith --name mi-retail-monolith
```

2. Verify federated credential:
```powershell
az identity federated-credential list --resource-group rg-retail-monolith --identity-name mi-retail-monolith
```

3. Check service account annotation:
```powershell
kubectl describe sa retail-monolith-sa -n retail-monolith
```

**Verify SQL permissions:**
Run this SQL query in Azure Portal Query Editor:
```sql
-- Check if Managed Identity user exists
SELECT name, type_desc FROM sys.database_principals WHERE name = 'mi-retail-monolith';

-- Check roles
SELECT r.name AS RoleName, m.name AS MemberName
FROM sys.database_role_members rm
JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
WHERE m.name = 'mi-retail-monolith';
```

**Verify SQL Server firewall:**
1. Go to Azure Portal
2. Navigate to your SQL Server
3. Check "Firewalls and virtual networks"
4. Ensure "Allow Azure services" is enabled

### Application Not Accessible

**Check ingress:**
```powershell
kubectl describe ingress -n retail-monolith retail-monolith-ingress
```

**Check NGINX Ingress Controller:**
```powershell
kubectl get pods -n ingress-nginx
```

**Check service:**
```powershell
kubectl get svc -n retail-monolith
```

### View All Logs

**Stream logs from all pods:**
```powershell
kubectl logs -n retail-monolith -l app=retail-monolith --all-containers --tail=100 -f
```

## Scaling

### Scale Deployment

**Scale to 3 replicas:**
```powershell
kubectl scale deployment retail-monolith -n retail-monolith --replicas=3
```

**Auto-scaling:**
```powershell
kubectl autoscale deployment retail-monolith -n retail-monolith --min=2 --max=10 --cpu-percent=70
```

## Updating the Application

### Update Code and Redeploy

1. Make code changes
2. Build and push new image:
```powershell
.\build-and-push-monolith.ps1 -AcrName "acrretailmonolith"
```

3. Restart deployment:
```powershell
kubectl rollout restart deployment retail-monolith -n retail-monolith
```

4. Watch rollout:
```powershell
kubectl rollout status deployment retail-monolith -n retail-monolith
```

### Rollback

If something goes wrong:
```powershell
kubectl rollout undo deployment retail-monolith -n retail-monolith
```

## Monitoring

### Resource Usage
```powershell
kubectl top pods -n retail-monolith
kubectl top nodes
```

### Events
```powershell
kubectl get events -n retail-monolith --sort-by='.lastTimestamp'
```

### Application Insights

If configured, view telemetry in Azure Portal:
1. Navigate to your Application Insights resource
2. View Live Metrics, Failures, Performance

## Clean Up

### Delete Application Only
```powershell
kubectl delete namespace retail-monolith
```

### Delete All Azure Resources
```powershell
az group delete --name rg-retail-monolith --yes --no-wait
```

**⚠️ WARNING**: This deletes everything including databases!

## Configuration Reference

### Environment Variables

Set in `k8s/monolith/configmap.yaml`:

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment | `Production` |
| `ASPNETCORE_URLS` | URLs to listen on | `http://+:8080` |
| `SQL_SERVER` | SQL Server hostname | (your server) |
| `SQL_DATABASE` | Database name | `RetailMonolithDB` |

### Secrets

Set in `k8s/monolith/secrets.yaml`:

| Secret | Description |
|--------|-------------|
| `SQL_ADMIN_USER` | SQL Server admin username |
| `SQL_ADMIN_PASSWORD` | SQL Server admin password |
| `ConnectionStrings__DefaultConnection` | Full SQL connection string |

### Resource Limits

Default per pod:
- CPU Request: 250m (0.25 cores)
- CPU Limit: 500m (0.5 cores)
- Memory Request: 256Mi
- Memory Limit: 512Mi

Adjust in `k8s/monolith/deployment.yaml`.

## Security Best Practices

✅ **Implemented:**
- Non-root container user
- Resource limits
- Health checks
- Read-only root filesystem (where possible)
- Network policies (via AKS)
- TLS encryption for SQL connections

🔧 **Recommended for Production:**
- Enable HTTPS with valid SSL certificate
- Use Azure Key Vault for secrets
- Enable Azure AD Pod Identity
- Implement network policies
- Enable container image scanning
- Configure Azure Firewall
- Enable Azure DDoS Protection

## Cost Optimization

**Current setup costs (approximate monthly):**
- AKS (2 B2s nodes): ~$60
- SQL Database (S1): ~$30
- ACR (Basic): ~$5
- Load Balancer: ~$20
- **Total**: ~$115/month

**To reduce costs:**
- Use dev/test tier for SQL Database
- Scale down to 1 node during off-hours
- Use Azure Reservations for long-term commitments
- Consider spot instances for non-production

## Production-Ready Enhancements

### Replace Self-Signed Certificate with Let's Encrypt

**Current state**: Self-signed certificate causes browser warnings.

**Production solution**: Use cert-manager with Let's Encrypt for trusted certificates.

```powershell
# Install cert-manager
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml

# Wait for cert-manager pods
kubectl wait --for=condition=Ready pods --all -n cert-manager --timeout=300s
```

Create `k8s/monolith/letsencrypt-issuer.yaml`:
```yaml
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: your-email@example.com  # Change this!
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: nginx
```

Update ingress to use Let's Encrypt:
```yaml
metadata:
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  tls:
  - hosts:
    - retail-monolith.yourdomain.com
    secretName: retail-monolith-tls-prod  # cert-manager will create this
```

### Configure Custom Domain

1. **Register domain** or use existing
2. **Create DNS A record** pointing to `145.133.57.234`
3. **Update ingress** with your domain name
4. **Apply Let's Encrypt** certificate (above)

### Enable Application Insights

Already configured in code, just need connection string:

```powershell
# Create Application Insights
az monitor app-insights component create \
  --app retail-monolith-insights \
  --location uksouth \
  --resource-group rg-retail-monolith \
  --application-type web

# Get connection string
az monitor app-insights component show \
  --app retail-monolith-insights \
  --resource-group rg-retail-monolith \
  --query connectionString -o tsv
```

Add to `k8s/monolith/configmap.yaml`:
```yaml
ApplicationInsights__ConnectionString: "<your-connection-string>"
```

## Common Issues and Lessons Learned

### 1. CrashLoopBackOff: Permission Denied (Port 80)
**Problem**: Container couldn't bind to port 80 as non-root user.

**Solution**: Changed to port 8080 throughout:
- Dockerfile: `ENV ASPNETCORE_URLS=http://+:8080`
- Deployment: `containerPort: 8080`
- Service: `targetPort: 8080`
- ConfigMap: `ASPNETCORE_URLS="http://+:8080"`

### 2. Ingress 404 with IP Access
**Problem**: Ingress only worked with hostname, not IP address.

**Solution**: Added catch-all rule (no host specified) in ingress for IP-based access.

### 3. SQL Password Prompt with Azure AD
**Problem**: Script prompted for SQL password despite Azure AD-only auth.

**Solution**: Removed SQL password logic from setup script. Azure AD authentication doesn't need passwords!

### 4. HTTPS Browser Warning
**Problem**: Self-signed certificate causes security warnings.

**Solution**: 
- **Development**: Click "Advanced" → "Proceed to site"
- **Production**: Use Let's Encrypt (see above)

## Quick Command Reference

```powershell
# View all resources
kubectl get all,ingress -n retail-monolith

# Stream logs
kubectl logs -n retail-monolith -l app=retail-monolith -f --tail=100

# Restart deployment
kubectl rollout restart deployment retail-monolith -n retail-monolith

# Scale replicas
kubectl scale deployment retail-monolith -n retail-monolith --replicas=3

# Check resource usage
kubectl top pods -n retail-monolith

# Get external IP
kubectl get ingress -n retail-monolith -o jsonpath='{.items[0].status.loadBalancer.ingress[0].ip}'

# Check TLS certificate
kubectl get secret retail-monolith-tls -n retail-monolith

# Verify Managed Identity
az identity show --resource-group rg-retail-monolith --name mi-retail-monolith

# Check SQL permissions
kubectl exec -n retail-monolith -it <pod-name> -- /bin/bash
# Then test connection from within pod
```

## Deployment Summary

### What Was Created
| Resource | Name | Purpose |
|----------|------|---------|
| Resource Group | rg-retail-monolith | Container for all resources |
| ACR | acrretailmonolith | Docker image registry |
| AKS Cluster | aks-retail-monolith | Kubernetes hosting (2 nodes, UK South) |
| SQL Server | sql-retail-monolith | Database server (Azure AD only) |
| SQL Database | RetailMonolithDB | Application database |
| Managed Identity | mi-retail-monolith | For SQL authentication |
| Service Account | retail-monolith-sa | Workload Identity |
| TLS Secret | retail-monolith-tls | Self-signed certificate |
| Ingress | retail-monolith-ingress | External access with HTTPS |

### Current Configuration
- **Application URL**: https://145.133.57.234
- **Port**: 8080 (container), 80 (service), 443 (ingress)
- **Replicas**: 2
- **Authentication**: Azure AD with Managed Identity
- **HTTPS**: Enabled with self-signed cert
- **Region**: UK South (uksouth)

### Monthly Cost Estimate
- AKS (2 B2s nodes): ~£60
- SQL Database (S1): ~£30
- ACR (Basic): ~£5
- Load Balancer: ~£20
- **Total**: ~£115/month

## Next Steps

### Immediate (Production Hardening)
1. ✅ **Replace self-signed certificate** with Let's Encrypt
2. ✅ **Configure custom domain** and DNS
3. ✅ **Enable Application Insights** for monitoring
4. ✅ **Set up alerts** for failures and performance
5. ✅ **Implement database backups** (automated)
6. ✅ **Configure CI/CD pipeline** (GitHub Actions recommended)

### Future (Microservices Migration)
7. ✅ **Begin RetailDecomposed containerization**
   - 5 microservices: Products, Cart, Orders, Checkout, Web
   - 3 separate databases
   - Inter-service communication
   - Same security patterns (Azure AD, HTTPS, etc.)

## Support and Troubleshooting

**For deployment issues:**
1. Check pod logs: `kubectl logs -n retail-monolith -l app=retail-monolith --tail=100`
2. Check pod status: `kubectl describe pod -n retail-monolith <pod-name>`
3. Review troubleshooting section above
4. Check Azure Portal for infrastructure issues

**Common fixes:**
- Pods not starting → Check logs for specific error
- Can't access app → Verify ingress IP and firewall rules
- Database errors → Check Managed Identity SQL permissions
- Certificate warnings → Expected with self-signed cert (use Let's Encrypt for production)

---

## 🎉 Success! Your RetailMonolith is Production-Ready on AKS

**Achievements:**
- ✅ Containerized ASP.NET Core application
- ✅ Deployed to Azure Kubernetes Service
- ✅ Configured Azure AD authentication (passwordless)
- ✅ Enabled HTTPS with TLS encryption
- ✅ Implemented security best practices
- ✅ Set up high availability (2 replicas)
- ✅ Deployed to UK South region

**You're now ready to tackle the more complex RetailDecomposed microservices architecture!** 🚀

---

*Last Updated: November 24, 2025*
*All configuration reflects actual deployed state with learnings from real deployment issues.*
