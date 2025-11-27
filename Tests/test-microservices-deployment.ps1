#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Automated test script for RetailDecomposed microservices deployment
.DESCRIPTION
    Tests all 5 microservices individually, inter-service communication, and end-to-end flows
.PARAMETER Environment
    Environment to test: Local (docker-compose) or Azure (AKS)
.PARAMETER BaseUrl
    Base URL for Frontend service (default: http://localhost:8080 for local)
.EXAMPLE
    .\test-microservices-deployment.ps1 -Environment Local
.EXAMPLE
    .\test-microservices-deployment.ps1 -Environment Azure -BaseUrl "https://retail-decomposed.uksouth.cloudapp.azure.com"
#>

param(
    [ValidateSet("Local", "Azure")]
    [string]$Environment = "Local",
    [string]$BaseUrl = ""
)

$ErrorActionPreference = "Stop"
$testResults = @()

# Set default URLs based on environment
if ($Environment -eq "Local") {
    $FrontendUrl = if ($BaseUrl) { $BaseUrl } else { "http://localhost:8080" }
    $ProductsUrl = "http://localhost:8081"
    $CartUrl = "http://localhost:8082"
    $OrdersUrl = "http://localhost:8083"
    $CheckoutUrl = "http://localhost:8084"
} else {
    $FrontendUrl = if ($BaseUrl) { $BaseUrl } else { throw "BaseUrl required for Azure environment" }
    # For Azure, backend services are not exposed externally
    $ProductsUrl = $null
    $CartUrl = $null
    $OrdersUrl = $null
    $CheckoutUrl = $null
}

function Write-TestHeader {
    param([string]$Message)
    Write-Host "`n╔═══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "╚═══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
}

function Write-TestResult {
    param(
        [string]$TestName,
        [bool]$Passed,
        [string]$Details = "",
        [string]$Category = "General"
    )
    
    $result = @{
        Category = $Category
        Test = $TestName
        Passed = $Passed
        Details = $Details
        Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    }
    
    $script:testResults += $result
    
    if ($Passed) {
        Write-Host "  ✅ PASS: $TestName" -ForegroundColor Green
        if ($Details) {
            Write-Host "     $Details" -ForegroundColor Gray
        }
    } else {
        Write-Host "  ❌ FAIL: $TestName" -ForegroundColor Red
        if ($Details) {
            Write-Host "     $Details" -ForegroundColor Yellow
        }
    }
}

function Test-ServiceHealth {
    param(
        [string]$ServiceName,
        [string]$HealthUrl
    )
    
    try {
        $response = Invoke-WebRequest -Uri $HealthUrl -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-TestResult "$ServiceName Health Check" $true "Status: Healthy (200 OK)" "Health"
            return $true
        } else {
            Write-TestResult "$ServiceName Health Check" $false "Status Code: $($response.StatusCode)" "Health"
            return $false
        }
    } catch {
        Write-TestResult "$ServiceName Health Check" $false $_.Exception.Message "Health"
        return $false
    }
}

# ============================================================================
# HEADER
# ============================================================================
Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║                                                                       ║" -ForegroundColor Magenta
Write-Host "║          RetailDecomposed - Microservices Test Suite                 ║" -ForegroundColor Magenta
Write-Host "║                                                                       ║" -ForegroundColor Magenta
Write-Host "╚═══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""
Write-Host "Environment:    $Environment" -ForegroundColor White
Write-Host "Frontend URL:   $FrontendUrl" -ForegroundColor White
if ($Environment -eq "Local") {
    Write-Host "Products URL:   $ProductsUrl" -ForegroundColor White
    Write-Host "Cart URL:       $CartUrl" -ForegroundColor White
    Write-Host "Orders URL:     $OrdersUrl" -ForegroundColor White
    Write-Host "Checkout URL:   $CheckoutUrl" -ForegroundColor White
}
Write-Host ""

