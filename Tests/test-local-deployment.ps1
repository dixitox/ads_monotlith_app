#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Automated test script for RetailMonolith local deployment
.DESCRIPTION
    Tests all major functionality of the RetailMonolith application running in Docker Compose
.PARAMETER BaseUrl
    Base URL of the application (default: http://localhost:5068)
.EXAMPLE
    .\test-local-deployment.ps1
.EXAMPLE
    .\test-local-deployment.ps1 -BaseUrl "http://localhost:5069"
#>

param(
    [string]$BaseUrl = "http://localhost:5068"
)

$ErrorActionPreference = "Stop"
$testResults = @()

function Write-TestHeader {
    param([string]$Message)
    Write-Host "`n╔══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "╚══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
}

function Write-TestResult {
    param(
        [string]$TestName,
        [bool]$Passed,
        [string]$Details = ""
    )
    
    $result = @{
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

Write-TestHeader "RetailMonolith - Local Deployment Test Suite"
Write-Host "Base URL: $BaseUrl`n" -ForegroundColor White

# Test 1: Check if containers are running
Write-TestHeader "Test 1: Container Health Checks"
try {
    $containers = docker-compose ps --format json | ConvertFrom-Json
    
    # Check SQL Server
    $sqlContainer = $containers | Where-Object { $_.Service -eq "sqlserver" }
    if ($sqlContainer -and $sqlContainer.State -eq "running" -and $sqlContainer.Health -eq "healthy") {
        Write-TestResult "SQL Server Container" $true "Status: $($sqlContainer.State), Health: $($sqlContainer.Health)"
    } else {
        Write-TestResult "SQL Server Container" $false "Status: $($sqlContainer.State), Health: $($sqlContainer.Health)"
    }
    
    # Check Application
    $appContainer = $containers | Where-Object { $_.Service -eq "retail-monolith" }
    if ($appContainer -and $appContainer.State -eq "running") {
        Write-TestResult "Application Container" $true "Status: $($appContainer.State)"
    } else {
        Write-TestResult "Application Container" $false "Status: $($appContainer.State)"
    }
} catch {
    Write-TestResult "Container Status Check" $false $_.Exception.Message
}

# Test 2: Health endpoint
Write-TestHeader "Test 2: Application Health Endpoint"
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/health" -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Health Endpoint" $true "Status Code: $($response.StatusCode), Body: $($response.Content)"
    } else {
        Write-TestResult "Health Endpoint" $false "Status Code: $($response.StatusCode)"
    }
} catch {
    Write-TestResult "Health Endpoint" $false $_.Exception.Message
}

# Test 3: Home page
Write-TestHeader "Test 3: Home Page"
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/" -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -eq 200 -and $response.Content -match "Retail") {
        Write-TestResult "Home Page Load" $true "Status Code: $($response.StatusCode)"
    } else {
        Write-TestResult "Home Page Load" $false "Status Code: $($response.StatusCode)"
    }
} catch {
    Write-TestResult "Home Page Load" $false $_.Exception.Message
}

# Test 4: Products page
Write-TestHeader "Test 4: Products Page"
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/Products" -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -eq 200 -and $response.Content -match "Products") {
        $productCount = ([regex]::Matches($response.Content, "product-card|product-item")).Count
        Write-TestResult "Products Page Load" $true "Status Code: $($response.StatusCode), Products Found: ~$productCount"
    } else {
        Write-TestResult "Products Page Load" $false "Status Code: $($response.StatusCode)"
    }
} catch {
    Write-TestResult "Products Page Load" $false $_.Exception.Message
}

# Test 5: Cart page
Write-TestHeader "Test 5: Cart Page"
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/Cart" -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Cart Page Load" $true "Status Code: $($response.StatusCode)"
    } else {
        Write-TestResult "Cart Page Load" $false "Status Code: $($response.StatusCode)"
    }
} catch {
    Write-TestResult "Cart Page Load" $false $_.Exception.Message
}

# Test 6: Orders page
Write-TestHeader "Test 6: Orders Page"
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/Orders" -TimeoutSec 10 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Orders Page Load" $true "Status Code: $($response.StatusCode)"
    } else {
        Write-TestResult "Orders Page Load" $false "Status Code: $($response.StatusCode)"
    }
} catch {
    Write-TestResult "Orders Page Load" $false $_.Exception.Message
}

