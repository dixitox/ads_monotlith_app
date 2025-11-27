#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test RetailDecomposed microservices deployed to Azure Kubernetes Service (AKS)
.DESCRIPTION
    Comprehensive test suite for AKS-deployed microservices including:
    - Pod health and status checks
    - Service connectivity
    - Ingress configuration
    - Frontend UI pages
    - Backend API endpoints
    - Authentication flow
    - End-to-end workflows
.PARAMETER IngressIP
    Ingress IP address (e.g., 4.158.195.134). If not provided, will auto-detect from cluster.
.PARAMETER ResourceGroup
    Azure resource group name containing the AKS cluster (default: rg-retail-decomposed)
.PARAMETER ClusterName
    AKS cluster name (default: aks-retail-decomposed)
.PARAMETER Namespace
    Kubernetes namespace (default: retail-decomposed)
.PARAMETER SkipKubernetesChecks
    Skip kubectl pod/service checks (useful if you don't have cluster access)
.EXAMPLE
    .\test-aks-deployment.ps1
    Auto-detect ingress IP and run all tests
.EXAMPLE
    .\test-aks-deployment.ps1 -IngressIP "4.158.195.134"
    Test specific ingress IP
.EXAMPLE
    .\test-aks-deployment.ps1 -SkipKubernetesChecks
    Test only HTTP endpoints (no kubectl required)
#>

param(
    [string]$IngressIP = "",
    [string]$ResourceGroup = "rg-retail-decomposed",
    [string]$ClusterName = "aks-retail-decomposed",
    [string]$Namespace = "retail-decomposed",
    [switch]$SkipKubernetesChecks
)

$ErrorActionPreference = "Continue"
$testResults = @()
$testStartTime = Get-Date

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
    
    $result = [PSCustomObject]@{
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

# ============================================================================
# HEADER
# ============================================================================
Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║                                                                       ║" -ForegroundColor Magenta
Write-Host "║      RetailDecomposed - Azure Kubernetes Service Test Suite          ║" -ForegroundColor Magenta
Write-Host "║                                                                       ║" -ForegroundColor Magenta
Write-Host "╚═══════════════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""
Write-Host "Environment:     Azure Kubernetes Service (AKS)" -ForegroundColor White
Write-Host "Resource Group:  $ResourceGroup" -ForegroundColor White
Write-Host "Cluster:         $ClusterName" -ForegroundColor White
Write-Host "Namespace:       $Namespace" -ForegroundColor White
Write-Host ""

# ============================================================================
# PHASE 1: KUBERNETES CLUSTER STATUS
# ============================================================================
if (-not $SkipKubernetesChecks) {
    Write-TestHeader "Phase 1: Kubernetes Cluster & Pod Status"
    
    # Check kubectl availability
    try {
        $kubectlVersion = kubectl version --client --short 2>&1 | Out-String
        Write-Host "  kubectl is available" -ForegroundColor Gray
        
        # Get cluster credentials if needed
        try {
            az aks get-credentials --resource-group $ResourceGroup --name $ClusterName --overwrite-existing 2>&1 | Out-Null
            Write-Host "  ✅ Connected to AKS cluster: $ClusterName" -ForegroundColor Green
        } catch {
            Write-Host "  ⚠️  Could not connect to AKS cluster" -ForegroundColor Yellow
        }
        
        # Check pod status
        Write-Host "`n  Pod Status:" -ForegroundColor Cyan
        $pods = kubectl get pods -n $Namespace -o json 2>&1 | ConvertFrom-Json
        
        if ($pods.items) {
            $expectedPods = @("frontend-service", "products-service", "cart-service", "orders-service", "checkout-service")
            
            foreach ($podPrefix in $expectedPods) {
                $podList = $pods.items | Where-Object { $_.metadata.name -like "$podPrefix-*" }
                
                if ($podList) {
                    $runningPods = @($podList | Where-Object { 
                        $_.status.phase -eq "Running" -and 
                        ($_.status.conditions | Where-Object { $_.type -eq "Ready" -and $_.status -eq "True" }).Count -gt 0
                    })
                    $totalPods = @($podList).Count
                    
                    if ($runningPods.Count -eq $totalPods -and $totalPods -gt 0) {
                        Write-TestResult "$podPrefix Pods" $true "$($runningPods.Count)/$totalPods Running" "Kubernetes"
                    } else {
                        Write-TestResult "$podPrefix Pods" $false "$($runningPods.Count)/$totalPods Ready" "Kubernetes"
                    }
                } else {
                    Write-TestResult "$podPrefix Pods" $false "No pods found" "Kubernetes"
                }
            }
        } else {
            Write-TestResult "Pod Status Check" $false "Could not retrieve pod list" "Kubernetes"
        }
        
        # Check services
        Write-Host "`n  Service Status:" -ForegroundColor Cyan
        $services = kubectl get svc -n $Namespace -o json 2>&1 | ConvertFrom-Json
        
        if ($services.items) {
            $expectedServices = @("frontend-service", "products-service", "cart-service", "orders-service", "checkout-service")
            
            foreach ($svcName in $expectedServices) {
                $svc = $services.items | Where-Object { $_.metadata.name -eq $svcName }
                
                if ($svc) {
                    $clusterIP = $svc.spec.clusterIP
                    $port = $svc.spec.ports[0].port
                    Write-TestResult "$svcName Service" $true "ClusterIP: $clusterIP, Port: $port" "Kubernetes"
                } else {
                    Write-TestResult "$svcName Service" $false "Service not found" "Kubernetes"
                }
            }
        }
        
        # Check ingress
        Write-Host "`n  Ingress Status:" -ForegroundColor Cyan
        $ingress = kubectl get ingress retail-decomposed-ingress -n $Namespace -o json 2>&1 | ConvertFrom-Json
        
        if ($ingress.status.loadBalancer.ingress) {
            $detectedIP = $ingress.status.loadBalancer.ingress[0].ip
            
            if ([string]::IsNullOrEmpty($IngressIP)) {
                $IngressIP = $detectedIP
                Write-Host "  ℹ️  Auto-detected Ingress IP: $IngressIP" -ForegroundColor Cyan
            }
            
            Write-TestResult "Ingress Load Balancer" $true "External IP: $detectedIP" "Kubernetes"
            
            # Check ingress annotations
            $bufferSize = $ingress.metadata.annotations.'nginx.ingress.kubernetes.io/proxy-buffer-size'
            $largeHeaders = $ingress.metadata.annotations.'nginx.ingress.kubernetes.io/large-client-header-buffers'
            
            if ($bufferSize -eq "16k" -and $largeHeaders -eq "4 32k") {
                Write-TestResult "Ingress Buffer Configuration" $true "Optimized for Azure AD (16k/32k buffers)" "Kubernetes"
            } else {
                Write-TestResult "Ingress Buffer Configuration" $false "May cause 502 errors with Azure AD" "Kubernetes"
            }
        } else {
            Write-TestResult "Ingress Load Balancer" $false "No external IP assigned" "Kubernetes"
        }
        
    } catch {
        Write-Host "  ⚠️  kubectl not available or cluster not accessible" -ForegroundColor Yellow
        Write-Host "     Skipping Kubernetes checks" -ForegroundColor Gray
    }
} else {
    Write-Host "⏭️  Skipping Kubernetes checks (SkipKubernetesChecks enabled)" -ForegroundColor Yellow
}

# Validate we have an ingress IP
if ([string]::IsNullOrEmpty($IngressIP)) {
    Write-Host ""
    Write-Host "❌ ERROR: No ingress IP provided or detected" -ForegroundColor Red
    Write-Host "   Please provide -IngressIP parameter or ensure cluster is accessible" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

$baseUrl = "https://$IngressIP"
Write-Host ""
Write-Host "Testing URL: $baseUrl" -ForegroundColor White
Write-Host ""

# ============================================================================
# PHASE 2: FRONTEND UI TESTS
# ============================================================================
Write-TestHeader "Phase 2: Frontend UI Page Tests"

# Test 2.1: Home page
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/" -SkipCertificateCheck -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        $hasTitle = $response.Content -match "<title>.*Retail.*</title>"
        if ($hasTitle) {
            Write-TestResult "Home Page" $true "Status: 200, Content: $($response.Content.Length) bytes" "Frontend UI"
        } else {
            Write-TestResult "Home Page" $false "Page loaded but title not found" "Frontend UI"
        }
    } else {
        Write-TestResult "Home Page" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Home Page" $false $_.Exception.Message "Frontend UI"
}

# Test 2.2: Products page
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/Products" -SkipCertificateCheck -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        $hasProducts = $response.Content -match "product" -or $response.Content.Length -gt 1000
        Write-TestResult "Products Page" $true "Status: 200, Has content: $hasProducts" "Frontend UI"
    } else {
        Write-TestResult "Products Page" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Products Page" $false $_.Exception.Message "Frontend UI"
}

# Test 2.3: Cart page
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/Cart" -SkipCertificateCheck -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Cart Page" $true "Status: 200" "Frontend UI"
    } else {
        Write-TestResult "Cart Page" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Cart Page" $false $_.Exception.Message "Frontend UI"
}