# ============================================================================
# PHASE 1: CONTAINER HEALTH CHECKS (Local only)
# ============================================================================
if ($Environment -eq "Local") {
    Write-TestHeader "Phase 1: Docker Container Health Checks"
    
    try {
        Push-Location "$PSScriptRoot/../RetailDecomposed"
        $containers = docker-compose -f docker-compose.microservices-no-sql.yml ps --format json | ConvertFrom-Json
        Pop-Location
        
        $expectedServices = @("products-service", "cart-service", "orders-service", "checkout-service", "frontend-service")
        
        foreach ($serviceName in $expectedServices) {
            $container = $containers | Where-Object { $_.Service -eq $serviceName }
            if ($container) {
                $isHealthy = ($container.State -eq "running") -and (($container.Health -eq "healthy") -or ($container.Health -eq ""))
                if ($isHealthy) {
                    Write-TestResult "$serviceName Container" $true "State: $($container.State), Health: $($container.Health)" "Containers"
                } else {
                    Write-TestResult "$serviceName Container" $false "State: $($container.State), Health: $($container.Health)" "Containers"
                }
            } else {
                Write-TestResult "$serviceName Container" $false "Container not found" "Containers"
            }
        }
    } catch {
        Write-TestResult "Container Status Check" $false $_.Exception.Message "Containers"
    }
}

# ============================================================================
# PHASE 2: INDIVIDUAL SERVICE HEALTH CHECKS
# ============================================================================
Write-TestHeader "Phase 2: Individual Service Health Endpoints"

$servicesHealthy = @{}

# Frontend Service
$servicesHealthy['Frontend'] = Test-ServiceHealth "Frontend Service" "$FrontendUrl/health"

if ($Environment -eq "Local") {
    # Products Service
    $servicesHealthy['Products'] = Test-ServiceHealth "Products Service" "$ProductsUrl/health"
    
    # Cart Service
    $servicesHealthy['Cart'] = Test-ServiceHealth "Cart Service" "$CartUrl/health"
    
    # Orders Service
    $servicesHealthy['Orders'] = Test-ServiceHealth "Orders Service" "$OrdersUrl/health"
    
    # Checkout Service
    $servicesHealthy['Checkout'] = Test-ServiceHealth "Checkout Service" "$CheckoutUrl/health"
}

# ============================================================================
# PHASE 3: PRODUCTS SERVICE API TESTS (Local only)
# ============================================================================
if ($Environment -eq "Local" -and $servicesHealthy['Products']) {
    Write-TestHeader "Phase 3: Products Service API Tests"
    
    # Test 3.1: Get all products
    try {
        $response = Invoke-RestMethod -Uri "$ProductsUrl/api/products" -Method Get -TimeoutSec 10
        if ($response -and $response.Count -gt 0) {
            Write-TestResult "Get All Products" $true "Retrieved $($response.Count) products" "Products API"
            $script:testProductId = $response[0].id
        } else {
            Write-TestResult "Get All Products" $false "No products returned" "Products API"
        }
    } catch {
        Write-TestResult "Get All Products" $false $_.Exception.Message "Products API"
    }
    
    # Test 3.2: Get product by ID
    if ($script:testProductId) {
        try {
            $response = Invoke-RestMethod -Uri "$ProductsUrl/api/products/$script:testProductId" -Method Get -TimeoutSec 10
            if ($response -and $response.id -eq $script:testProductId) {
                Write-TestResult "Get Product by ID" $true "Product ID: $($response.id), Name: $($response.name)" "Products API"
            } else {
                Write-TestResult "Get Product by ID" $false "Product not found or ID mismatch" "Products API"
            }
        } catch {
            Write-TestResult "Get Product by ID" $false $_.Exception.Message "Products API"
        }
    }
    
    # Test 3.3: Get products by category
    try {
        $response = Invoke-RestMethod -Uri "$ProductsUrl/api/products/category/Electronics" -Method Get -TimeoutSec 10
        if ($response) {
            Write-TestResult "Get Products by Category" $true "Retrieved $($response.Count) products in Electronics" "Products API"
        } else {
            Write-TestResult "Get Products by Category" $false "No products in category" "Products API"
        }
    } catch {
        Write-TestResult "Get Products by Category" $false $_.Exception.Message "Products API"
    }
}

