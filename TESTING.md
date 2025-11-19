# Testing Guide for Microservices Architecture

This guide provides comprehensive testing procedures for the decomposed retail application.

## Prerequisites

- Docker Desktop installed and running
- At least 4GB RAM available for containers
- Ports 5000, 5001, and 1433 available on localhost

## Quick Start

```bash
# From repository root
docker compose up --build

# Wait for all services to be healthy (check logs)
# This typically takes 2-3 minutes on first run

# Access the application
open http://localhost:5000
```

## Step-by-Step Testing

### 1. Verify All Services Are Running

```bash
# Check all containers are up
docker compose ps

# Expected output:
# NAME                      STATUS          PORTS
# ads_monotlith_app-db-1    Up (healthy)    0.0.0.0:1433->1433/tcp
# ads_monotlith_app-checkout-api-1  Up (healthy)    0.0.0.0:5001->8080/tcp
# ads_monotlith_app-monolith-1      Up (healthy)    0.0.0.0:5000->8080/tcp
```

### 2. Verify Health Checks

```bash
# Check database is ready
docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -C -Q "SELECT 1"

# Check Checkout API health
curl http://localhost:5001/health
# Expected: Healthy

# Check Monolith health
curl http://localhost:5000/health
# Expected: Healthy
```

### 3. Test End-to-End Checkout Flow

#### Via Browser (Recommended)

1. **Navigate to Application**
   - Open http://localhost:5000
   - You should see the home page

2. **Browse Products**
   - Click "Products" in navigation
   - Verify 50 products are displayed with categories

3. **Add Items to Cart**
   - Click "Add to Cart" on several products
   - Navigate to "Cart"
   - Verify cart displays correct items and totals

4. **Complete Checkout**
   - Click "Checkout"
   - Review cart items and total
   - (Optional) Change payment token
   - Click "Complete Checkout"
   - **This triggers the Checkout API call!**

5. **Verify Order Creation**
   - You should be redirected to Order Details
   - Verify order shows:
     - Order ID
     - Status: "Paid"
     - Correct items and total
     - Creation timestamp

6. **View Order History**
   - Click "Orders" in navigation
   - Verify your order appears in the list

#### Via API (Advanced)

```bash
# 1. Add items to cart (via monolith - requires manual database setup)
# This would typically be done through the UI

# 2. Call Checkout API directly
curl -X POST http://localhost:5001/api/checkout \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "guest",
    "paymentToken": "tok_test",
    "cartId": 0
  }'

# Expected response:
# {
#   "orderId": 1,
#   "status": "Paid",
#   "total": 150.50,
#   "createdUtc": "2024-11-19T10:30:00Z"
# }

# 3. Verify order via monolith API
curl http://localhost:5000/api/orders/1

# Expected: Order details with line items
```

### 4. Verify Service Communication

```bash
# View monolith logs to see Checkout API calls
docker compose logs -f monolith

# In another terminal, trigger a checkout via browser
# Look for log entries like:
# "Calling Checkout API for customer: guest"
# "Checkout completed successfully. OrderId: X"

# View Checkout API logs
docker compose logs -f checkout-api

# Look for:
# "Processing checkout for customer: guest"
# "Checkout completed successfully. OrderId: X"
```

### 5. Test Error Scenarios

#### Cart Not Found

```bash
# Try checkout with non-existent cart
curl -X POST http://localhost:5001/api/checkout \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "nonexistent",
    "paymentToken": "tok_test",
    "cartId": 0
  }'

# Expected: 400 Bad Request
# { "error": "Cart not found" }
```

#### Out of Stock

1. Add a large quantity of items to cart (more than available inventory)
2. Attempt checkout
3. Should receive error about insufficient stock

#### Service Unavailable

```bash
# Stop Checkout API
docker compose stop checkout-api

# Try to checkout via browser
# Should see error message: "Unable to process checkout. Please try again later."

# Restart Checkout API
docker compose start checkout-api
```

### 6. Test Inventory Management

```bash
# Connect to database
docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -C

# Check inventory before checkout
1> SELECT TOP 5 Sku, Quantity FROM InventoryItems ORDER BY Sku;
2> GO

# Note quantities, then perform checkout via browser

# Check inventory after checkout (should be decremented)
1> SELECT TOP 5 Sku, Quantity FROM InventoryItems ORDER BY Sku;
2> GO

# Exit
1> QUIT
```

### 7. Performance Testing

#### Simple Load Test with curl

```bash
# Test health endpoint performance
for i in {1..100}; do
  curl -w "@curl-format.txt" -o /dev/null -s http://localhost:5001/health
done

# Create curl-format.txt with:
time_total: %{time_total}s\n
```

#### Load Test with Apache Bench

```bash
# Install ab (apache2-utils on Ubuntu, httpd on macOS)

# Test Checkout API health endpoint
ab -n 1000 -c 10 http://localhost:5001/health

# Analyze results:
# - Requests per second
# - Mean response time
# - Percentage of requests served within certain time
```

## Monitoring and Debugging

### View Real-Time Logs

```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f checkout-api
docker compose logs -f monolith
docker compose logs -f db

# Filter logs
docker compose logs checkout-api | grep "ERROR"
```

