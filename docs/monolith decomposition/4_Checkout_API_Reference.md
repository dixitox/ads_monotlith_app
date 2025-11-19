# Checkout API Reference

## Overview

The Checkout API is a microservice extracted from the Retail Monolith responsible for handling the complete checkout process. This service manages cart processing, inventory reservation, payment processing, and order creation.

**Version:** 1.0  
**Base URL:** `http://localhost:5100` (Development)  
**Protocol:** HTTP/HTTPS  
**Authentication:** None (currently uses customer ID; future: Microsoft Entra ID)

---

## Endpoints

### 1. Health Check

Check if the service is running and healthy.

**Endpoint:** `GET /health`

**Response:**
- **200 OK** - Service is healthy
  ```
  Healthy
  ```

**Example:**
```bash
curl http://localhost:5100/health
```

---

### 2. Process Checkout

Process a customer's cart, charge payment, and create an order.

**Endpoint:** `POST /api/checkout`

**Request Body:**
```json
{
  "customerId": "string",
  "paymentToken": "string"
}
```

**Request Schema:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `customerId` | string | Yes | Unique identifier for the customer |
| `paymentToken` | string | Yes | Payment token from payment provider |

**Response Codes:**

| Code | Status | Description |
|------|--------|-------------|
| 200 | OK | Checkout successful |
| 400 | Bad Request | Validation failure or business rule violation |
| 500 | Internal Server Error | Unexpected error occurred |
| 503 | Service Unavailable | Database or external service temporarily unavailable |

**Success Response (200 OK):**
```json
{
  "orderId": 123,
  "status": "Paid",
  "total": 149.98,
  "createdUtc": "2025-11-19T14:30:00Z"
}
```

**Success Response Schema:**

| Field | Type | Description |
|-------|------|-------------|
| `orderId` | integer | Unique order identifier |
| `status` | string | Order status: "Paid" or "Failed" |
| `total` | decimal | Total order amount in GBP |
| `createdUtc` | datetime | Order creation timestamp (UTC) |

**Error Response (400 Bad Request):**
```json
{
  "error": "Cart not found or empty"
}
```

**Error Response (503 Service Unavailable):**
```json
{
  "error": "Service temporarily unavailable"
}
```

**Example Request:**
```bash
curl -X POST http://localhost:5100/api/checkout \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "guest",
    "paymentToken": "tok_visa_12345"
  }'
```

**Example Response:**
```json
{
  "orderId": 42,
  "status": "Paid",
  "total": 74.98,
  "createdUtc": "2025-11-19T14:35:22.123Z"
}
```

---

## Business Logic Flow

The checkout process executes the following steps:

1. **Validation**
   - Verify `customerId` is not empty
   - Verify `paymentToken` is not empty
   - Returns `400 Bad Request` if validation fails

2. **Cart Retrieval**
   - Fetch cart for the specified customer
   - Include all cart lines
   - Returns `400 Bad Request` if cart not found or empty

3. **Total Calculation**
   - Calculate total: `Sum(UnitPrice × Quantity)` for all cart lines

4. **Stock Reservation**
   - For each cart line:
     - Check inventory availability
     - Decrement inventory quantity
   - Returns `400 Bad Request` if insufficient stock

5. **Payment Processing**
   - Call payment gateway with total, currency (GBP), and payment token
   - Determine order status: "Paid" (success) or "Failed" (declined)

6. **Order Creation**
   - Create order entity with customer ID, status, and total
   - Copy cart lines to order lines
   - Add order to database

7. **Cart Clearing**
   - Remove all cart lines for the customer
   - Cart entity may remain but will be empty

8. **Persistence**
   - Save all changes to database in a single transaction
   - Returns `503 Service Unavailable` if database operation fails

9. **Response**
   - Log checkout completion
   - Return order details to caller

---

## Error Scenarios

### Validation Errors (400 Bad Request)