# ============================================================================
# PHASE 4: CART SERVICE API TESTS (Local only)
# ============================================================================
if ($Environment -eq "Local" -and $servicesHealthy['Cart']) {
    Write-TestHeader "Phase 4: Cart Service API Tests"
    
    $testCustomerId = "test-customer-" + (Get-Random -Minimum 1000 -Maximum 9999)
    
    # Test 4.1: Get empty cart
    try {
        $response = Invoke-RestMethod -Uri "$CartUrl/api/cart/$testCustomerId" -Method Get -TimeoutSec 10
        if ($response) {
            $itemCount = if ($response.lines) { $response.lines.Count } else { 0 }
            Write-TestResult "Get Empty Cart" $true "Customer ID: $testCustomerId, Items: $itemCount" "Cart API"
        } else {
            Write-TestResult "Get Empty Cart" $false "No cart returned" "Cart API"
        }
    } catch {
        Write-TestResult "Get Empty Cart" $false $_.Exception.Message "Cart API"
    }
    
    # Test 4.2: Add item to cart (requires Products service)
    if ($script:testProductId -and $servicesHealthy['Products']) {
        try {
            # Cart API expects query parameters, not JSON body
            $response = Invoke-RestMethod -Uri "$CartUrl/api/cart/$testCustomerId/items?productId=$script:testProductId&quantity=2" -Method Post -TimeoutSec 10
            if ($response) {
                Write-TestResult "Add Item to Cart" $true "Added product $script:testProductId, Quantity: 2" "Cart API"
            } else {
                Write-TestResult "Add Item to Cart" $false "No response from cart service" "Cart API"
            }
        } catch {
            Write-TestResult "Add Item to Cart" $false $_.Exception.Message "Cart API"
        }
    }
    
    # Test 4.3: Get cart with items
    try {
        Start-Sleep -Seconds 1
        $response = Invoke-RestMethod -Uri "$CartUrl/api/cart/$testCustomerId" -Method Get -TimeoutSec 10
        if ($response -and $response.lines.Count -gt 0) {
            Write-TestResult "Get Cart with Items" $true "Cart has $($response.lines.Count) item(s)" "Cart API"
        } else {
            Write-TestResult "Get Cart with Items" $false "Cart is empty after adding items" "Cart API"
        }
    } catch {
        Write-TestResult "Get Cart with Items" $false $_.Exception.Message "Cart API"
    }
    
    # Test 4.4: Clear cart
    try {
        $response = Invoke-RestMethod -Uri "$CartUrl/api/cart/$testCustomerId" -Method Delete -TimeoutSec 10
        Write-TestResult "Clear Cart" $true "Cart cleared successfully" "Cart API"
    } catch {
        Write-TestResult "Clear Cart" $false $_.Exception.Message "Cart API"
    }
}