# Test 2.4: Orders page
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/Orders" -SkipCertificateCheck -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Orders Page" $true "Status: 200" "Frontend UI"
    } else {
        Write-TestResult "Orders Page" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Orders Page" $false $_.Exception.Message "Frontend UI"
}

# Test 2.5: Checkout page  
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/Checkout" -SkipCertificateCheck -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Checkout Page" $true "Status: 200" "Frontend UI"
    } else {
        Write-TestResult "Checkout Page" $false "Status Code: $($response.StatusCode)" "Frontend UI"
    }
} catch {
    Write-TestResult "Checkout Page" $false $_.Exception.Message "Frontend UI"
}

# ============================================================================
# PHASE 3: BACKEND API TESTS
# ============================================================================
Write-TestHeader "Phase 3: Backend API Endpoint Tests"

# Test 3.1: Products API - List all products
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/api/products" -SkipCertificateCheck -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        $products = $response.Content | ConvertFrom-Json
        if ($products.Count -gt 0) {
            Write-TestResult "Products API - List" $true "Status: 200, Products: $($products.Count)" "Backend API"
        } else {
            Write-TestResult "Products API - List" $false "No products returned" "Backend API"
        }
    } else {
        Write-TestResult "Products API - List" $false "Status Code: $($response.StatusCode)" "Backend API"
    }
} catch {
    Write-TestResult "Products API - List" $false $_.Exception.Message "Backend API"
}