| Scenario | Error Message |
|----------|---------------|
| Missing customer ID | "Customer ID is required" |
| Missing payment token | "Payment token is required" |
| Cart not found | "Cart not found or empty" |
| Empty cart | "Cart not found or empty" |
| Insufficient stock | "Insufficient stock for SKU: {sku}" |

### External Service Failures

| Scenario | Response Code | Status |
|----------|---------------|--------|
| Payment gateway declines | 200 OK | Status: "Failed" |
| Database unavailable | 503 Service Unavailable | - |
| Unexpected exception | 500 Internal Server Error | - |

**Note:** When payment is declined, the API returns `200 OK` with `status: "Failed"` so the order is still recorded for tracking purposes.

---

## Data Models

### Cart
```csharp
public class Cart
{
    public int Id { get; set; }
    public string CustomerId { get; set; }
    public List<CartLine> Lines { get; set; } = new();
}
```

### CartLine
```csharp
public class CartLine
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public string Sku { get; set; }
    public string Name { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
```

### Order
```csharp
public class Order
{
    public int Id { get; set; }
    public string CustomerId { get; set; }
    public string Status { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<OrderLine> Lines { get; set; } = new();
}
```

### OrderLine
```csharp
public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Sku { get; set; }
    public string Name { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
```

### InventoryItem
```csharp
public class InventoryItem
{
    public int Id { get; set; }
    public string Sku { get; set; } // Unique index
    public int Quantity { get; set; }
}
```

---

## Dependencies

### Payment Gateway

The API depends on `IPaymentGateway` for payment processing:

```csharp
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest req, CancellationToken ct = default);
}
```

**PaymentRequest:**
```csharp
public record PaymentRequest(decimal Amount, string Currency, string Token);
```

**PaymentResult:**
```csharp
public record PaymentResult(bool Succeeded, string? ProviderRef, string? Error);
```

**Current Implementation:** `MockPaymentGateway` (always succeeds for development)

**Future:** Replace with real payment provider (Stripe, etc.)

---

## Configuration

### Connection Strings

**Key:** `ConnectionStrings:DefaultConnection`

**Values:**
- `"InMemory"` - Use in-memory database (testing)
- SQL connection string - Use SQL Server (production)

**Example (appsettings.json):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=RetailDb;Trusted_Connection=True;"
  }
}
```

**Environment Variable Override:**
```bash
ConnectionStrings__DefaultConnection="Server=..."
```

### Logging

Logs are written to console (stdout/stderr) for container compatibility.

**Log Levels:**
- Information: Successful checkout completion
- Warning: Validation failures
- Error: Database errors, unexpected exceptions

**Example Log Entry:**
```
info: RetailMonolith.Checkout.Api.Controllers.CheckoutController[0]
      Checkout completed for customer guest, Order 42, Status Paid
```

---

## Testing

### Unit Tests

**Location:** `RetailMonolith.Checkout.Tests/CheckoutControllerTests.cs`

**Coverage:**
- Happy path scenarios (valid cart, multiple items)
- Validation failures (empty cart, invalid token)
- Business rule failures (insufficient stock)
- External failures (payment declined, database unavailable)

**Total Tests:** 9 unit tests + 1 integration test

### Integration Test

**Location:** `RetailMonolith.Checkout.Tests/CheckoutIntegrationTests.cs`

**Coverage:**
- End-to-end checkout flow
- Database state verification (order created, inventory decremented, cart cleared)

### Running Tests

```bash
# Run all tests
dotnet test RetailMonolith.Checkout.Tests

# Run with coverage
dotnet test RetailMonolith.Checkout.Tests --collect:"XPlat Code Coverage"

# Run with detailed output
dotnet test RetailMonolith.Checkout.Tests --logger "console;verbosity=detailed"
```

---

## Deployment

### Docker

**Dockerfile Location:** `RetailMonolith.Checkout.Api/Dockerfile`

**Build Image:**
```bash
docker build -t checkout-api:latest -f RetailMonolith.Checkout.Api/Dockerfile .
```

**Run Container:**
```bash
docker run -p 5100:5100 -p 5101:5101 \
  -e ConnectionStrings__DefaultConnection="Server=..." \
  checkout-api:latest