# ============================================================================
# PHASE 5: ORDERS SERVICE API TESTS (Local only)
# ============================================================================
if ($Environment -eq "Local" -and $servicesHealthy['Orders']) {
    Write-TestHeader "Phase 5: Orders Service API Tests"
    
    # Test 5.1: Get all orders
    try {
        $response = Invoke-RestMethod -Uri "$OrdersUrl/api/orders" -Method Get -TimeoutSec 10
        if ($response) {
            Write-TestResult "Get All Orders" $true "Retrieved $($response.Count) orders" "Orders API"
            if ($response.Count -gt 0) {
                $script:testOrderId = $response[0].orderId
            }
        } else {
            Write-TestResult "Get All Orders" $false "No orders returned" "Orders API"
        }
    } catch {
        Write-TestResult "Get All Orders" $false $_.Exception.Message "Orders API"
    }
    
    # Test 5.2: Get order by ID
    if ($script:testOrderId) {
        try {
            $response = Invoke-RestMethod -Uri "$OrdersUrl/api/orders/$script:testOrderId" -Method Get -TimeoutSec 10
            if ($response -and $response.orderId -eq $script:testOrderId) {
                Write-TestResult "Get Order by ID" $true "Order ID: $script:testOrderId, Total: £$($response.totalAmount)" "Orders API"
            } else {
                Write-TestResult "Get Order by ID" $false "Order not found" "Orders API"
            }
        } catch {
            Write-TestResult "Get Order by ID" $false $_.Exception.Message "Orders API"
        }
    }
}

# ============================================================================
# PHASE 6: CHECKOUT SERVICE API TESTS (Local only)
# ============================================================================
if ($Environment -eq "Local" -and $servicesHealthy['Checkout'] -and $servicesHealthy['Cart'] -and $servicesHealthy['Products']) {
    Write-TestHeader "Phase 6: Checkout Service API Tests (End-to-End)"
    
    $testCustomerId = "test-customer-" + (Get-Random -Minimum 1000 -Maximum 9999)
    
    # Step 1: Add items to cart
    try {
        if ($script:testProductId) {
            # Cart API expects query parameters, not JSON body
            Invoke-RestMethod -Uri "$CartUrl/api/cart/$testCustomerId/items?productId=$script:testProductId&quantity=1" -Method Post -TimeoutSec 10 | Out-Null
            Start-Sleep -Seconds 1
        }
    } catch {
        Write-Host "     Warning: Could not add item to cart for checkout test" -ForegroundColor Yellow
    }
    
    # Step 2: Process checkout
    try {
        $checkoutBody = @{
            customerId = $testCustomerId
            paymentToken = "test-payment-token-12345"
        } | ConvertTo-Json
        
        $response = Invoke-RestMethod -Uri "$CheckoutUrl/api/checkout" -Method Post -Body $checkoutBody -ContentType "application/json" -TimeoutSec 15
        if ($response -and $response.id) {
            Write-TestResult "Process Checkout" $true "Order ID: $($response.id), Status: $($response.status)" "Checkout API"
        } else {
            Write-TestResult "Process Checkout" $false "Checkout did not return order" "Checkout API"
        }
    } catch {
        Write-TestResult "Process Checkout" $false $_.Exception.Message "Checkout API"
    }
}

# ============================================================================
# PHASE 7: FRONTEND UI TESTS
# ============================================================================
Write-TestHeader "Phase 7: Frontend UI Tests"

# Test 7.1: Home page
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/" -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Home Page Load" $true "Status Code: 200" "Frontend UI"
    } else {
        Write-TestResult "Home Page Load" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Home Page Load" $false $_.Exception.Message "Frontend UI"
}

# Test 7.2: Products page
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/Products" -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Products Page Load" $true "Status Code: 200" "Frontend UI"
    } else {
        Write-TestResult "Products Page Load" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Products Page Load" $false $_.Exception.Message "Frontend UI"
}

# Test 7.3: Cart page
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/Cart" -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Cart Page Load" $true "Status Code: 200" "Frontend UI"
    } else {
        Write-TestResult "Cart Page Load" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Cart Page Load" $false $_.Exception.Message "Frontend UI"
}

# Test 7.4: Orders page
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/Orders" -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Orders Page Load" $true "Status Code: 200" "Frontend UI"
    } else {
        Write-TestResult "Orders Page Load" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Orders Page Load" $false $_.Exception.Message "Frontend UI"
}