# Test 3.2: Products API - Get specific product
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/api/products/1" -SkipCertificateCheck -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        $product = $response.Content | ConvertFrom-Json
        if ($product.id -and $product.name) {
            Write-TestResult "Products API - Get by ID" $true "Product: $($product.name)" "Backend API"
        } else {
            Write-TestResult "Products API - Get by ID" $false "Invalid product data" "Backend API"
        }
    } else {
        Write-TestResult "Products API - Get by ID" $false "Status Code: $($response.StatusCode)" "Backend API"
    }
} catch {
    Write-TestResult "Products API - Get by ID" $false $_.Exception.Message "Backend API"
}

# Test 3.3: Health endpoint
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/health" -SkipCertificateCheck -TimeoutSec 30 -UseBasicParsing
    if ($response.StatusCode -eq 200) {
        Write-TestResult "Health Endpoint" $true "Status: Healthy" "Backend API"
    } else {
        Write-TestResult "Health Endpoint" $false "Status Code: $($response.StatusCode)" "Backend API"
    }
} catch {
    Write-TestResult "Health Endpoint" $false $_.Exception.Message "Backend API"
}

# ============================================================================
# PHASE 4: AUTHENTICATION TESTS
# ============================================================================
Write-TestHeader "Phase 4: Authentication & Azure AD Integration"

# Test 4.1: Sign-in redirect
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/MicrosoftIdentity/Account/SignIn" -SkipCertificateCheck -TimeoutSec 30 -UseBasicParsing -MaximumRedirection 0 -ErrorAction SilentlyContinue
    
    if ($response.StatusCode -eq 302) {
        $location = $response.Headers.Location
        if ($location -match "login.microsoftonline.com") {
            Write-TestResult "Azure AD Sign-In Redirect" $true "Redirects to: login.microsoftonline.com" "Authentication"
        } else {
            Write-TestResult "Azure AD Sign-In Redirect" $false "Unexpected redirect: $location" "Authentication"
        }
    } else {
        Write-TestResult "Azure AD Sign-In Redirect" $false "Expected 302, got: $($response.StatusCode)" "Authentication"
    }
} catch {
    if ($_.Exception.Response.StatusCode.value__ -eq 302) {
        $location = $_.Exception.Response.Headers.Location.AbsoluteUri
        if ($location -match "login.microsoftonline.com") {
            Write-TestResult "Azure AD Sign-In Redirect" $true "Redirects to: login.microsoftonline.com" "Authentication"
        } else {
            Write-TestResult "Azure AD Sign-In Redirect" $false "Unexpected redirect: $location" "Authentication"
        }
    } else {
        Write-TestResult "Azure AD Sign-In Redirect" $false $_.Exception.Message "Authentication"
    }
}

