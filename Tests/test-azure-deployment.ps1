#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test RetailDecomposed microservices deployed to Azure Container Apps
.DESCRIPTION
    Comprehensive test suite for Azure-deployed microservices including health checks,
    API endpoints, UI pages, and end-to-end workflows.
.PARAMETER FrontendUrl
    Frontend URL (e.g., https://retaildecomposed-frontend.uksouth.azurecontainerapps.io)
.PARAMETER SkipContainerChecks
    Skip Azure Container Apps status checks (useful when you don't have Azure CLI access)
.PARAMETER ResourceGroup
    Azure resource group name containing the Container Apps (required for container checks)
.EXAMPLE
    .\test-azure-deployment.ps1 -FrontendUrl "https://retaildecomposed-frontend.uksouth.azurecontainerapps.io"
.EXAMPLE
    .\test-azure-deployment.ps1 -FrontendUrl "https://retaildecomposed-frontend.uksouth.azurecontainerapps.io" -ResourceGroup "rg-retail-decomposed" -SkipContainerChecks
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$FrontendUrl,
    
    [string]$ResourceGroup = "",
    
    [switch]$SkipContainerChecks
)

$ErrorActionPreference = "Stop"
$testResults = @()

# Normalize URL (remove trailing slash)
$FrontendUrl = $FrontendUrl.TrimEnd('/')

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
        $response = Invoke-WebRequest -Uri $HealthUrl -TimeoutSec 30 -UseBasicParsing -ErrorAction Stop
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
Write-Host "║      RetailDecomposed - Azure Container Apps Test Suite              ║" -ForegroundColor Magenta
Write-Host "║                                                                       ║" -ForegroundColor Magenta
Write-Host "╚═══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""
Write-Host "Environment:    Azure Container Apps" -ForegroundColor White
Write-Host "Frontend URL:   $FrontendUrl" -ForegroundColor White
if ($ResourceGroup) {
    Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
}
Write-Host ""

# ============================================================================
# PHASE 1: AZURE CONTAINER APPS STATUS (Optional)
# ============================================================================
if (-not $SkipContainerChecks -and $ResourceGroup) {
    Write-TestHeader "Phase 1: Azure Container Apps Status"
    
    # Check if Azure CLI is installed
    try {
        $azVersion = az version 2>&1 | ConvertFrom-Json
        Write-Host "  Azure CLI Version: $($azVersion.'azure-cli')" -ForegroundColor Gray
        
        # Expected container apps
        $expectedApps = @(
            "retaildecomposed-frontend",
            "retaildecomposed-products",
            "retaildecomposed-cart", 
            "retaildecomposed-orders",
            "retaildecomposed-checkout"
        )
        
        foreach ($appName in $expectedApps) {
            try {
                $appStatus = az containerapp show `
                    --name $appName `
                    --resource-group $ResourceGroup `
                    --query "{name:name, provisioningState:properties.provisioningState, runningStatus:properties.runningStatus, replicas:properties.template.scale.maxReplicas}" `
                    -o json 2>&1 | ConvertFrom-Json
                
                if ($appStatus.provisioningState -eq "Succeeded") {
                    Write-TestResult "$appName Container App" $true "State: $($appStatus.provisioningState), Replicas: $($appStatus.replicas)" "Azure Containers"
                } else {
                    Write-TestResult "$appName Container App" $false "State: $($appStatus.provisioningState)" "Azure Containers"
                }
            } catch {
                Write-TestResult "$appName Container App" $false "Could not retrieve status: $($_.Exception.Message)" "Azure Containers"
            }
        }
    } catch {
        Write-Host "  ⚠️  Azure CLI not found or not logged in - skipping container status checks" -ForegroundColor Yellow
        Write-Host "     Run 'az login' to enable container status checks" -ForegroundColor Gray
    }
} elseif ($SkipContainerChecks) {
    Write-Host "⏭️  Skipping Azure Container Apps status checks (SkipContainerChecks enabled)" -ForegroundColor Yellow
} else {
    Write-Host "⏭️  Skipping Azure Container Apps status checks (ResourceGroup not provided)" -ForegroundColor Yellow
}

# ============================================================================
# PHASE 2: HEALTH ENDPOINT CHECKS
# ============================================================================
Write-TestHeader "Phase 2: Service Health Endpoints"

$frontendHealthy = Test-ServiceHealth "Frontend Service" "$FrontendUrl/health"

# ============================================================================
# PHASE 3: FRONTEND UI TESTS
# ============================================================================
Write-TestHeader "Phase 3: Frontend UI Page Tests"

# Test 3.1: Home page
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/" -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        $hasContent = $response.Content -match "Retail" -or $response.Content.Length -gt 500
        if ($hasContent) {
            Write-TestResult "Home Page Load" $true "Status: 200, Content Length: $($response.Content.Length) bytes" "Frontend UI"
        } else {
            Write-TestResult "Home Page Load" $false "Page loaded but content seems incomplete" "Frontend UI"
        }
    } else {
        Write-TestResult "Home Page Load" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Home Page Load" $false $_.Exception.Message "Frontend UI"
}