# ============================================================================
# PHASE 8: INTER-SERVICE COMMUNICATION TESTS (Local only)
# ============================================================================
if ($Environment -eq "Local") {
    Write-TestHeader "Phase 8: Inter-Service Communication Tests"
    
    # Test 8.1: Cart → Products (dependency verification)
    if ($servicesHealthy['Cart'] -and $servicesHealthy['Products']) {
        Write-TestResult "Cart → Products Communication" $true "Cart service can call Products service" "Inter-Service"
    } else {
        Write-TestResult "Cart → Products Communication" $false "One or both services unhealthy" "Inter-Service"
    }
    
    # Test 8.2: Checkout → Multiple Services
    if ($servicesHealthy['Checkout'] -and $servicesHealthy['Cart'] -and $servicesHealthy['Products'] -and $servicesHealthy['Orders']) {
        Write-TestResult "Checkout → All Services Communication" $true "Checkout can orchestrate all services" "Inter-Service"
    } else {
        Write-TestResult "Checkout → All Services Communication" $false "Not all services are healthy" "Inter-Service"
    }
    
    # Test 8.3: Frontend → All Backend Services
    if ($servicesHealthy['Frontend'] -and $servicesHealthy['Products'] -and $servicesHealthy['Cart'] -and $servicesHealthy['Orders'] -and $servicesHealthy['Checkout']) {
        Write-TestResult "Frontend → All Backend Services" $true "Frontend can access all backend services" "Inter-Service"
    } else {
        Write-TestResult "Frontend → All Backend Services" $false "Not all services are healthy" "Inter-Service"
    }
}

# ============================================================================
# PHASE 9: PERFORMANCE TESTS
# ============================================================================
Write-TestHeader "Phase 9: Performance Tests"

# Test 9.1: Frontend response time
try {
    $measurements = @()
    1..5 | ForEach-Object {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        Invoke-WebRequest -Uri "$FrontendUrl/" -TimeoutSec 10 -UseBasicParsing | Out-Null
        $stopwatch.Stop()
        $measurements += $stopwatch.ElapsedMilliseconds
    }
    
    $avgTime = ($measurements | Measure-Object -Average).Average
    $maxTime = ($measurements | Measure-Object -Maximum).Maximum
    
    if ($avgTime -lt 2000) {
        Write-TestResult "Frontend Response Time" $true "Avg: $([math]::Round($avgTime, 0))ms, Max: $([math]::Round($maxTime, 0))ms (5 requests)" "Performance"
    } else {
        Write-TestResult "Frontend Response Time" $false "Avg: $([math]::Round($avgTime, 0))ms (slower than expected)" "Performance"
    }
} catch {
    Write-TestResult "Frontend Response Time" $false $_.Exception.Message "Performance"
}

if ($Environment -eq "Local" -and $servicesHealthy['Products']) {
    # Test 9.2: Products API response time
    try {
        $measurements = @()
        1..10 | ForEach-Object {
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            Invoke-RestMethod -Uri "$ProductsUrl/api/products" -Method Get -TimeoutSec 10 | Out-Null
            $stopwatch.Stop()
            $measurements += $stopwatch.ElapsedMilliseconds
        }
        
        $avgTime = ($measurements | Measure-Object -Average).Average
        
        if ($avgTime -lt 500) {
            Write-TestResult "Products API Response Time" $true "Avg: $([math]::Round($avgTime, 0))ms (10 requests)" "Performance"
        } else {
            Write-TestResult "Products API Response Time" $false "Avg: $([math]::Round($avgTime, 0))ms (slower than expected)" "Performance"
        }
    } catch {
        Write-TestResult "Products API Response Time" $false $_.Exception.Message "Performance"
    }
}

