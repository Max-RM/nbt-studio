param(
    [string[]]$Rids = @("win-x64", "win-x86", "win-arm64"),
    [string]$Configuration = "Release",
    [switch]$SelfContained,
    [switch]$SkipBuild,
    [switch]$NoPause
)

. (Join-Path $PSScriptRoot "_Common.ps1")

# When the script is launched with -File (for example from a .cmd or CI),
# PowerShell passes "win-x64,win-x86" as one string, so split it manually.
$validRids = @("win-x64", "win-x86", "win-arm64")
$Rids = @($Rids |
    ForEach-Object { $_ -split ',' } |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_ } |
    Select-Object -Unique)
foreach ($rid in $Rids) {
    if ($validRids -notcontains $rid) {
        Write-Fail "Invalid runtime identifier '$rid'. Valid identifiers: $($validRids -join ', ')."
        Wait-ForExit -NoPause:$NoPause
        exit 1
    }
}

$OutputRoot = Join-Path $ProjectRoot "Publish"

function Get-PublishArgs {
    param([string]$Rid)
    $selfContainedValue = if ($SelfContained) { "true" } else { "false" }
    $publishArgs = @(
        "--configuration", $Configuration,
        "--runtime", $Rid,
        "--self-contained", $selfContainedValue,
        "-p:PublishSingleFile=true",
        "-p:DebugType=None",
        "-p:DebugSymbols=false",
        "--nologo"
    )
    if ($SelfContained) {
        $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
    }
    return $publishArgs
}

function Get-PublishDirName {
    param([string]$Rid)
    if ($SelfContained) { return "NbtStudio_SelfContained_$Rid" }
    return "NbtStudio_$Rid"
}

function Publish-Rid {
    param([string]$Rid)

    $outDir = Join-Path $OutputRoot (Get-PublishDirName $Rid)

    if (Test-Path -LiteralPath $outDir) {
        Remove-Item -LiteralPath $outDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null

    Write-Host "  Building NbtStudio (NbtStudio.exe)..."
    & dotnet publish $GuiProject @(Get-PublishArgs $Rid) -o $outDir
    if ($LASTEXITCODE -ne 0) { throw "Publishing NbtStudio for $Rid failed (exit code $LASTEXITCODE)." }

    $exe = Join-Path $outDir "NbtStudio.exe"
    if (-not (Test-Path -LiteralPath $exe)) {
        throw "Publishing for $Rid did not produce NbtStudio.exe."
    }
}

$failed = $false
try {
    Write-Step "Checking prerequisites..."
    $problems = @()
    $problems += @(Test-DotNetPrerequisites)
    $problems += @(Test-Submodules)
    if ($problems.Count -gt 0) {
        Show-PrerequisiteHelp -Problems $problems
        throw "Prerequisites are missing."
    }
    Write-Ok "Prerequisites OK."

    New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null

    foreach ($rid in $Rids) {
        Write-Step ""
        Write-Step "=== Publishing $rid ==="
        if ($SkipBuild) {
            Write-Warn "  -SkipBuild set, skipping compilation for $rid"
        }
        else {
            Publish-Rid -Rid $rid
        }
        Write-Ok "OK: $rid  ->  $(Join-Path $OutputRoot (Get-PublishDirName $rid))"
    }

    Write-Step ""
    if (-not $SelfContained) {
        Write-Host "This build requires the .NET 6 Desktop Runtime on the target machine."
    }
    Write-Ok "Done. Published to: $OutputRoot"
}
catch {
    if ($_.Exception.Message -ne "Prerequisites are missing.") {
        Write-Fail ""
        Write-Fail "BUILD FAILED: $($_.Exception.Message)"
        Write-Host "If the error mentions NuGet / downloading packages, check your internet connection:"
        Write-Host "the runtime packs for self-contained builds are downloaded on the first build."
        Write-Host "If the problem is about Windows Desktop / Windows Forms components or the SDK, install the"
        Write-Host "'.NET desktop development' workload in the Visual Studio Installer and try again."
    }
    $failed = $true
}

if ($failed) {
    Wait-ForExit -NoPause:$NoPause
    exit 1
}
