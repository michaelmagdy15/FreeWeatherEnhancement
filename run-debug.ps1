<#
.SYNOPSIS
    SkyWeave Unified Interactive Debug & Test Environment Runner
.DESCRIPTION
    Launches test injection scenarios, debugs SimConnect in real time, syncs in-sim bridge panels,
    and runs test suites without restarting Microsoft Flight Simulator.
#>

param (
    [string]$Scenario = "menu", # storm, fog, clear, highwinds, loop, app, api, sync, test
    [switch]$Loop
)

$Host.UI.RawUI.WindowTitle = "SkyWeave Simulation Debug Environment"

function Show-Header {
    Clear-Host
    Write-Host "===================================================================" -ForegroundColor Cyan
    Write-Host "         SKYWEAVE MSFS 2024 REAL-TIME DEBUG ENVIRONMENT            " -ForegroundColor Cyan
    Write-Host "===================================================================" -ForegroundColor Cyan
    
    # Check MSFS Process
    $simProc = Get-Process -Name FlightSimulator*, *Simulator*, FlightSimulator2024* -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($simProc) {
        Write-Host " [SIM STATUS] " -NoNewline -ForegroundColor Green
        Write-Host "MSFS is RUNNING (PID: $($simProc.Id), WorkingSet: $([math]::Round($simProc.WorkingSet64 / 1MB)) MB)" -ForegroundColor Green
    } else {
        Write-Host " [SIM STATUS] " -NoNewline -ForegroundColor Yellow
        Write-Host "MSFS is NOT detected (start MSFS and load cockpit to inject)" -ForegroundColor Yellow
    }

    # Check Community Folder
    $communityPath = "C:\Users\Mi5a\AppData\Local\Packages\Microsoft.Limitless_8wekyb3d8bbwe\LocalCache\Packages\Community\SkyWeaveWeatherBridge"
    if (Test-Path $communityPath) {
        Write-Host " [BRIDGE]     " -NoNewline -ForegroundColor Green
        Write-Host "In-Sim Panel installed in Community folder" -ForegroundColor Green
    } else {
        Write-Host " [BRIDGE]     " -NoNewline -ForegroundColor Red
        Write-Host "In-Sim Panel NOT installed in Community folder" -ForegroundColor Red
    }
    Write-Host "-------------------------------------------------------------------" -ForegroundColor DarkGray
}

function Sync-Bridge {
    Write-Host "`n[SYNC] Syncing SkyWeaveWeatherBridge into MSFS Community Folder..." -ForegroundColor Cyan
    $src = "c:\Users\Mi5a\FreeWeatherEnhancement\bridge\SkyWeaveWeatherBridge\*"
    $dest = "C:\Users\Mi5a\AppData\Local\Packages\Microsoft.Limitless_8wekyb3d8bbwe\LocalCache\Packages\Community\SkyWeaveWeatherBridge\"
    
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item -Recurse -Force $src $dest
    Remove-Item -Force "$dest\InGamePanels\skyweave-weather-bridge.spb" -ErrorAction SilentlyContinue

    Write-Host "[SYNC] Sync complete! Toggle the toolbar icon in MSFS to reload the panel." -ForegroundColor Green
}

function Run-Scenario([string]$preset, [bool]$continuous) {
    Write-Host "`n[INJECTOR] Launching scenario [$preset]..." -ForegroundColor Cyan
    $args = @()
    if ($preset -eq "live") { $args += "--live" }
    else { $args += "--preset=$preset" }
    if ($continuous) { $args += "--loop" }
    dotnet run --project src/SkyWeave.TestInjector -- $args
}

function Run-App {
    Write-Host "`n[APP] Launching SkyWeave Desktop Application (Avalonia UI)..." -ForegroundColor Cyan
    dotnet run --project src/SkyWeave.App
}

function Run-Api {
    Write-Host "`n[API] Launching SkyWeave REST API Server on http://localhost:54170..." -ForegroundColor Cyan
    dotnet run --project src/SkyWeave.Api
}

function Run-Tests {
    Write-Host "`n[TEST] Running full automated test suite..." -ForegroundColor Cyan
    dotnet test --verbosity normal
}