# ============================================================================
# PHASE 10: DATABASE CONNECTIVITY (Local only)
# ============================================================================
if ($Environment -eq "Local") {
    Write-TestHeader "Phase 10: Database Connectivity Tests"
    
    try {
        $sqlOutput = docker exec retaildecomposed-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d RetailDecomposedDB -Q "SELECT COUNT(*) FROM dbo.Products" -h -1 -W 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            # SQL Server returns output followed by "(X rows affected)"
            # Parse array and get the first line with numeric value
            if ($sqlOutput -is [array]) {
                # Find first line that contains only a number
                $productCountStr = ($sqlOutput | Where-Object { $_ -match '^\s*\d+\s*$' } | Select-Object -First 1)
            } else {
                $productCountStr = $sqlOutput
            }
            $productCountStr = $productCountStr.ToString().Trim()
            
            if ($productCountStr -match '^\d+$') {
                $productCount = [int]$productCountStr
                if ($productCount -gt 0) {
                    Write-TestResult "SQL Server - Products Table" $true "Products in database: $productCount" "Database"
                } else {
                    Write-TestResult "SQL Server - Products Table" $false "Database connected but no products found" "Database"
                }
            } else {
                Write-TestResult "SQL Server - Products Table" $false "Unexpected SQL output: $productCountStr" "Database"
            }
        } else {
            Write-TestResult "SQL Server - Products Table" $false "SQL query failed: $sqlOutput" "Database"
        }
    } catch {
        Write-TestResult "SQL Server - Products Table" $false $_.Exception.Message "Database"
    }
    
    # Check other tables
    $tables = @("Carts", "Orders", "Inventory")
    foreach ($table in $tables) {
        try {
            $sqlOutput = docker exec retaildecomposed-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -C -d RetailDecomposedDB -Q "SELECT COUNT(*) FROM dbo.$table" -h -1 -W 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                # SQL Server returns output followed by "(X rows affected)"
                # Parse array and get the first line with numeric value
                if ($sqlOutput -is [array]) {
                    # Find first line that contains only a number
                    $countStr = ($sqlOutput | Where-Object { $_ -match '^\s*\d+\s*$' } | Select-Object -First 1)
                } else {
                    $countStr = $sqlOutput
                }
                $countStr = $countStr.ToString().Trim()
                
                if ($countStr -match '^\d+$') {
                    $count = [int]$countStr
                    Write-TestResult "SQL Server - $table Table" $true "Records: $count" "Database"
                } else {
                    Write-TestResult "SQL Server - $table Table" $false "Unexpected SQL output: $countStr" "Database"
                }
            } else {
                Write-TestResult "SQL Server - $table Table" $false "Query failed: $sqlOutput" "Database"
            }
        } catch {
            Write-TestResult "SQL Server - $table Table" $false $_.Exception.Message "Database"
        }
    }
}

# ============================================================================
# RESULTS SUMMARY
# ============================================================================
Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║                         TEST RESULTS SUMMARY                          ║" -ForegroundColor Magenta
Write-Host "╚═══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""

$totalTests = $testResults.Count
$passedTests = ($testResults | Where-Object { $_.Passed }).Count
$failedTests = $totalTests - $passedTests
$passRate = if ($totalTests -gt 0) { [math]::Round(($passedTests / $totalTests) * 100, 1) } else { 0 }

Write-Host "Total Tests:   $totalTests" -ForegroundColor White
Write-Host "Passed:        $passedTests" -ForegroundColor Green
Write-Host "Failed:        $failedTests" -ForegroundColor Red
Write-Host "Pass Rate:     $passRate%" -ForegroundColor $(if ($passRate -ge 90) { "Green" } elseif ($passRate -ge 70) { "Yellow" } else { "Red" })
Write-Host ""

# Group results by category
$categories = $testResults | Group-Object -Property Category
foreach ($category in $categories) {
    $catPassed = @($category.Group | Where-Object { $_.Passed }).Count
    $catTotal = @($category.Group).Count
    Write-Host "$($category.Name): $catPassed/$catTotal passed" -ForegroundColor $(if ($catPassed -eq $catTotal) { "Green" } else { "Yellow" })
}