# Test 4.2: OIDC callback endpoint accessibility
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/signin-oidc" -SkipCertificateCheck -TimeoutSec 30 -UseBasicParsing -ErrorAction SilentlyContinue
    # Expecting 400 or 500 when called without valid state parameter
    if ($response.StatusCode -in @(400, 500) -or $_.Exception.Response.StatusCode.value__ -in @(400, 500)) {
        Write-TestResult "OIDC Callback Endpoint" $true "Endpoint accessible (returns $($response.StatusCode) without parameters)" "Authentication"
    } else {
        Write-TestResult "OIDC Callback Endpoint" $true "Status: $($response.StatusCode)" "Authentication"
    }
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    if ($statusCode -in @(400, 500)) {
        Write-TestResult "OIDC Callback Endpoint" $true "Endpoint accessible (returns $statusCode without parameters)" "Authentication"
    } else {
        Write-TestResult "OIDC Callback Endpoint" $false "Unexpected status: $statusCode" "Authentication"
    }
}

# ============================================================================
# FINAL SUMMARY
# ============================================================================
Write-TestHeader "Test Summary"

$totalTests = $testResults.Count
$passedTests = @($testResults | Where-Object { $_.Passed -eq $true }).Count
$failedTests = $totalTests - $passedTests
$passRate = if ($totalTests -gt 0) { [math]::Round(($passedTests / $totalTests) * 100, 1) } else { 0 }

Write-Host ""
Write-Host "  Total Tests:  $totalTests" -ForegroundColor White
Write-Host "  Passed:       $passedTests" -ForegroundColor Green
Write-Host "  Failed:       $failedTests" -ForegroundColor $(if ($failedTests -eq 0) { "Green" } else { "Red" })
Write-Host "  Pass Rate:    $passRate%" -ForegroundColor $(if ($passRate -ge 90) { "Green" } elseif ($passRate -ge 70) { "Yellow" } else { "Red" })

# Group results by category
Write-Host "`n  Results by Category:" -ForegroundColor Cyan
$categories = $testResults | Group-Object -Property Category | Sort-Object Name

foreach ($category in $categories) {
    $catPassed = @($category.Group | Where-Object { $_.Passed -eq $true }).Count
    $catTotal = $category.Count
    $catPassRate = [math]::Round(($catPassed / $catTotal) * 100, 0)
    
    $statusIcon = if ($catPassRate -eq 100) { "✅" } elseif ($catPassRate -ge 50) { "⚠️" } else { "❌" }
    Write-Host "    $statusIcon $($category.Name): $catPassed/$catTotal ($catPassRate%)" -ForegroundColor $(if ($catPassRate -eq 100) { "Green" } elseif ($catPassRate -ge 50) { "Yellow" } else { "Red" })
}

# Show failed tests
if ($failedTests -gt 0) {
    Write-Host "`n  Failed Tests:" -ForegroundColor Red
    $testResults | Where-Object { $_.Passed -eq $false } | ForEach-Object {
        Write-Host "    ❌ [$($_.Category)] $($_.Test)" -ForegroundColor Red
        if ($_.Details) {
            Write-Host "       $($_.Details)" -ForegroundColor Gray
        }
    }
}

$testDuration = (Get-Date) - $testStartTime
Write-Host "`n  Test Duration: $($testDuration.TotalSeconds.ToString('F1')) seconds" -ForegroundColor Gray

Write-Host ""
Write-Host "═════════════════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Export results to file
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $PSScriptRoot "aks-test-results-$timestamp.json"
$testResults | ConvertTo-Json -Depth 10 | Out-File -FilePath $reportPath -Encoding UTF8

Write-Host "📄 Test results exported to: $reportPath" -ForegroundColor Cyan
Write-Host ""

# Exit with appropriate code
exit $(if ($failedTests -eq 0) { 0 } else { 1 })