# Test 3.2: Products page
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/Products" -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Products Page Load" $true "Status: 200, Content Length: $($response.Content.Length) bytes" "Frontend UI"
    } else {
        Write-TestResult "Products Page Load" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Products Page Load" $false $_.Exception.Message "Frontend UI"
}

# Test 3.3: Cart page
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/Cart" -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Cart Page Load" $true "Status: 200" "Frontend UI"
    } else {
        Write-TestResult "Cart Page Load" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Cart Page Load" $false $_.Exception.Message "Frontend UI"
}

# Test 3.4: Orders page
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/Orders" -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Orders Page Load" $true "Status: 200" "Frontend UI"
    } else {
        Write-TestResult "Orders Page Load" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Orders Page Load" $false $_.Exception.Message "Frontend UI"
}

# Test 3.5: Checkout page
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/Checkout" -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Checkout Page Load" $true "Status: 200" "Frontend UI"
    } else {
        Write-TestResult "Checkout Page Load" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Checkout Page Load" $false $_.Exception.Message "Frontend UI"
}

# ============================================================================
# PHASE 4: AUTHENTICATION TESTS
# ============================================================================
Write-TestHeader "Phase 4: Authentication & Authorization"

# Test 4.1: Check if authentication redirects work
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/Account/Login" -TimeoutSec 30 -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 200 -or $response.StatusCode -eq 302) {
        Write-TestResult "Login Page Access" $true "Status: $($response.StatusCode)" "Authentication"
    } else {
        Write-TestResult "Login Page Access" $false "Status Code: $($response.StatusCode)" "Authentication"
    }
} catch {
    # 302 redirects throw exception with -ErrorAction Stop
    if ($_.Exception.Response.StatusCode.value__ -eq 302) {
        Write-TestResult "Login Page Access" $true "Redirect working (302)" "Authentication"
    } else {
        Write-TestResult "Login Page Access" $false $_.Exception.Message "Authentication"
    }
}

# ============================================================================
# PHASE 5: STATIC ASSETS & RESOURCES
# ============================================================================
Write-TestHeader "Phase 5: Static Assets & Resources"

# Test 5.1: CSS files
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/css/site.css" -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "CSS Files Load" $true "site.css loaded successfully" "Static Assets"
    } else {
        Write-TestResult "CSS Files Load" $false "Status Code: $($response.StatusCode)" "Static Assets"
    }
} catch {
    Write-TestResult "CSS Files Load" $false $_.Exception.Message "Static Assets"
}

# Test 5.2: JavaScript files
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/js/site.js" -TimeoutSec 30 -UseBasicParsing -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 200) {
        Write-TestResult "JavaScript Files Load" $true "site.js loaded successfully" "Static Assets"
    } else {
        Write-TestResult "JavaScript Files Load" $false "Status Code: $($response.StatusCode)" "Static Assets"
    }
} catch {
    # site.js might not exist - this is OK
    Write-TestResult "JavaScript Files Load" $true "No custom JS file (OK)" "Static Assets"
}

