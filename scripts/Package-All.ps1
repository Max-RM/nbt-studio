param(
    [string[]]$Items = @("win-x64", "win-x86", "win-arm64"),
    [switch]$SkipSelfContained,
    [switch]$NoPause
)

. (Join-Path $PSScriptRoot "_Common.ps1")

# When the script is launched with -File (for example from a .cmd or CI),
# PowerShell passes "win-x64,win-x86" as one string, so split it manually.
$validItems = @("win-x64", "win-x86", "win-arm64", "source")
$Items = @($Items |
    ForEach-Object { $_ -split ',' } |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ } |
    Select-Object -Unique)
foreach ($item in $Items) {
    if ($validItems -notcontains $item) {
        Write-Fail "Invalid item '$item'. Valid items: $($validItems -join ', ')."
        Wait-ForExit -NoPause:$NoPause
        exit 1
    }
}

$PublishRoot = Join-Path $ProjectRoot "Publish"
$ArchiveRoot = Join-Path $ProjectRoot "Archives"

New-Item -ItemType Directory -Path $ArchiveRoot -Force | Out-Null

# Prefer 7-Zip (fast) when available on PATH, otherwise fall back to Compress-Archive (slow).
$SevenZip = $null
$sevenZipCandidates = @()
foreach ($candidate in @("7z", "7za")) {
    $cmd = Get-Command $candidate -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cmd) { $sevenZipCandidates += $cmd.Source }
}
if ($env:ProgramFiles) {
    $sevenZipCandidates += Join-Path $env:ProgramFiles "7-Zip\7z.exe"
}
if (${env:ProgramFiles(x86)}) {
    $sevenZipCandidates += Join-Path ${env:ProgramFiles(x86)} "7-Zip\7z.exe"
}
foreach ($candidate in $sevenZipCandidates) {
    if ($candidate -and (Test-Path -LiteralPath $candidate)) {
        $SevenZip = $candidate
        break
    }
}
if ($SevenZip) {
    Write-Ok "Using 7-Zip: $SevenZip"
}
else {
    Write-Warn "7-Zip not found on PATH, falling back to Compress-Archive (slower)."
}

# Compress-Archive has no exclusion support, so stage the filtered tree first.
function New-FilteredZip {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir,
        [Parameter(Mandatory = $true)][string]$ArchivePath,
        [string[]]$Exclude
    )

    $staging = Join-Path ([System.IO.Path]::GetTempPath()) ("nbt-studio-package-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    try {
        $robocopyArgs = @($SourceDir, $staging, "/E", "/NFL", "/NDL", "/NJH", "/NJS", "/NP")
        foreach ($pattern in @($Exclude)) {
            if (-not $pattern) { continue }
            if ($pattern.Contains("*")) {
                $robocopyArgs += @("/XF", $pattern)
            }
            else {
                $robocopyArgs += @("/XD", $pattern)
            }
        }
        & robocopy @robocopyArgs | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed (exit code $LASTEXITCODE) for $SourceDir" }

        Compress-Archive -Path "$staging\*" -DestinationPath $ArchivePath -CompressionLevel Optimal
    }
    finally {
        Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function New-Zip {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir,
        [Parameter(Mandatory = $true)][string]$ArchivePath,
        [string[]]$Exclude
    )

    if (Test-Path -LiteralPath $ArchivePath) {
        Remove-Item -LiteralPath $ArchivePath -Force
    }

    if ($SevenZip) {
        $sevenZipArgs = @("a", "-tzip", "-mx=5", "-y", $ArchivePath, ".\*")
        foreach ($pattern in @($Exclude)) {
            if ($pattern) { $sevenZipArgs += "-xr!$pattern" }
        }
        Push-Location -LiteralPath $SourceDir
        try {
            & $SevenZip @sevenZipArgs | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "7-Zip failed (exit code $LASTEXITCODE) for $SourceDir" }
        }
        finally {
            Pop-Location
        }
    }
    else {
        if ($Exclude) {
            New-FilteredZip -SourceDir $SourceDir -ArchivePath $ArchivePath -Exclude $Exclude
        }
        else {
            Compress-Archive -Path "$SourceDir\*" -DestinationPath $ArchivePath -CompressionLevel Optimal
        }
    }
}

function New-Archive {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir,
        [Parameter(Mandatory = $true)][string]$ArchiveName
    )

    if (-not (Test-Path -LiteralPath $SourceDir)) {
        Write-Warn "  SKIPPED (not found): $SourceDir"
        return
    }

    $archivePath = Join-Path $ArchiveRoot "$ArchiveName.zip"
    Write-Host "  Packing: $SourceDir -> $archivePath"
    New-Zip -SourceDir $SourceDir -ArchivePath $archivePath
}

function New-SourceArchive {
    $archivePath = Join-Path $ArchiveRoot "nbt-studio-source.zip"
    $exclude = @(
        ".git", ".vs", ".idea",
        "Publish", "Archives",
        "bin", "obj",
        "*.user", "*.suo"
    )
    Write-Host "  Packing source: $ProjectRoot -> $archivePath"
    New-Zip -SourceDir $ProjectRoot -ArchivePath $archivePath -Exclude $exclude
}

$failed = $false
try {
    Write-Step "=== Packaging ==="

    if ($Items -contains "source") {
        Write-Host "  Source archive..."
        New-SourceArchive
    }

    foreach ($rid in @("win-x64", "win-x86", "win-arm64")) {
        if ($Items -notcontains $rid) { continue }

        $srcDir = Join-Path $PublishRoot "NbtStudio_$rid"
        New-Archive -SourceDir $srcDir -ArchiveName "NbtStudio-$rid"

        if (-not $SkipSelfContained) {
            $selfContainedDir = Join-Path $PublishRoot "NbtStudio_SelfContained_$rid"
            New-Archive -SourceDir $selfContainedDir -ArchiveName "NbtStudio-SelfContained-$rid"
        }
    }

    Write-Ok ""
    Write-Ok "Done. Archives in: $ArchiveRoot"
}
catch {
    Write-Fail ""
    Write-Fail "PACKAGING FAILED: $($_.Exception.Message)"
    $failed = $true
}

if ($failed) {
    Wait-ForExit -NoPause:$NoPause
    exit 1
}
