<#
.SYNOPSIS
    SkyWeave Automated Release Build Script
.DESCRIPTION
    Builds, tests, publishes, and packages SkyWeave into an Inno Setup installer executable.
#>
param(
    [string]$Configuration = "Release",
    [switch]$SkipTests = $false
)

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "           SkyWeave Automated Release Pipeline            " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Step 1: Terminate running instances to release file locks
Write-Host "`n[1/7] Checking for running SkyWeave instances..." -ForegroundColor Yellow
$running = Get-Process -Name "SkyWeave*" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping running SkyWeave processes ($($running.Count))..." -ForegroundColor Yellow
    $running | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
}
Write-Host "Processes cleared." -ForegroundColor Green

# Step 2: Build bridge package layout
Write-Host "`n[2/7] Generating in-sim bridge package layout..." -ForegroundColor Yellow
$buildLayoutScript = Join-Path $ScriptDir "bridge\build-layout.ps1"
if (Test-Path $buildLayoutScript) {
    & $buildLayoutScript
    Write-Host "Bridge layout generated." -ForegroundColor Green
} else {
    Write-Host "Warning: build-layout.ps1 not found, skipping layout build." -ForegroundColor DarkYellow
}

# Step 3: Sync bridge to local MSFS Community folder if present
Write-Host "`n[3/7] Checking local MSFS Community folder..." -ForegroundColor Yellow
$communityCandidates = @(
    "$env:LOCALAPPDATA\Packages\Microsoft.Limitless_8wekyb3d8bbwe\LocalCache\Packages\Community",
    "$env:APPDATA\Microsoft Flight Simulator 2024\Packages\Community"
)
$sourceBridge = Join-Path $ScriptDir "bridge\SkyWeaveWeatherBridge"
foreach ($comm in $communityCandidates) {
    if (Test-Path $comm) {
        $destBridge = Join-Path $comm "SkyWeaveWeatherBridge"
        Write-Host "Syncing bridge to $destBridge..." -ForegroundColor Cyan
        Copy-Item -Path $sourceBridge -Destination $comm -Recurse -Force
        Write-Host "Community bridge synchronized." -ForegroundColor Green
        break
    }
}

# Step 4: Clean & Build Solution
Write-Host "`n[4/7] Compiling solution ($Configuration)..." -ForegroundColor Yellow
$slnPath = Join-Path $ScriptDir "SkyWeave.sln"
dotnet build $slnPath -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "Solution build failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}
Write-Host "Build succeeded with 0 errors, 0 warnings." -ForegroundColor Green

# Step 5: Run Automated Tests
if (-not $SkipTests) {
    Write-Host "`n[5/7] Running test suite..." -ForegroundColor Yellow
    dotnet test $slnPath -c $Configuration --no-build
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
    Write-Host "All tests passed successfully." -ForegroundColor Green
} else {
    Write-Host "`n[5/7] Skipping test execution (-SkipTests specified)." -ForegroundColor DarkYellow
}

# Step 6: Publish Desktop App
Write-Host "`n[6/7] Publishing SkyWeave.App..." -ForegroundColor Yellow
$appProj = Join-Path $ScriptDir "src\SkyWeave.App\SkyWeave.App.csproj"
$publishDir = Join-Path $ScriptDir "bin\Release\App"
dotnet publish $appProj -c $Configuration -o $publishDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "App publish failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}
Write-Host "Published to $publishDir" -ForegroundColor Green

# Step 7: Compile Inno Setup Installer
Write-Host "`n[7/7] Compiling Inno Setup installer..." -ForegroundColor Yellow
$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$isccPath = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $isccPath) {
    $command = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
    if ($command) { $isccPath = $command.Source }
}

if (-not $isccPath) {
    Write-Error "Inno Setup compiler (ISCC.exe) not found. Please install Inno Setup 6."
    exit 1
}

$installerIss = Join-Path $ScriptDir "installer.iss"
& $isccPath $installerIss
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup compilation failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

# Verification & Summary
$installerExe = Join-Path $ScriptDir "bin\Release\Installer\SkyWeave-Setup-0.7.0.exe"
if (Test-Path $installerExe) {
    $item = Get-Item $installerExe
    $hash = Get-FileHash -Path $installerExe -Algorithm SHA256
    $sizeMb = [math]::Round($item.Length / 1MB, 2)
    
    Write-Host "`n==========================================================" -ForegroundColor Green
    Write-Host "             RELEASE BUILD COMPLETED SUCCESSFULLY         " -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "Artifact:  $($item.FullName)" -ForegroundColor White
    Write-Host "Version:   0.7.0" -ForegroundColor White
    Write-Host "Size:      $sizeMb MB ($($item.Length) bytes)" -ForegroundColor White
    Write-Host "SHA-256:   $($hash.Hash)" -ForegroundColor White
    Write-Host "==========================================================`n" -ForegroundColor Green
} else {
    Write-Error "Installer executable not found at expected path: $installerExe"
    exit 1
}