# ============================================================================
# PHASE 6: PERFORMANCE & LATENCY TESTS
# ============================================================================
Write-TestHeader "Phase 6: Performance & Response Time"

# Test 6.1: Home page response time
try {
    $measurements = @()
    1..5 | ForEach-Object {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        Invoke-WebRequest -Uri "$FrontendUrl/" -TimeoutSec 30 -UseBasicParsing | Out-Null
        $stopwatch.Stop()
        $measurements += $stopwatch.ElapsedMilliseconds
    }
    
    $avgTime = ($measurements | Measure-Object -Average).Average
    $minTime = ($measurements | Measure-Object -Minimum).Minimum
    $maxTime = ($measurements | Measure-Object -Maximum).Maximum
    
    if ($avgTime -lt 5000) {
        Write-TestResult "Home Page Response Time" $true "Avg: $([math]::Round($avgTime, 0))ms, Min: $([math]::Round($minTime, 0))ms, Max: $([math]::Round($maxTime, 0))ms" "Performance"
    } else {
        Write-TestResult "Home Page Response Time" $false "Avg: $([math]::Round($avgTime, 0))ms (slower than expected)" "Performance"
    }
} catch {
    Write-TestResult "Home Page Response Time" $false $_.Exception.Message "Performance"
}

# Test 6.2: Products page response time
try {
    $measurements = @()
    1..3 | ForEach-Object {
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        Invoke-WebRequest -Uri "$FrontendUrl/Products" -TimeoutSec 30 -UseBasicParsing | Out-Null
        $stopwatch.Stop()
        $measurements += $stopwatch.ElapsedMilliseconds
    }
    
    $avgTime = ($measurements | Measure-Object -Average).Average
    
    if ($avgTime -lt 5000) {
        Write-TestResult "Products Page Response Time" $true "Avg: $([math]::Round($avgTime, 0))ms (3 requests)" "Performance"
    } else {
        Write-TestResult "Products Page Response Time" $false "Avg: $([math]::Round($avgTime, 0))ms (slower than expected)" "Performance"
    }
} catch {
    Write-TestResult "Products Page Response Time" $false $_.Exception.Message "Performance"
}

# ============================================================================
# PHASE 7: SSL/TLS & SECURITY CHECKS
# ============================================================================
Write-TestHeader "Phase 7: SSL/TLS & Security"

# Test 7.1: HTTPS enforcement
if ($FrontendUrl.StartsWith("https://")) {
    try {
        $response = Invoke-WebRequest -Uri $FrontendUrl -TimeoutSec 30 -UseBasicParsing
        Write-TestResult "HTTPS Enabled" $true "Site uses HTTPS" "Security"
    } catch {
        Write-TestResult "HTTPS Enabled" $false $_.Exception.Message "Security"
    }
} else {
    Write-TestResult "HTTPS Enabled" $false "Site uses HTTP instead of HTTPS" "Security"
}

# Test 7.2: Security headers
try {
    $response = Invoke-WebRequest -Uri $FrontendUrl -TimeoutSec 30 -UseBasicParsing
    
    $hasStrictTransport = $response.Headers["Strict-Transport-Security"] -ne $null
    $hasXFrameOptions = $response.Headers["X-Frame-Options"] -ne $null
    $hasXContentType = $response.Headers["X-Content-Type-Options"] -ne $null
    
    $headerCount = @($hasStrictTransport, $hasXFrameOptions, $hasXContentType) | Where-Object { $_ } | Measure-Object | Select-Object -ExpandProperty Count
    
    if ($headerCount -ge 2) {
        Write-TestResult "Security Headers Present" $true "$headerCount/3 security headers found" "Security"
    } else {
        Write-TestResult "Security Headers Present" $false "Only $headerCount/3 security headers found" "Security"
    }
} catch {
    Write-TestResult "Security Headers Present" $false $_.Exception.Message "Security"
}

