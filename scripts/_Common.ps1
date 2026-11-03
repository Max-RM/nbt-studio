# Common helpers shared by the build scripts in this folder.
# Dot-source this file:  . (Join-Path $PSScriptRoot "_Common.ps1")

$ErrorActionPreference = "Stop"

$Script:ProjectRoot = Split-Path -Parent $PSScriptRoot
$Script:SolutionFile = Join-Path $Script:ProjectRoot "NbtStudio.sln"
$Script:GuiProject = Join-Path $Script:ProjectRoot "NbtStudio\NbtStudio.csproj"
$Script:TestProject = Join-Path $Script:ProjectRoot "NbtStudioTests\NbtStudioTests.csproj"

function Write-Step {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Yellow
}

function Write-Fail {
    param([string]$Message)
    Write-Host $Message -ForegroundColor Red
}

# Keeps the console window open so the user can read the output.
# Skipped when -NoPause is passed (useful for CI).
function Wait-ForExit {
    param([switch]$NoPause)
    if ($NoPause) { return }
    Write-Host ""
    Write-Host "Press any key to exit..." -ForegroundColor DarkGray
    try {
        $null = [System.Console]::ReadKey($true)
    }
    catch {
        try { $null = Read-Host }
        catch { Start-Sleep -Seconds 3 }
    }
}

# Returns a list of human readable problems. Empty list means everything is ready.
function Test-DotNetPrerequisites {
    $problems = New-Object System.Collections.Generic.List[string]

    $dotnet = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        $problems.Add("The 'dotnet' CLI was not found on PATH.")
        return $problems
    }

    $sdks = @()
    try { $sdks = @(& dotnet --list-sdks 2>$null) } catch { }
    if (-not $sdks -or $sdks.Count -eq 0) {
        $problems.Add("No .NET SDK is installed.")
    }
    else {
        $hasSupported = $false
        foreach ($line in $sdks) {
            if ($line -match '^(?<major>\d+)\.') {
                if ([int]$Matches['major'] -ge 6) { $hasSupported = $true }
            }
        }
        if (-not $hasSupported) {
            $problems.Add(".NET SDK 6.0 or newer is required (found: $($sdks -join '; ')).")
        }
    }

    # NBT Studio is a Windows Forms application.
    if ($env:OS -eq "Windows_NT") {
        $runtimes = @()
        try { $runtimes = @(& dotnet --list-runtimes 2>$null) } catch { }
        $hasDesktop = $false
        foreach ($line in $runtimes) {
            if ($line -match 'Microsoft\.WindowsDesktop\.App') { $hasDesktop = $true }
        }
        if (-not $hasDesktop) {
            $problems.Add("The 'Microsoft.WindowsDesktop.App' components are missing (required by the Windows Forms GUI).")
        }
    }

    return $problems
}

# Returns the paths listed in .gitmodules.
function Get-SubmodulePaths {
    $gitmodules = Join-Path $ProjectRoot ".gitmodules"
    if (-not (Test-Path -LiteralPath $gitmodules)) { return @() }

    $paths = @()
    foreach ($line in Get-Content -LiteralPath $gitmodules) {
        if ($line -match '^\s*path\s*=\s*(.+?)\s*$') {
            $paths += $Matches[1]
        }
    }
    return $paths
}

# Returns a list of missing or empty submodules.
function Test-Submodules {
    $problems = New-Object System.Collections.Generic.List[string]

    foreach ($relative in Get-SubmodulePaths) {
        $path = Join-Path $ProjectRoot $relative
        if (-not (Test-Path -LiteralPath $path)) {
            $problems.Add("The git submodule '$relative' is missing.")
            continue
        }
        $entries = @(Get-ChildItem -LiteralPath $path -Force -ErrorAction SilentlyContinue)
        if ($entries.Count -eq 0) {
            $problems.Add("The git submodule '$relative' is empty.")
        }
    }

    return $problems
}

function Show-PrerequisiteHelp {
    param($Problems)

    Write-Fail ""
    Write-Fail "Cannot start the build because some required components are missing:"
    foreach ($p in $Problems) { Write-Fail "  - $p" }
    Write-Host ""
    Write-Warn "How to fix:"
    Write-Host "  1. Install the .NET SDK 6.0 or newer: https://dotnet.microsoft.com/download"
    Write-Host "  2. If you use Visual Studio, open the Visual Studio Installer, click Modify and"
    Write-Host "     enable the workload: .NET desktop development"
    Write-Host "     (this provides the Windows Desktop / Windows Forms components)."
    Write-Host "  3. Make sure the git submodules are present:"
    Write-Host "       git submodule update --init --recursive"
}