# Test 7: Database connectivity
Write-TestHeader "Test 7: Database Connectivity"
try {
    $sqlOutput = docker exec retail-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "YourStrong!Passw0rd" -Q "SELECT COUNT(*) FROM RetailMonolith.dbo.Products" -h -1 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        $productCount = $sqlOutput.Trim()
        if ([int]$productCount -gt 0) {
            Write-TestResult "Database Connection" $true "Products in database: $productCount"
        } else {
            Write-TestResult "Database Connection" $false "Database connected but no products found"
        }
    } else {
        Write-TestResult "Database Connection" $false "SQL query failed"
    }
} catch {
    Write-TestResult "Database Connection" $false $_.Exception.Message
}

# Test 8: Container logs (check for errors)
Write-TestHeader "Test 8: Container Logs Analysis"
try {
    $appLogs = docker-compose logs retail-monolith --tail=100 2>&1
    $errorCount = ($appLogs | Select-String -Pattern "error|exception|fail" -CaseSensitive:$false).Count
    
    if ($errorCount -eq 0) {
        Write-TestResult "Application Logs" $true "No errors found in recent logs"
    } else {
        Write-TestResult "Application Logs" $false "Found $errorCount potential errors in logs"
    }
} catch {
    Write-TestResult "Application Logs" $false $_.Exception.Message
}

# Test 9: Response time test
Write-TestHeader "Test 9: Response Time Test"
try {
    $measurements = @()
    1..5 | ForEach-Object {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $response = Invoke-WebRequest -Uri "$BaseUrl/" -TimeoutSec 10 -UseBasicParsing
        $stopwatch.Stop()
        $measurements += $stopwatch.ElapsedMilliseconds
    }
    
    $avgTime = ($measurements | Measure-Object -Average).Average
    if ($avgTime -lt 2000) {
        Write-TestResult "Response Time" $true "Average: $([math]::Round($avgTime, 2))ms (5 requests)"
    } else {
        Write-TestResult "Response Time" $false "Average: $([math]::Round($avgTime, 2))ms (slower than expected)"
    }
} catch {
    Write-TestResult "Response Time" $false $_.Exception.Message
}

# Test 10: Static files (CSS/JS)
Write-TestHeader "Test 10: Static Files"
try {
    $cssResponse = Invoke-WebRequest -Uri "$BaseUrl/css/site.css" -TimeoutSec 5 -UseBasicParsing
    if ($cssResponse.StatusCode -eq 200) {
        Write-TestResult "Static Files (CSS)" $true "Status Code: $($cssResponse.StatusCode)"
    } else {
        Write-TestResult "Static Files (CSS)" $false "Status Code: $($cssResponse.StatusCode)"
    }
} catch {
    Write-TestResult "Static Files (CSS)" $false $_.Exception.Message
}

# Summary
Write-TestHeader "Test Summary"
$passedTests = ($testResults | Where-Object { $_.Passed }).Count
$failedTests = ($testResults | Where-Object { -not $_.Passed }).Count
$totalTests = $testResults.Count

Write-Host "`n  Total Tests: $totalTests" -ForegroundColor White
Write-Host "  Passed: $passedTests" -ForegroundColor Green
Write-Host "  Failed: $failedTests" -ForegroundColor $(if ($failedTests -gt 0) { "Red" } else { "Green" })

$successRate = [math]::Round(($passedTests / $totalTests) * 100, 1)
Write-Host "  Success Rate: $successRate%" -ForegroundColor $(if ($successRate -ge 80) { "Green" } elseif ($successRate -ge 60) { "Yellow" } else { "Red" })

# Export results
$resultsFile = "test-results-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$testResults | ConvertTo-Json -Depth 10 | Out-File $resultsFile
Write-Host "`n  Results saved to: $resultsFile" -ForegroundColor Gray

# Exit code
if ($failedTests -eq 0) {
    Write-Host "`n🎉 All tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n⚠️  Some tests failed. Review the results above." -ForegroundColor Yellow
    exit 1
}
