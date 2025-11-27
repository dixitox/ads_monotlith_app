# Retail Application Tests

This folder contains functional tests for both the monolithic and decomposed retail applications.

## Port Configuration

**Both systems can now run simultaneously!** SQL Server ports have been separated to enable running both monolith and microservices together.

### SQL Server Ports

| System | Container Name | Host Port | Internal Port | Database |
|--------|---------------|-----------|---------------|----------|
| **Monolith** | `retail-monolith-sqlserver` | **1433** | 1433 | RetailMonolith |
| **Microservices** | `retaildecomposed-sqlserver` | **1434** | 1433 | RetailDecomposedDB |

### Application Ports

**Monolith:**
- Frontend: http://localhost:5068
- Container: `retail-monolith-app`

**Microservices:**
- Frontend: http://localhost:8080
- Products API: http://localhost:8081
- Cart API: http://localhost:8082
- Orders API: http://localhost:8083
- Checkout API: http://localhost:8084

### Connection Strings

```csharp
// Monolith (external access)
Server=localhost,1433;Database=RetailMonolith;User ID=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;

// Microservices (external access)
Server=localhost,1434;Database=RetailDecomposedDB;User ID=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;

// Microservices (internal - from containers)
Server=sqlserver;Database=RetailDecomposedDB;User ID=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;
```

### Running Both Systems Simultaneously

```powershell
# Terminal 1 - Start Monolith
docker-compose up -d

# Terminal 2 - Start Microservices
cd RetailDecomposed
docker-compose -f docker-compose.microservices.yml up -d

# Verify both running
docker ps

# Stop Monolith
docker-compose down

# Stop Microservices
cd RetailDecomposed
docker-compose -f docker-compose.microservices.yml down
```

---

## Test Types

### 1. Unit/Integration Tests (.NET)
Traditional .NET tests using xUnit and in-memory databases.

### 2. Docker Compose Tests (Local Deployment)
End-to-end tests that validate the complete Docker environment with real SQL Server.

See [LOCAL_TESTING_GUIDE.md](./LOCAL_TESTING_GUIDE.md) for detailed Docker testing documentation.

### 3. Azure Kubernetes Service (AKS) Tests (Cloud Deployment)
Automated test suite for validating AKS-deployed applications including Kubernetes infrastructure, frontend UI, backend APIs, and Azure AD authentication.

## Test Projects

### 1. RetailMonolith.Tests
Functional tests for the original monolithic application.

**Test Coverage:**
- ✅ Products Page Tests - Product listing and add-to-cart functionality
- ✅ Cart Page Tests - Cart display and item management
- ✅ Checkout Page Tests - Checkout process validation
- ✅ Orders Page Tests - Order listing and details

**Test Type:** Integration tests using in-memory database

### 2. RetailDecomposed.Tests
Functional tests for the decomposed application with API endpoints.

**Test Coverage:**
- ✅ Products API Tests - GET /api/products and GET /api/products/{id}
- ✅ Cart API Tests - GET /api/cart/{customerId} and POST /api/cart/{customerId}/items
- ✅ Integration Tests - End-to-end workflows and page tests
- ✅ Circular Reference Handling - Ensures JSON serialization works correctly

**Test Type:** Integration tests using in-memory database + API endpoint tests

## Running Tests

### Run All Tests (Unit + Integration + Docker)

```powershell
# From repository root
.\Tests\run-all-tests.ps1
```

This runs:

1. RetailMonolith unit/integration tests
2. RetailDecomposed unit/integration tests  
3. Docker Compose local deployment tests
4. Microservices deployment tests



### Run Unit Tests Only

#### Run Monolith Tests Only
```powershell
# From repository root
dotnet test .\Tests\RetailMonolith.Tests\RetailMonolith.Tests.csproj
```

#### Run Decomposed Tests Only
```powershell
# From repository root
dotnet test .\Tests\RetailDecomposed.Tests\RetailDecomposed.Tests.csproj
```

### Run Docker Compose Tests Only

```powershell
# Complete workflow (build, start, test, cleanup)
.\Tests\run-local-tests.ps1

# Or run tests against already running containers
.\Tests\test-local-deployment.ps1
```

See [LOCAL_TESTING_GUIDE.md](./LOCAL_TESTING_GUIDE.md) for detailed Docker testing documentation.

### Run Azure Kubernetes Service (AKS) Tests Only

```powershell
# Test AKS deployment (auto-detects ingress IP from cluster)
.\Tests\test-aks-deployment.ps1

# Requires:
# - kubectl configured with AKS cluster context
# - NGINX ingress controller deployed
# - Application deployed in 'retail-decomposed' namespace
```

The AKS test suite includes:

- ✅ **Kubernetes Infrastructure** (12 tests)
  - Pod status and health (all pods Running and Ready)
  - Service configurations (ClusterIP, ports)
  - Ingress configuration (NGINX, TLS, external IP)
  