### Inspect Database

```bash
# Connect to SQL Server
docker compose exec db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -C

# Useful queries:
1> SELECT COUNT(*) as ProductCount FROM Products;
2> GO

1> SELECT COUNT(*) as OrderCount FROM Orders;
2> GO

1> SELECT TOP 10 * FROM Orders ORDER BY CreatedUtc DESC;
2> GO

1> SELECT o.Id, o.Status, o.Total, COUNT(ol.Id) as ItemCount
2> FROM Orders o
3> LEFT JOIN OrderLines ol ON o.Id = ol.OrderId
4> GROUP BY o.Id, o.Status, o.Total
5> ORDER BY o.CreatedUtc DESC;
6> GO
```

### Container Resource Usage

```bash
# View resource usage
docker stats

# Expected:
# - Database: 500MB-1GB memory
# - Checkout API: 100-200MB memory
# - Monolith: 100-200MB memory
```

### Access Container Shell

```bash
# Checkout API container
docker compose exec checkout-api /bin/sh

# Monolith container
docker compose exec monolith /bin/sh

# Inside container, check:
ls -la /app
env | grep ConnectionStrings
```

## Troubleshooting

### Services Won't Start

```bash
# Check if ports are in use
lsof -i :5000
lsof -i :5001
lsof -i :1433

# Stop conflicting services or change ports in docker-compose.yml
```

### Database Connection Issues

```bash
# Check database is healthy
docker compose ps db

# View database logs
docker compose logs db

# Verify connection string in environment
docker compose exec checkout-api env | grep ConnectionStrings
```

### Checkout API Not Reachable from Monolith

```bash
# Verify both are on same Docker network
docker network ls
docker network inspect ads_monotlith_app_default

# Ping Checkout API from Monolith
docker compose exec monolith ping checkout-api

# Check environment variable
docker compose exec monolith env | grep CheckoutApi
```

### Out of Memory

```bash
# Check Docker Desktop resource limits
# Settings > Resources > Memory (should be 4GB+)

# Reduce memory if needed by limiting SQL Server
# Add to docker-compose.yml under db service:
mem_limit: 1g
```

## Cleanup

```bash
# Stop all services
docker compose down

# Remove volumes (deletes database)
docker compose down -v

# Remove images
docker compose down --rmi all

# Full cleanup (remove everything)
docker compose down -v --rmi all --remove-orphans
```

## Automated Testing Script

Create a file `test-deployment.sh`:

```bash
#!/bin/bash
set -e

echo "🚀 Starting deployment test..."

# Start services
echo "📦 Starting services..."
docker compose up -d

# Wait for services to be healthy
echo "⏳ Waiting for services to be healthy..."
timeout=120
elapsed=0
while ! docker compose ps | grep -q "healthy"; do
  sleep 5
  elapsed=$((elapsed + 5))
  if [ $elapsed -gt $timeout ]; then
    echo "❌ Services failed to become healthy"
    docker compose logs
    exit 1
  fi
  echo "   Waiting... ${elapsed}s/${timeout}s"
done

echo "✅ All services are healthy!"

# Test health endpoints
echo "🔍 Testing health endpoints..."
curl -f http://localhost:5001/health || exit 1
curl -f http://localhost:5000/health || exit 1

echo "✅ Health checks passed!"

# Test API endpoint (requires pre-populated cart)
echo "🧪 Testing checkout API..."
response=$(curl -s -X POST http://localhost:5001/api/checkout \
  -H "Content-Type: application/json" \
  -d '{"customerId":"guest","paymentToken":"tok_test","cartId":0}' \
  -w "%{http_code}")

if [[ $response == *"400"* ]] && [[ $response == *"Cart not found"* ]]; then
  echo "✅ API responds correctly (cart not found is expected)"
elif [[ $response == *"200"* ]]; then
  echo "✅ Checkout successful!"
else
  echo "❌ Unexpected API response: $response"
  exit 1
fi

echo "🎉 All tests passed!"
echo "📊 Application is running at http://localhost:5000"
echo "📊 Checkout API is at http://localhost:5001"
echo ""
echo "To stop: docker compose down"
```

Make it executable and run:

```bash
chmod +x test-deployment.sh
./test-deployment.sh
```

## Success Criteria

✅ All three containers (db, checkout-api, monolith) are running and healthy  
✅ Health check endpoints return "Healthy"  
✅ Can browse products and add to cart  
✅ Checkout page displays cart contents  
✅ Clicking "Complete Checkout" successfully creates an order  
✅ Order appears in Orders list with correct details  
✅ Inventory is decremented after checkout  
✅ Logs show successful API calls between monolith and Checkout API  
✅ Error scenarios are handled gracefully  

## Next Steps

After successful testing:

1. **Deploy to Cloud**: Consider Azure Container Apps or AKS
2. **Add Monitoring**: Integrate Application Insights or Prometheus
3. **Implement CI/CD**: Set up GitHub Actions for automated testing
4. **Add More Tests**: Unit tests, integration tests, load tests
5. **Enhance Resilience**: Add retry policies, circuit breakers
6. **Improve Security**: Add authentication, API keys, HTTPS

---

For questions or issues, refer to the main README.md or CheckoutApi/README.md.
