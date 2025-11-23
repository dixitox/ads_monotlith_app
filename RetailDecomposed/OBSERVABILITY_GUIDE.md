# Observability Implementation Guide

## Overview

RetailDecomposed has been instrumented with comprehensive observability using **Azure Application Insights** and **OpenTelemetry**. This implementation provides end-to-end distributed tracing, metrics collection, and structured logging for monitoring application performance and diagnosing issues.

**Last Updated**: November 23, 2025

---

## Table of Contents

1. [Architecture](#architecture)
2. [Technologies Used](#technologies-used)
3. [Configuration](#configuration)
4. [Instrumented Components](#instrumented-components)
5. [Telemetry Data Collected](#telemetry-data-collected)
6. [Setup Instructions](#setup-instructions)
7. [Viewing Telemetry in Application Insights](#viewing-telemetry-in-application-insights)
8. [Custom Telemetry](#custom-telemetry)
9. [Best Practices](#best-practices)
10. [Troubleshooting](#troubleshooting)

---

## Architecture

The observability stack consists of:

```
┌─────────────────────────────────────────────────────────────┐
│                    RetailDecomposed App                      │
│  ┌──────────────────────────────────────────────────────┐   │
│  │           OpenTelemetry SDK                          │   │
│  │  - Traces (Distributed Tracing)                      │   │
│  │  - Metrics (Performance Counters)                    │   │
│  │  - Logs (Structured Logging with Trace Context)      │   │
│  └────────────────────┬─────────────────────────────────┘   │
└─────────────────────────┼─────────────────────────────────────┘
                          │
                          ▼
            ┌─────────────────────────────┐
            │  Azure Monitor Exporter      │
            │  (Application Insights)      │
            └─────────────┬────────────────┘
                          │
                          ▼
            ┌─────────────────────────────┐
            │   Azure Application          │
            │      Insights                │
            │  - Transaction Search        │
            │  - Application Map           │
            │  - Performance Monitoring    │
            │  - Live Metrics              │
            │  - Failures & Dependencies   │
            └─────────────────────────────┘
```

---

## Technologies Used

| Technology | Purpose |
|------------|---------|
| **Azure Application Insights** | Cloud-based APM (Application Performance Management) service for telemetry collection and analysis |
| **OpenTelemetry** | Vendor-neutral observability framework for traces, metrics, and logs |
| **OpenTelemetry.Instrumentation.AspNetCore** | Automatic instrumentation for ASP.NET Core applications |
| **OpenTelemetry.Instrumentation.Http** | Automatic instrumentation for HttpClient calls |
| **OpenTelemetry.Instrumentation.SqlClient** | Automatic instrumentation for SQL Server database operations |
| **Azure.Monitor.OpenTelemetry.AspNetCore** | Azure Monitor exporter for sending telemetry to Application Insights |

---

## Configuration

### Application Settings

#### Development Environment (`appsettings.Development.json`)

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost;LiveEndpoint=https://localhost"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Azure.Core": "Information",
      "Azure.Identity": "Information",
      "RetailDecomposed.Services": "Debug"
    }
  }
}
```

#### Production Environment (`appsettings.json`)

```json
{
  "_comment_ApplicationInsights": "WARNING: Configure in Azure App Service Configuration or environment variables.",
  "ApplicationInsights": {
    "ConnectionString": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Azure.Core": "Warning",
      "Azure.Identity": "Warning"
    }
  }
}
```

### Environment Variables (Production)

For production deployments, configure the Application Insights connection string using:

**Azure App Service Configuration:**
```
Name: ApplicationInsights__ConnectionString
Value: InstrumentationKey=<your-key>;IngestionEndpoint=https://...;LiveEndpoint=https://...
```

**Or Environment Variable:**
```bash
export ApplicationInsights__ConnectionString="InstrumentationKey=<your-key>;IngestionEndpoint=https://..."
```

---

## Instrumented Components

### 1. **ASP.NET Core Automatic Instrumentation**

- **HTTP Requests**: All incoming HTTP requests are traced
- **Request Duration**: Time taken to process each request
- **Response Status**: HTTP status codes (200, 404, 500, etc.)
- **User Context**: Authenticated user information

### 2. **HTTP Client Instrumentation**

- **Outgoing API Calls**: All HttpClient requests to internal APIs
- **Dependencies**: External service dependencies
- **Request/Response Details**: URLs, status codes, headers

### 3. **SQL Server Instrumentation**

- **Database Queries**: SQL statements executed
- **Query Performance**: Duration and success/failure
- **Connection Details**: Server, database name

### 4. **Custom Service Instrumentation**

All service classes have been instrumented with custom activity sources:

| Service | Activity Source | Operations Tracked |
|---------|----------------|-------------------|
| **CopilotService** | `RetailDecomposed.Services.Copilot` | GetChatResponse, AzureOpenAI.CompleteChat |
| **ProductsApiClient** | `RetailDecomposed.Services.Products` | GetProducts, GetProductById |
| **CartApiClient** | `RetailDecomposed.Services.Cart` | GetCart, AddToCart |
| **OrdersApiClient** | `RetailDecomposed.Services.Orders` | GetOrders, GetOrderById |
| **CheckoutApiClient** | `RetailDecomposed.Services.Checkout` | Checkout |

---

## Telemetry Data Collected

### Traces (Distributed Tracing)

**Automatic Spans:**
- HTTP requests (incoming/outgoing)
- Database queries
- HttpClient calls

**Custom Spans:**
- AI Copilot chat operations
- Product catalog queries
- Cart operations
- Order management
- Checkout processing

**Example Trace Hierarchy:**
```
└─ GET /api/chat
   ├─ CopilotService.GetChatResponse
   │  ├─ ProductsApiClient.GetProducts
   │  │  └─ HTTP GET /api/products
   │  └─ AzureOpenAI.CompleteChat
   └─ Response 200 OK
```

### Custom Tags

Each span includes rich contextual tags:

**CopilotService:**
- `copilot.user_message_length`: Length of user message
- `copilot.has_conversation_history`: Whether history exists
- `copilot.history_message_count`: Number of previous messages
- `copilot.product_count`: Products in catalog
- `copilot.response_length`: Length of AI response
- `ai.model`: Model deployment name
- `ai.max_tokens`: Token limit
- `ai.temperature`: Temperature setting
- `ai.response_tokens`: Tokens in response
- `ai.total_tokens`: Total tokens used

**ProductsApiClient:**
- `products.operation`: Type of operation (list, get_by_id)
- `products.id`: Product ID (for get_by_id)
- `products.count`: Number of products returned
- `products.found`: Whether product was found
- `http.status_code`: HTTP status code

**CartApiClient:**
- `cart.operation`: Type of operation (get, add_item)
- `cart.customer_id`: Customer identifier
- `cart.items_count`: Number of items in cart
- `cart.product_id`: Product being added
- `cart.quantity`: Quantity being added
- `http.status_code`: HTTP status code

**OrdersApiClient:**
- `orders.operation`: Type of operation (list, get_by_id)
- `orders.id`: Order ID
- `orders.count`: Number of orders returned
- `orders.found`: Whether order was found
- `http.status_code`: HTTP status code

**CheckoutApiClient:**
- `checkout.operation`: process
- `checkout.customer_id`: Customer identifier
- `checkout.order_id`: Created order ID
- `checkout.order_total`: Order total amount
- `http.status_code`: HTTP status code

### Metrics

**ASP.NET Core Metrics:**
- Request rate (requests/sec)
- Request duration (percentiles: p50, p95, p99)
- Active requests
- Failed requests

**HTTP Client Metrics:**
- Outgoing request rate
- Request duration
- Active connections

**Runtime Metrics:**
- CPU usage
- Memory usage
- GC collections
- Thread pool utilization

### Logs

**Structured Logging with Trace Correlation:**
- All logs include trace context (TraceId, SpanId)
- Logs are correlated with distributed traces
- Exception details captured and linked to traces

---

## Setup Instructions

### 1. Create Application Insights Resource

**Via Azure Portal:**

1. Navigate to [Azure Portal](https://portal.azure.com)
2. Click "Create a resource" → "Application Insights"
3. Fill in details:
   - **Subscription**: Your subscription
   - **Resource Group**: Your resource group
   - **Name**: `retail-decomposed-insights`
   - **Region**: Same as your app
   - **Workspace**: Create new or use existing Log Analytics workspace
4. Click "Review + Create" → "Create"
5. After deployment, navigate to the resource
6. Copy the **Connection String** from the Overview page

**Via Azure CLI:**

```bash
# Create Log Analytics workspace (required)
az monitor log-analytics workspace create \
  --resource-group <your-rg> \
  --workspace-name retail-logs \
  --location <region>

# Get workspace ID
WORKSPACE_ID=$(az monitor log-analytics workspace show \
  --resource-group <your-rg> \
  --workspace-name retail-logs \
  --query id -o tsv)

# Create Application Insights
az monitor app-insights component create \
  --app retail-decomposed-insights \
  --location <region> \
  --resource-group <your-rg> \
  --workspace $WORKSPACE_ID

# Get connection string
az monitor app-insights component show \
  --app retail-decomposed-insights \
  --resource-group <your-rg> \
  --query connectionString -o tsv
```

### 2. Configure Connection String

**For Local Development:**

Update `appsettings.Development.json`:

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=<your-key>;IngestionEndpoint=https://...;LiveEndpoint=https://..."
  }
}
```

**For Production (Azure App Service):**

```bash
az webapp config appsettings set \
  --resource-group <your-rg> \
  --name <your-app-name> \
  --settings ApplicationInsights__ConnectionString="<your-connection-string>"
```

### 3. Run the Application

```powershell
cd RetailDecomposed
dotnet run
```

The application will start sending telemetry to Application Insights automatically.

### 4. Verify Telemetry

After running the app for a few minutes:

1. Navigate to your Application Insights resource in Azure Portal
2. Go to "Live Metrics" to see real-time telemetry
3. Go to "Transaction Search" to view individual traces
4. Go to "Application Map" to see service dependencies

---

## Viewing Telemetry in Application Insights

### 1. **Live Metrics Stream**

- Real-time monitoring of requests, dependencies, and exceptions
- CPU and memory usage
- Request rate and duration

**Navigation:** Application Insights → Live Metrics

### 2. **Transaction Search**

- Search for individual requests and traces
- Filter by time range, status code, user, etc.
- View end-to-end transaction details
- See correlated logs and exceptions

**Navigation:** Application Insights → Transaction Search

**Example Queries:**
- Find all failed requests: `resultCode >= 400`
- Find AI Copilot operations: `operation/name contains "Copilot"`
- Find slow requests: `duration > 2000`

### 3. **Application Map**

- Visualize service dependencies
- See health and performance of each component
- Identify bottlenecks and failures

**Navigation:** Application Insights → Application Map

### 4. **Performance**

- View performance trends over time
- Identify slow operations
- Analyze dependencies

**Navigation:** Application Insights → Performance

### 5. **Failures**

- View exception rate and types
- See failed dependencies
- Drill into specific failures

**Navigation:** Application Insights → Failures

### 6. **Logs (Kusto Queries)**

Run custom queries using Kusto Query Language (KQL):

**Navigation:** Application Insights → Logs

**Example Queries:**

```kusto
// All traces from CopilotService
traces
| where customDimensions.CategoryName == "RetailDecomposed.Services.CopilotService"
| order by timestamp desc

// Failed requests with details
requests
| where success == false
| project timestamp, name, resultCode, duration, customDimensions

// AI Copilot performance
dependencies
| where customDimensions["ai.model"] != ""
| summarize avg(duration), percentiles(duration, 50, 95, 99) by bin(timestamp, 5m)

// Database query performance
dependencies
| where type == "SQL"
| summarize count(), avg(duration) by name
| order by avg_duration desc

// Custom tags from CopilotService
traces
| where customDimensions["copilot.user_message_length"] != ""
| project 
    timestamp,
    message,
    message_length = toint(customDimensions["copilot.user_message_length"]),
    product_count = toint(customDimensions["copilot.product_count"]),
    response_length = toint(customDimensions["copilot.response_length"])

// Checkout funnel analysis
requests
| where name contains "cart" or name contains "checkout" or name contains "orders"
| summarize count() by name, resultCode
| order by name
```

---

## Custom Telemetry

### Adding Custom Activity Sources

All activity sources are centrally managed in `TelemetryActivitySources.cs`:

```csharp
public static class TelemetryActivitySources
{
    public const string ServiceName = "RetailDecomposed";
    
    public static readonly ActivitySource Copilot = new(
        $"{ServiceName}.Services.Copilot",
        "1.0.0");
    
    // Add new sources here...
}
```

### Creating Custom Spans

```csharp
using System.Diagnostics;

public class MyService
{
    private static readonly ActivitySource _activitySource = TelemetryActivitySources.MyService;
    
    public async Task DoWorkAsync()
    {
        using var activity = _activitySource.StartActivity("DoWork", ActivityKind.Server);
        activity?.SetTag("custom.tag", "value");
        
        try
        {
            // Your business logic
            await PerformWork();
            
            activity?.SetTag("work.result", "success");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
}
```

### Registering Activity Sources

Update `Program.cs` to include new activity sources:

```csharp
.WithTracing(tracing => tracing
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddSqlClientInstrumentation()
    .AddSource("RetailDecomposed.Services.*")  // Matches all service sources
    .AddSource("RetailDecomposed.MyModule.*")) // Add specific modules
```

---

## Best Practices

### 1. **Sampling**

For high-volume production environments, configure sampling:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .SetSampler(new TraceIdRatioBasedSampler(0.1))) // Sample 10% of traces
```

### 2. **Sensitive Data**

**DO NOT** log sensitive information in tags:
- Passwords
- API keys
- Credit card numbers
- Personal identifiable information (PII)

### 3. **Tag Naming Conventions**

Follow semantic conventions:
- Use lowercase with dots: `service.operation`, `http.status_code`
- Use prefixes for grouping: `copilot.*`, `cart.*`, `orders.*`

### 4. **Activity Naming**

Use clear, descriptive names:
- ✅ `GetProductById`, `ProcessCheckout`, `CompleteAIChat`
- ❌ `Process`, `DoWork`, `Execute`

### 5. **Exception Handling**

Always record exceptions in spans:

```csharp
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.RecordException(ex);
    throw; // Re-throw to preserve stack trace
}
```

### 6. **Performance Considerations**

- Activity creation is lightweight (~100ns)
- Tags are cheap to add
- Avoid creating activities in hot paths if not needed
- Use sampling for high-throughput scenarios

---

## Troubleshooting

### No Telemetry Appearing

**Check Configuration:**
```powershell
# Verify connection string is set
dotnet user-secrets list
# Or check appsettings.Development.json
```

**Check Console Output:**
```
Application Insights not configured - telemetry disabled
```

**Solution:** Add valid connection string to configuration

### Connection Failures

**Error:** `Failed to send telemetry to Application Insights`

**Causes:**
- Network connectivity issues
- Invalid connection string
- Firewall blocking Application Insights endpoints

**Solution:**
1. Verify connection string format
2. Test network connectivity:
   ```bash
   curl https://dc.services.visualstudio.com/v2/track
   ```
3. Check firewall rules

### High Latency in Telemetry

**Symptoms:** Telemetry appears in Application Insights with significant delay

**Causes:**
- Data ingestion delay (normal: 2-5 minutes)
- Sampling reducing visible data
- Throttling due to high volume

**Solution:**
- Wait 5-10 minutes for data to appear
- Check "Live Metrics" for real-time data
- Review sampling configuration

### Missing Custom Spans

**Issue:** Custom activity sources not appearing in traces

**Solution:**
1. Verify activity source is registered in `Program.cs`:
   ```csharp
   .AddSource("RetailDecomposed.Services.*")
   ```
2. Ensure `StartActivity()` is being called
3. Check that activity is disposed (use `using` statement)

### Exceptions Not Captured

**Issue:** Exceptions not showing in Application Insights

**Solution:**
1. Ensure exceptions are being logged:
   ```csharp
   _logger.LogError(ex, "Error message");
   ```
2. Record exception in activity:
   ```csharp
   activity?.RecordException(ex);
   ```
3. Set activity status:
   ```csharp
   activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
   ```

---

## Production Deployment

### Azure App Service

**Configure Application Insights:**

```bash
# Set connection string
az webapp config appsettings set \
  --resource-group <rg> \
  --name <app-name> \
  --settings ApplicationInsights__ConnectionString="<connection-string>"

# Enable Application Insights (automatic instrumentation)
az webapp config appsettings set \
  --resource-group <rg> \
  --name <app-name> \
  --settings APPLICATIONINSIGHTS_CONNECTION_STRING="<connection-string>"
```

### Azure Container Apps

**Add environment variable:**

```bash
az containerapp update \
  --name <app-name> \
  --resource-group <rg> \
  --set-env-vars ApplicationInsights__ConnectionString=<connection-string>
```

### Kubernetes

**Create secret:**

```bash
kubectl create secret generic app-insights \
  --from-literal=connection-string='<connection-string>'
```

**Mount in deployment:**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: retail-decomposed
spec:
  template:
    spec:
      containers:
      - name: app
        env:
        - name: ApplicationInsights__ConnectionString
          valueFrom:
            secretKeyRef:
              name: app-insights
              key: connection-string
```

---

## Additional Resources

- [Azure Application Insights Documentation](https://docs.microsoft.com/azure/azure-monitor/app/app-insights-overview)
- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/instrumentation/net/)
- [Application Insights KQL Reference](https://docs.microsoft.com/azure/data-explorer/kusto/query/)
- [Distributed Tracing Concepts](https://opentelemetry.io/docs/concepts/observability-primer/)

---

## Summary

RetailDecomposed now has comprehensive observability with:

✅ **End-to-end distributed tracing** across all services  
✅ **Custom instrumentation** for business operations  
✅ **Performance metrics** for requests, dependencies, and runtime  
✅ **Structured logging** with trace correlation  
✅ **Rich contextual tags** for filtering and analysis  
✅ **Exception tracking** with full stack traces  
✅ **Azure Application Insights integration** for monitoring and alerting  

This observability stack enables you to:
- Monitor application health in real-time
- Diagnose performance bottlenecks
- Trace requests across distributed services
- Identify and troubleshoot errors quickly
- Optimize resource usage
- Set up alerts for critical issues