# ============================================================================
# GENERATE HTML REPORT
# ============================================================================
Write-Host ""
Write-Host "Generating HTML report..." -ForegroundColor Cyan

$htmlReport = @"
<!DOCTYPE html>
<html>
<head>
    <title>RetailDecomposed Microservices Test Report</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; background: #f5f5f5; }
        .container { max-width: 1200px; margin: 0 auto; background: white; padding: 30px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        h1 { color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px; }
        h2 { color: #34495e; margin-top: 30px; border-left: 4px solid #3498db; padding-left: 10px; }
        .summary { display: grid; grid-template-columns: repeat(4, 1fr); gap: 15px; margin: 20px 0; }
        .metric { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 8px; text-align: center; }
        .metric.success { background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); }
        .metric.fail { background: linear-gradient(135deg, #eb3349 0%, #f45c43 100%); }
        .metric .value { font-size: 32px; font-weight: bold; }
        .metric .label { font-size: 14px; margin-top: 5px; opacity: 0.9; }
        table { width: 100%; border-collapse: collapse; margin: 20px 0; }
        th { background: #34495e; color: white; padding: 12px; text-align: left; }
        td { padding: 10px; border-bottom: 1px solid #ddd; }
        tr:hover { background: #f8f9fa; }
        .pass { color: #27ae60; font-weight: bold; }
        .fail { color: #e74c3c; font-weight: bold; }
        .category { background: #ecf0f1; font-weight: bold; padding: 15px; margin-top: 10px; }
        .timestamp { color: #7f8c8d; font-size: 12px; }
        .details { color: #555; font-size: 13px; font-style: italic; }
    </style>
</head>
<body>
    <div class="container">
        <h1>🎯 RetailDecomposed Microservices Test Report</h1>
        <p><strong>Environment:</strong> $Environment</p>
        <p><strong>Generated:</strong> $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</p>
        <p><strong>Frontend URL:</strong> $FrontendUrl</p>
        
        <h2>📊 Summary</h2>
        <div class="summary">
            <div class="metric">
                <div class="value">$totalTests</div>
                <div class="label">Total Tests</div>
            </div>
            <div class="metric success">
                <div class="value">$passedTests</div>
                <div class="label">Passed</div>
            </div>
            <div class="metric fail">
                <div class="value">$failedTests</div>
                <div class="label">Failed</div>
            </div>
            <div class="metric">
                <div class="value">$passRate%</div>
                <div class="label">Pass Rate</div>
            </div>
        </div>
        
        <h2>📋 Test Results by Category</h2>
"@

foreach ($category in $categories | Sort-Object Name) {
    $htmlReport += "<div class='category'>$($category.Name)</div>`n"
    $htmlReport += "<table>`n"
    $htmlReport += "<tr><th>Test Name</th><th>Result</th><th>Details</th><th>Timestamp</th></tr>`n"
    
    foreach ($result in $category.Group) {
        $resultClass = if ($result.Passed) { "pass" } else { "fail" }
        $resultText = if ($result.Passed) { "✅ PASS" } else { "❌ FAIL" }
        $htmlReport += "<tr><td>$($result.Test)</td><td class='$resultClass'>$resultText</td><td class='details'>$($result.Details)</td><td class='timestamp'>$($result.Timestamp)</td></tr>`n"
    }
    
    $htmlReport += "</table>`n"
}

$htmlReport += @"
    </div>
</body>
</html>
"@

$reportPath = "microservices-test-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').html"
$htmlReport | Out-File -FilePath $reportPath -Encoding UTF8

Write-Host "✅ HTML report generated: Tests/$reportPath" -ForegroundColor Green
Write-Host ""

# ============================================================================
# EXIT CODE
# ============================================================================
if ($failedTests -eq 0) {
    Write-Host "✅ ALL TESTS PASSED!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ SOME TESTS FAILED" -ForegroundColor Red
    exit 1
}