function Launch-Sim {
    Write-Host "`n[MSFS] Launching Microsoft Flight Simulator 2024 with -FastLaunch..." -ForegroundColor Green
    Start-Process "shell:AppsFolder\Microsoft.Limitless_8wekyb3d8bbwe!App" -ArgumentList "-FastLaunch"
}

# Direct switch execution
if ($Scenario -ne "menu") {
    Show-Header
    switch ($Scenario.ToLowerInvariant()) {
        "launch"    { Launch-Sim }
        "live"      { Run-Scenario "live" $Loop }
        "storm"     { Run-Scenario "storm" $Loop }
        "fog"       { Run-Scenario "fog" $Loop }
        "clear"     { Run-Scenario "clear" $Loop }
        "highwinds" { Run-Scenario "highwinds" $Loop }
        "loop"      { Run-Scenario "live" $true }
        "app"       { Run-App }
        "api"       { Run-Api }
        "sync"      { Sync-Bridge }
        "test"      { Run-Tests }
        default     { Run-Scenario "live" $false }
    }
    exit
}

# Interactive Menu
while ($true) {
    Show-Header
    Write-Host "Select a debug action:" -ForegroundColor White
    Write-Host " [S] Launch MSFS 2024 (-FastLaunch)" -ForegroundColor Green
    Write-Host " [1] INJECT LIVE REAL-WORLD WEATHER (Live METAR, Winds Aloft & All Cloud Layers)" -ForegroundColor Green
    Write-Host " [2] Inject Scenario: Thunderstorm & Heavy Rain (QNH 29.65 / 1004 hPa / CB clouds)" -ForegroundColor Yellow
    Write-Host " [3] Inject Scenario: Low Ceiling & Heavy Fog / LIFR (QNH 30.18 / 1022 hPa / Stratus)" -ForegroundColor Gray
    Write-Host " [4] Inject Scenario: High Winds Aloft & Mountain Wave (QNH 29.47 / 998 hPa / 125kt jet)" -ForegroundColor Magenta
    Write-Host " [5] Inject Scenario: Clear Skies VFR (QNH 30.02 / 1016.6 hPa / 0 clouds)" -ForegroundColor White
    Write-Host " [6] Continuous Live Weather Loop (Injects & interpolates every 10s dynamically)" -ForegroundColor Cyan
    Write-Host " [7] Launch SkyWeave Desktop App (Full GUI)" -ForegroundColor DarkCyan
    Write-Host " [8] Launch Local Weather API Server (http://localhost:54170)" -ForegroundColor DarkMagenta
    Write-Host " [9] Fast Re-Sync In-Sim Panel to MSFS Community Folder" -ForegroundColor Blue
    Write-Host " [T] Run All Unit Tests (127 Tests)" -ForegroundColor DarkGreen
    Write-Host " [0] Exit" -ForegroundColor DarkGray
    Write-Host "-------------------------------------------------------------------" -ForegroundColor DarkGray

    $choice = Read-Host "Enter option [0-9, S, or T]"
    switch ($choice.ToUpperInvariant()) {
        "S" { Launch-Sim; Read-Host "`nPress Enter to continue..." }
        "1" { Run-Scenario "live" $false; Read-Host "`nPress Enter to continue..." }
        "2" { Run-Scenario "storm" $false; Read-Host "`nPress Enter to continue..." }
        "3" { Run-Scenario "fog" $false; Read-Host "`nPress Enter to continue..." }
        "4" { Run-Scenario "highwinds" $false; Read-Host "`nPress Enter to continue..." }
        "5" { Run-Scenario "clear" $false; Read-Host "`nPress Enter to continue..." }
        "6" { Run-Scenario "live" $true; Read-Host "`nPress Enter to continue..." }
        "7" { Run-App }
        "8" { Run-Api }
        "9" { Sync-Bridge; Read-Host "`nPress Enter to continue..." }
        "T" { Run-Tests; Read-Host "`nPress Enter to continue..." }
        "0" { break }
        default { Write-Host "Invalid option" -ForegroundColor Red; Start-Sleep -Seconds 1 }
    }
}
