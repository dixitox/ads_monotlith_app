# Checkout API

A standalone microservice extracted from the retail monolith, responsible for orchestrating the checkout process.

## Overview

The Checkout API handles:
- Validating cart contents
- Reserving inventory
- Processing payments via MockPaymentGateway
- Creating and persisting orders
- Clearing cart after successful checkout

## Architecture

This service follows the **Decomposition Pattern** for microservices, extracting checkout functionality from the monolith while maintaining a shared database approach.

### Service Boundaries
- **Checkout API**: Checkout orchestration, payment processing, order creation
- **Monolith**: Product catalog, cart management, order viewing

## API Endpoints

### POST /api/checkout
Processes a checkout request for a customer's cart.

**Request:**
```json
{
  "customerId": "guest",
  "paymentToken": "tok_test",
  "cartId": 0
}
```

**Response (Success - 200 OK):**
```json
{
  "orderId": 123,
  "status": "Paid",
  "total": 150.50,
  "createdUtc": "2024-11-19T10:30:00Z"
}
```

**Response (Error - 400 Bad Request):**
```json
{
  "error": "Cart not found"
}
```

### GET /health
Health check endpoint for monitoring and container orchestration.

**Response:**
```
Healthy
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | Database connection string | LocalDB |
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | Development |
| `ASPNETCORE_URLS` | URLs the app listens on | `http://+:8080` |

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=RetailMonolith;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

## Running Locally

### Prerequisites
- .NET 9 SDK
- SQL Server or LocalDB

### Commands
```bash
# Restore packages
dotnet restore

# Build
dotnet build

# Run
dotnet run

# Access Swagger UI
open http://localhost:5001/swagger
```

## Running with Docker

### Build Image
```bash
docker build -t checkout-api -f Dockerfile ..
```

### Run Container
```bash
docker run -p 5001:8080 \
  -e ConnectionStrings__DefaultConnection="Server=host.docker.internal;Database=RetailMonolith;User=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True" \
  checkout-api
```

## Running with Docker Compose

From the repository root:

```bash
# Start all services (database, checkout-api, monolith)
docker-compose up --build

# Access Checkout API
curl http://localhost:5001/health

# Stop all services
docker-compose down
```

## Testing

### Manual Testing with curl

```bash
# Check health
curl http://localhost:5001/health

# Process checkout (requires existing cart in database)
curl -X POST http://localhost:5001/api/checkout \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "guest",
    "paymentToken": "tok_test",
    "cartId": 0
  }'
```

## Database Schema

The Checkout API uses the following tables from the shared database:

- **Carts**: Shopping cart records
- **CartLines**: Line items in carts
- **Orders**: Order records
- **OrderLines**: Line items in orders
- **Inventory**: Stock levels by SKU

## Error Handling

The API returns appropriate HTTP status codes:

- **200 OK**: Checkout successful
- **400 Bad Request**: Invalid request or business logic error (cart not found, out of stock)
- **500 Internal Server Error**: Unexpected server error

All errors include a JSON response with an `error` field describing the issue.

## Logging

The API uses structured logging via `ILogger<T>`:

- **Information**: Successful checkouts
- **Warning**: Business logic errors (cart not found, out of stock)
- **Error**: Unexpected exceptions

## Security Features

### Docker Container
- Runs as non-root user (`appuser`)
- Uses Alpine-based runtime image for smaller attack surface
- Multi-stage build separates build and runtime dependencies

## Future Enhancements

- [ ] Implement circuit breaker pattern with Polly
- [ ] Add distributed tracing with OpenTelemetry
- [ ] Publish OrderCreated events to message bus
- [ ] Implement database per service pattern
- [ ] Add comprehensive unit and integration tests
- [ ] Implement rate limiting
- [ ] Add authentication and authorization
- [ ] Implement idempotency for checkout requests

## Related Services

- **Retail Monolith**: Main application with product catalog and cart management
- **Database**: Shared SQL Server database

## Support

For issues or questions, please refer to the main repository documentation.