```

### Ports

- **HTTP:** 5100
- **HTTPS:** 5101

### Health Checks

Configure orchestrator (Kubernetes/Container Apps) to monitor `/health` endpoint:

```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 5100
  initialDelaySeconds: 10
  periodSeconds: 30
```

---

## Security Considerations

### Current State (Phase 2)

- **Authentication:** None (uses customer ID string)
- **Authorization:** None
- **HTTPS:** Enabled for local development
- **Secrets:** Connection strings via configuration (not hardcoded)

### Future Enhancements (Post-Phase 4)

- **Authentication:** Migrate to Microsoft Entra ID (JWT tokens)
- **Authorization:** Role-based access control
- **Rate Limiting:** Implement to prevent abuse
- **Input Validation:** Enhanced validation with FluentValidation
- **CORS:** Configure allowed origins
- **API Keys:** For service-to-service authentication

---

## OpenAPI / Swagger

**Swagger UI:** `http://localhost:5100/swagger` (when running in development)

**OpenAPI JSON:** `http://localhost:5100/swagger/v1/swagger.json`

The API is configured with OpenAPI support for automatic documentation and testing.

---

## Performance Characteristics

### Current Implementation

- **Database:** EF Core with SQL Server / InMemory
- **Transactions:** Implicit (single `SaveChangesAsync` call)
- **Concurrency:** Optimistic (no locking)
- **Scalability:** Stateless design allows horizontal scaling

### Known Limitations

- **Stock Reservation:** Not atomic (race condition possible under high load)
- **Payment Idempotency:** Not implemented (duplicate charges possible if retried)
- **Cart Locking:** No pessimistic locking (simultaneous checkouts not prevented)

### Future Optimizations

- Implement distributed locking (Redis)
- Add payment idempotency keys
- Batch inventory updates
- Implement event sourcing for order events
- Add caching for inventory checks

---

## Monitoring & Observability

### Logging

All checkout operations are logged with structured data:
- Customer ID
- Order ID
- Status (Paid/Failed)
- Errors with stack traces

### Metrics (Future)

Recommended metrics to collect:
- Checkout success rate
- Checkout failure rate by reason
- Average checkout duration
- Payment gateway response time
- Database query performance

### Tracing (Future)

Prepare for OpenTelemetry:
- Request tracing across service boundaries
- Database query tracing
- Payment gateway call tracing

---

## Support & Troubleshooting

### Common Issues

**Issue:** "Cart not found or empty"  
**Cause:** Customer has no active cart or cart has no items  
**Resolution:** Verify cart exists and has items before checkout

**Issue:** "Insufficient stock for SKU: XXX"  
**Cause:** Inventory quantity less than cart quantity  
**Resolution:** Reduce cart quantity or restock inventory

**Issue:** Payment returns "Failed" status  
**Cause:** Payment gateway declined the charge  
**Resolution:** Check payment token validity, ensure sufficient funds

**Issue:** 503 Service Unavailable  
**Cause:** Database connection failure  
**Resolution:** Check connection string, verify database is accessible

### Debug Mode

Run with detailed logging:
```bash
dotnet run --project RetailMonolith.Checkout.Api --environment Development
```

Set log level in `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "RetailMonolith.Checkout.Api": "Trace"
    }
  }
}
```

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 19 Nov 2025 | Initial release - Phase 2 complete |

---

## Related Documentation

- [Guiding Star](1_Guiding_Star.md) - Strategic vision and future roadmap
- [Phased Plan](2_Phased_Plan.md) - Decomposition phases and acceptance criteria
- [Coding Standards](3_Coding_Standards.md) - Development standards and best practices

---

## Contact & Feedback

For issues, questions, or feedback about the Checkout API, please refer to the project repository or contact the development team.

---

*Last Updated: 19 November 2025*  
*Document Version: 1.0*  
*API Version: 1.0 (Phase 2 Complete)*