- ✅ **Frontend UI** (5 tests)
  - Home, Products, Cart, Orders, Checkout pages
  - HTTP status codes and content validation
  
- ✅ **Backend APIs** (3 tests)
  - Products API (list and single product)
  - Health endpoint checks
  
- ✅ **Authentication** (2 tests)
  - Azure AD redirect flow
  - OIDC callback endpoint

- 📊 JSON results exported with timestamp
- 🎯 Total: 22 automated tests across 4 categories

### Run Tests with Detailed Output
```powershell
# From repository root
dotnet test .\Tests\RetailMonolith.Tests\RetailMonolith.Tests.csproj --logger "console;verbosity=detailed"
dotnet test .\Tests\RetailDecomposed.Tests\RetailDecomposed.Tests.csproj --logger "console;verbosity=detailed"
```

### Run Specific Test
```powershell
# Filter by test name
dotnet test --filter "ProductsPage_Returns_Success"

# Filter by test class
dotnet test --filter "FullyQualifiedName~ProductsApiTests"
```

## Test Architecture

### Test Fixtures
Both test projects use `WebApplicationFactory<TProgram>` to create in-memory test servers:
- `MonolithWebApplicationFactory` - For RetailMonolith
- `DecomposedWebApplicationFactory` - For RetailDecomposed

### In-Memory Database
Tests use Entity Framework Core's in-memory database provider to:
- Isolate tests from production database
- Ensure fast test execution
- Provide consistent test data

### Test Data Seeding
Each test factory seeds 3 test products:
- Test Product 1 (Electronics) - £10.99
- Test Product 2 (Apparel) - £20.99
- Test Product 3 (Accessories) - £30.99

## Test Results

Tests validate:
1. **HTTP Status Codes** - Ensure endpoints return correct status codes
2. **Response Content** - Verify returned data is correct
3. **Page Rendering** - Check that pages load and contain expected content
4. **API Functionality** - Test API endpoints work as expected
5. **Data Persistence** - Ensure cart and order data persists correctly
6. **Circular Reference Handling** - Verify JSON serialization doesn't fail

## Continuous Testing

### After Changes
Run the relevant test suite after making changes:
- Modified Products module? → Run Products tests
- Modified Cart API? → Run Cart API tests
- Modified any page? → Run integration tests

### Before Deployment
Always run **all tests** before deploying to ensure nothing broke:
```powershell
.\Tests\run-all-tests.ps1
```

## Adding New Tests

### 1. Create Test File
Add a new test file in the appropriate test project:
```csharp
using System.Net;
using Xunit;

namespace RetailMonolith.Tests;

public class MyNewTests : IClassFixture<MonolithWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MyNewTests(MonolithWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MyTest_Does_Something()
    {
        // Arrange
        
        // Act
        var response = await _client.GetAsync("/my-endpoint");
        
        // Assert
        response.EnsureSuccessStatusCode();
    }
}
```

### 2. Run New Tests
```powershell
dotnet test --filter "MyNewTests"
```

## Common Issues

### Issue: Tests fail with database connection errors
**Solution:** Tests use in-memory database by default. Ensure no connection strings are overriding the in-memory provider.

### Issue: Tests fail with circular reference errors
**Solution:** Ensure JSON serialization options include `ReferenceHandler.IgnoreCycles` in Program.cs.

### Issue: Tests timeout
**Solution:** Check that the application is not trying to connect to external services. Mock external dependencies.

## Test Metrics

| Test Suite | Test Files | Test Cases | Coverage |
|------------|-----------|------------|----------|
| RetailMonolith.Tests | 4 | ~15 | Pages |
| RetailDecomposed.Tests | 3 | ~127 | APIs + Pages + Observability |
| Docker Compose Tests | 2 | ~11 | Monolith Deployment |
| Microservices Tests | 1 | ~32 | Local Microservices |
| Azure Kubernetes Tests | 1 | 22 | Cloud Deployment (AKS) |
| **Total** | **11** | **~192** | **Full Stack + Cloud** |

## Best Practices

1. ✅ **Isolate Tests** - Each test should be independent
2. ✅ **Use Fixtures** - Share setup code via test fixtures
3. ✅ **Clear Names** - Test names should describe what they test
4. ✅ **Arrange-Act-Assert** - Follow AAA pattern
5. ✅ **Test Edge Cases** - Include invalid inputs and error scenarios
6. ✅ **Fast Tests** - Keep tests fast by using in-memory database
7. ✅ **Run Regularly** - Run tests after every significant change

## Future Enhancements

- [ ] Add test coverage reports
- [ ] Add performance benchmarking tests
- [ ] Add load tests for API endpoints
- [ ] Add UI automation tests with Selenium/Playwright
- [ ] Add mutation testing
- [ ] Integrate with CI/CD pipeline