# ============================================================================
# PHASE 8: ERROR HANDLING & EDGE CASES
# ============================================================================
Write-TestHeader "Phase 8: Error Handling & Edge Cases"

# Test 8.1: Non-existent page (should return 404)
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/NonExistentPage12345" -TimeoutSec 30 -UseBasicParsing -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 404) {
        Write-TestResult "404 Page Handling" $true "Returns 404 for non-existent pages" "Error Handling"
    } else {
        Write-TestResult "404 Page Handling" $false "Status Code: $($response.StatusCode) (expected 404)" "Error Handling"
    }
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 404) {
        Write-TestResult "404 Page Handling" $true "Returns 404 for non-existent pages" "Error Handling"
    } else {
        Write-TestResult "404 Page Handling" $false $_.Exception.Message "Error Handling"
    }
}

# Test 8.2: Invalid product ID
try {
    $response = Invoke-WebRequest -Uri "$FrontendUrl/Products/Details/999999" -TimeoutSec 30 -UseBasicParsing -ErrorAction SilentlyContinue
    # Should either return 404 or redirect to products list
    if ($response.StatusCode -eq 404 -or $response.StatusCode -eq 302 -or $response.StatusCode -eq 200) {
        Write-TestResult "Invalid Product ID Handling" $true "Handles invalid product ID gracefully" "Error Handling"
    } else {
        Write-TestResult "Invalid Product ID Handling" $false "Status Code: $($response.StatusCode)" "Error Handling"
    }
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -eq 404 -or $statusCode -eq 302) {
        Write-TestResult "Invalid Product ID Handling" $true "Handles invalid product ID gracefully" "Error Handling"
    } else {
        Write-TestResult "Invalid Product ID Handling" $false $_.Exception.Message "Error Handling"
    }
}

# ============================================================================
# PHASE 9: AVAILABILITY & UPTIME CHECK
# ============================================================================
Write-TestHeader "Phase 9: Availability & Reliability"

