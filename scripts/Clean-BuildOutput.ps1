param(
    [switch]$IncludeArchives,
    [switch]$NoPause
)

# Removes .NET compile caches (obj) and build outputs (bin), and clears the
# local Publish folder created by Publish-All.ps1 while keeping the folder
# itself. Archives are kept unless -IncludeArchives is passed, in which case
# their contents are cleared and the folder is kept as well.

. (Join-Path $PSScriptRoot "_Common.ps1")

function Remove-Directories {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Filter,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $targets = @(Get-ChildItem -LiteralPath $Root -Directory -Recurse -Force -Filter $Filter -ErrorAction SilentlyContinue)
    if ($targets.Count -eq 0) {
        Write-Host "No $Label folders found."
        return
    }

    foreach ($dir in $targets) {
        try {
            Remove-Item -LiteralPath $dir.FullName -Recurse -Force -ErrorAction Stop
            Write-Host "  removed: $($dir.FullName)"
        }
        catch {
            Write-Warn "  could not remove: $($dir.FullName)"
        }
    }
}

function Clear-Folder {
    param(
        [Parameter(Mandatory = $true)][string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
        Write-Host "  created empty: $Path"
        return
    }

    $items = @(Get-ChildItem -LiteralPath $Path -Force -ErrorAction SilentlyContinue)
    if ($items.Count -eq 0) {
        Write-Host "  already empty: $Path"
        return
    }

    $failedItems = 0
    foreach ($item in $items) {
        try {
            Remove-Item -LiteralPath $item.FullName -Recurse -Force -ErrorAction Stop
            Write-Host "  removed: $($item.FullName)"
        }
        catch {
            Write-Warn "  could not remove: $($item.FullName)"
            $failedItems++
        }
    }

    if ($failedItems -gt 0) {
        Write-Warn "  kept folder, but $failedItems item(s) could not be removed: $Path"
    }
    else {
        Write-Host "  kept empty folder: $Path"
    }
}

$failed = $false
try {
    Write-Step "=== Cleaning build output ==="

    Write-Host "Removing compile caches (obj)..."
    Remove-Directories -Root $ProjectRoot -Filter "obj" -Label "obj"

    Write-Host "Removing build outputs (bin)..."
    Remove-Directories -Root $ProjectRoot -Filter "bin" -Label "bin"

    Write-Host "Clearing publish output (Publish folder is kept)..."
    Clear-Folder -Path (Join-Path $ProjectRoot "Publish")

    if ($IncludeArchives) {
        Write-Host "Clearing archives (Archives folder is kept)..."
        Clear-Folder -Path (Join-Path $ProjectRoot "Archives")
    }
    else {
        Write-Host "Archives kept (pass -IncludeArchives to clear them)."
    }

    Write-Ok ""
    Write-Ok "Clean complete."
}
catch {
    Write-Fail ""
    Write-Fail "CLEAN FAILED: $($_.Exception.Message)"
    $failed = $true
}

if ($failed) {
    Wait-ForExit -NoPause:$NoPause
    exit 1
}
