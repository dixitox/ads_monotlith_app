#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs both RetailMonolith and RetailDecomposed applications simultaneously.
.DESCRIPTION
    This script launches both applications in separate processes, displaying their output in the console.
    Press Ctrl+C to stop both applications.
.EXAMPLE
    .\run-both-apps.ps1
#>

Write-Host ""
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "  Starting both applications (HTTP mode)..." -ForegroundColor Cyan
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""
Write-Host "  RetailMonolith will run on: " -NoNewline
Write-Host "http://localhost:5068" -ForegroundColor Green
Write-Host "  RetailDecomposed will run on: " -NoNewline
Write-Host "http://localhost:6068" -ForegroundColor Green
Write-Host ""
Write-Host "  Note: Applications use the default HTTP launch profiles." -ForegroundColor DarkGray
Write-Host "  To use HTTPS, modify the script to specify --launch-profile https" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Press Ctrl+C to stop both applications" -ForegroundColor Yellow
Write-Host ("=" * 80) -ForegroundColor DarkGray
Write-Host ""

# Start RetailMonolith in the background
$monolithJob = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    dotnet run --project .\RetailMonolith.csproj
}

# Start RetailDecomposed in the background
$decomposedJob = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    dotnet run --project .\RetailDecomposed\RetailDecomposed.csproj
}

# Wait a bit for apps to start
Start-Sleep -Seconds 3

Write-Host "Both applications are starting up..." -ForegroundColor Cyan
Write-Host ""

# Function to display job output with color coding
function Show-JobOutput {
    param($Job, $AppName, $Color)
    
    $output = Receive-Job -Job $Job
    if ($output) {
        foreach ($line in $output) {
            Write-Host "[$AppName] " -ForegroundColor $Color -NoNewline
            Write-Host $line
        }
    }
}

# Monitor both jobs and display their output
try {
    while ($monolithJob.State -eq 'Running' -or $decomposedJob.State -eq 'Running') {
        Show-JobOutput -Job $monolithJob -AppName "RetailMonolith" -Color "Magenta"
        Show-JobOutput -Job $decomposedJob -AppName "RetailDecomposed" -Color "Blue"
        Start-Sleep -Milliseconds 500
    }
}
finally {
    Write-Host ""
    Write-Host ("=" * 80) -ForegroundColor Yellow
    Write-Host "  Stopping applications..." -ForegroundColor Yellow
    Write-Host ("=" * 80) -ForegroundColor Yellow
    
    # Stop both jobs
    Stop-Job -Job $monolithJob -ErrorAction SilentlyContinue
    Stop-Job -Job $decomposedJob -ErrorAction SilentlyContinue
    
    # Remove jobs
    Remove-Job -Job $monolithJob -Force -ErrorAction SilentlyContinue
    Remove-Job -Job $decomposedJob -Force -ErrorAction SilentlyContinue
    
    Write-Host ""
    Write-Host "  Both applications stopped." -ForegroundColor Green
    Write-Host ""
    Write-Host "  Application URLs:" -ForegroundColor Cyan
    Write-Host "    • RetailMonolith:   " -NoNewline
    Write-Host "http://localhost:5068" -ForegroundColor Magenta
    Write-Host "    • RetailDecomposed: " -NoNewline
    Write-Host "http://localhost:6068" -ForegroundColor Blue
    Write-Host ""
}