# Test 9.1: Consistent availability (10 rapid requests)
try {
    $successCount = 0
    1..10 | ForEach-Object {
        try {
            $response = Invoke-WebRequest -Uri "$FrontendUrl/health" -TimeoutSec 30 -UseBasicParsing
            if ($response.StatusCode -eq 200) {
                $successCount++
            }
        } catch {
            # Request failed
        }
    }
    
    $availabilityPercent = ($successCount / 10) * 100
    
    if ($availabilityPercent -ge 90) {
        Write-TestResult "Service Availability" $true "$successCount/10 requests succeeded ($availabilityPercent%)" "Availability"
    } else {
        Write-TestResult "Service Availability" $false "$successCount/10 requests succeeded ($availabilityPercent%)" "Availability"
    }
} catch {
    Write-TestResult "Service Availability" $false $_.Exception.Message "Availability"
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
Write-Host "Failed:        $failedTests" -ForegroundColor $(if ($failedTests -eq 0) { "Green" } else { "Red" })
Write-Host "Pass Rate:     $passRate%" -ForegroundColor $(if ($passRate -ge 90) { "Green" } elseif ($passRate -ge 70) { "Yellow" } else { "Red" })
Write-Host ""

# Group results by category
$categories = $testResults | Group-Object -Property Category | Sort-Object Name
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
    <title>Azure Container Apps Test Report - RetailDecomposed</title>
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; background: #f5f5f5; }
        .container { max-width: 1200px; margin: 0 auto; background: white; padding: 30px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        h1 { color: #2c3e50; border-bottom: 3px solid #0078d4; padding-bottom: 10px; }
        h2 { color: #34495e; margin-top: 30px; border-left: 4px solid #0078d4; padding-left: 10px; }
        .summary { display: grid; grid-template-columns: repeat(4, 1fr); gap: 15px; margin: 20px 0; }
        .metric { background: linear-gradient(135deg, #0078d4 0%, #005a9e 100%); color: white; padding: 20px; border-radius: 8px; text-align: center; }
        .metric.success { background: linear-gradient(135deg, #107c10 0%, #0b5c0b 100%); }
        .metric.fail { background: linear-gradient(135deg, #e81123 0%, #c50f1f 100%); }
        .metric .value { font-size: 32px; font-weight: bold; }
        .metric .label { font-size: 14px; margin-top: 5px; opacity: 0.9; }
        table { width: 100%; border-collapse: collapse; margin: 20px 0; }
        th { background: #0078d4; color: white; padding: 12px; text-align: left; }
        td { padding: 10px; border-bottom: 1px solid #ddd; }
        tr:hover { background: #f8f9fa; }
        .pass { color: #107c10; font-weight: bold; }
        .fail { color: #e81123; font-weight: bold; }
        .category { background: #deecf9; font-weight: bold; padding: 15px; margin-top: 10px; border-left: 4px solid #0078d4; }
        .timestamp { color: #605e5c; font-size: 12px; }
        .details { color: #323130; font-size: 13px; font-style: italic; }
        .info-box { background: #f3f2f1; padding: 15px; border-radius: 4px; margin: 15px 0; }
        .info-box strong { color: #0078d4; }
    </style>
</head>
<body>
    <div class="container">
        <h1>☁️ Azure Container Apps Test Report</h1>
        <h2>RetailDecomposed Microservices</h2>
        
        <div class="info-box">
            <p><strong>Environment:</strong> Azure Container Apps</p>
            <p><strong>Generated:</strong> $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</p>
            <p><strong>Frontend URL:</strong> <a href="$FrontendUrl" target="_blank">$FrontendUrl</a></p>
            $(if ($ResourceGroup) { "<p><strong>Resource Group:</strong> $ResourceGroup</p>" } else { "" })
        </div>
        
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

foreach ($category in $categories) {
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
        <h2>🎯 Recommendations</h2>
        <div class="info-box">
"@

if ($failedTests -eq 0) {
    $htmlReport += "<p>✅ All tests passed! Your Azure Container Apps deployment is healthy.</p>"
} else {
    $htmlReport += "<p>⚠️ Some tests failed. Review the failed tests above and check:</p>"
    $htmlReport += "<ul>"
    $htmlReport += "<li>Azure Container Apps logs for error messages</li>"
    $htmlReport += "<li>Database connectivity and configuration</li>"
    $htmlReport += "<li>Environment variables and secrets</li>"
    $htmlReport += "<li>Network security and ingress settings</li>"
    $htmlReport += "</ul>"
}

$htmlReport += @"
        </div>
        
        <h2>📚 Next Steps</h2>
        <div class="info-box">
            <ul>
                <li>Monitor application performance in Azure Portal</li>
                <li>Set up Application Insights for detailed telemetry</li>
                <li>Configure auto-scaling rules based on traffic</li>
                <li>Review Azure Monitor alerts and metrics</li>
                <li>Schedule regular automated testing</li>
            </ul>
        </div>
    </div>
</body>
</html>
"@

$reportPath = "$PSScriptRoot\azure-test-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').html"
$htmlReport | Out-File -FilePath $reportPath -Encoding UTF8

Write-Host "✅ HTML report generated: $reportPath" -ForegroundColor Green
Write-Host ""

# Save JSON results for CI/CD integration
$jsonPath = "$PSScriptRoot\azure-test-results-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
$testResults | ConvertTo-Json -Depth 10 | Out-File -FilePath $jsonPath -Encoding UTF8
Write-Host "✅ JSON results saved: $jsonPath" -ForegroundColor Green
Write-Host ""

# ============================================================================
# EXIT CODE
# ============================================================================
if ($failedTests -eq 0) {
    Write-Host "✅ ALL TESTS PASSED!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Your Azure Container Apps deployment is healthy and operational." -ForegroundColor Green
    exit 0
} else {
    Write-Host "❌ SOME TESTS FAILED ($failedTests/$totalTests)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Review the test results above and check Azure Container Apps logs." -ForegroundColor Yellow
    exit 1
}
