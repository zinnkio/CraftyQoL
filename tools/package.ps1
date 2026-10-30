param([string]$ValheimRoot, [string]$BepInExCore)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    $revision = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or (& git status --porcelain)) { throw 'A clean source checkpoint is required.' }
    & (Join-Path $PSScriptRoot 'check-publication.ps1')
    $environmentFile = Join-Path $projectRoot 'Environment.props'
    if (Test-Path -LiteralPath $environmentFile) {
        [xml]$environment = Get-Content -LiteralPath $environmentFile -Raw
        if (!$ValheimRoot) { $ValheimRoot = [string]$environment.Project.PropertyGroup.ValheimRoot }
        if (!$BepInExCore) { $BepInExCore = [string]$environment.Project.PropertyGroup.BepInExCore }
    }
    if (!$ValheimRoot) { throw 'Pass -ValheimRoot or configure ignored Environment.props. See docs/BUILDING.md.' }
    $ValheimRoot = [IO.Path]::GetFullPath($ValheimRoot)
    if (!$BepInExCore -or $BepInExCore.Contains('$(')) { $BepInExCore = Join-Path $ValheimRoot 'BepInEx/core' }
    $gameAssembly = Join-Path $ValheimRoot 'valheim_Data/Managed/assembly_valheim.dll'
    if (!(Test-Path -LiteralPath $gameAssembly)) { throw 'Game reference assembly missing.' }
    [xml]$project = Get-Content -LiteralPath 'src/CraftyQoL/CraftyQoL.csproj' -Raw
    $version = [string]$project.Project.PropertyGroup.Version
    $name = "CraftyQoL-$version"
    $artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
    $release = [IO.Path]::GetFullPath((Join-Path $artifactRoot $name))
    if (!$release.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid release path.' }
    if (Test-Path -LiteralPath $release) { throw 'Release output exists; older artifacts are never overwritten.' }
    $sdk = Join-Path $PSScriptRoot 'dotnet.ps1'
    & $sdk build CraftyQoL.sln -c Release --nologo "-p:ValheimRoot=$ValheimRoot" "-p:BepInExCore=$BepInExCore" '-p:DebugType=None' '-p:DebugSymbols=false' "-p:PathMap=$projectRoot=/_/CraftyQoL"
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    $output = Join-Path $projectRoot 'src/CraftyQoL/bin/Release/net472'
    $plugin = Join-Path $output 'CraftyQoL.dll'
    if (Get-ChildItem -LiteralPath $output -File | Where-Object { $_.Name -notin @('CraftyQoL.dll', 'CraftyQoL.pdb') }) { throw 'Unexpected output dependencies.' }
    $verification = & $sdk run --project tests/CraftyQoL.Tests -c Release --no-build -- --api $gameAssembly --plugin $plugin
    $verification | Write-Output
    if ($LASTEXITCODE -ne 0) { throw 'Regression/API/plugin checks failed.' }
    $summary = $verification | Where-Object { $_ -match '^\d+ checks passed; 0 failed' } | Select-Object -Last 1
    if (!$summary) { throw 'Verification summary missing.' }
    $checks = [int]([regex]::Match($summary, '^\d+').Value)
    $tooltip = & $sdk run --project tests/CraftyQoL.Tests -c Release --no-build -- --tooltip-only (Join-Path $ValheimRoot 'valheim_Data/Managed/assembly_guiutils.dll') $plugin
    $tooltip | Write-Output
    if ($LASTEXITCODE -ne 0 -or !($tooltip | Where-Object { $_ -match '^2 tooltip integration checks passed; 0 failed' })) { throw 'Tooltip/identity checks failed.' }
    if (& git status --porcelain) { throw 'Source changed during verification.' }
    $stage = Join-Path $release 'package'
    New-Item -ItemType Directory -Path (Join-Path $stage 'BepInEx/plugins/CraftyQoL') -Force | Out-Null
    $files = [ordered]@{
        'BepInEx/plugins/CraftyQoL/CraftyQoL.dll' = $plugin
        'README.md' = 'docs/RELEASE_INSTALL.md'
        'LICENSE' = 'LICENSE'
        'THIRD_PARTY_NOTICES.md' = 'THIRD_PARTY_NOTICES.md'
    }
    foreach ($relative in $files.Keys) { Copy-Item -LiteralPath $files[$relative] -Destination (Join-Path $stage $relative) }
    $pluginHash = (Get-FileHash -LiteralPath $plugin -Algorithm SHA256).Hash
    $info = [ordered]@{
        name='CraftyQoL'; version=$version; pluginGuid='io.github.zinnkio.craftyqol'; sourceCommit=$revision
        builtUtc=[DateTime]::UtcNow.ToString('o'); targetGame='1.0.21'; bepinexPack='5.4.2351'; framework='net472'; sdk='10.0.402'
        gameAssemblySha256=(Get-FileHash -LiteralPath $gameAssembly -Algorithm SHA256).Hash
        regressionChecksPassed=$checks; tooltipChecksPassed=2; totalChecksPassed=($checks+2); fullRegressionRun=$true
        harmonyTargets=9; benchmarkRun=$false; gameExecutedByBuild=$false; debugSymbols=$false; pluginSha256=$pluginHash
    }
    # Developer provenance stays beside the player ZIP, never in its install contents.
    $info | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $release 'build-info.json') -Encoding UTF8
    $zip = Join-Path $release ($name+'.zip')
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $actual = @($archive.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName.Replace('\','/') } | Sort-Object)
        $expected = @($files.Keys | Sort-Object)
        if (Compare-Object $expected $actual) { throw 'Archive allowlist audit failed.' }
    } finally { $archive.Dispose() }
    $standalone = Join-Path $release 'CraftyQoL.dll'
    Copy-Item -LiteralPath $plugin -Destination $standalone
    if ((Get-FileHash -LiteralPath $standalone -Algorithm SHA256).Hash -ne $pluginHash) { throw 'Standalone DLL mismatch.' }
    $zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    @("$zipHash  $name.zip", "$pluginHash  CraftyQoL.dll") | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ASCII
    Write-Output "Release files: $release"
    Write-Output "ZIP SHA256: $zipHash"
    Write-Output "DLL SHA256: $pluginHash"
} finally { Pop-Location }
