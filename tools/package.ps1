param([string]$ValheimRoot, [string]$BepInExCore)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    $revision = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Git checkpoint required.' }
    if (& git status --porcelain) { throw 'Commit source/documentation before producing a package with build provenance.' }
    if (!$ValheimRoot) {
        $environmentFile = Join-Path $projectRoot 'Environment.props'
        if (!(Test-Path -LiteralPath $environmentFile)) { throw 'Pass -ValheimRoot or configure Environment.props.' }
        [xml]$environment = Get-Content -LiteralPath $environmentFile -Raw
        $ValheimRoot = [string]$environment.Project.PropertyGroup.ValheimRoot
    }
    $ValheimRoot = [IO.Path]::GetFullPath($ValheimRoot)
    $gameAssembly = Join-Path $ValheimRoot 'valheim_Data\Managed\assembly_valheim.dll'
    if (!(Test-Path -LiteralPath $gameAssembly -PathType Leaf)) { throw 'Game reference assembly missing.' }
    if (!$BepInExCore) {
        $environmentFile = Join-Path $projectRoot 'Environment.props'
        if (Test-Path -LiteralPath $environmentFile) {
            [xml]$environment = Get-Content -LiteralPath $environmentFile -Raw
            $BepInExCore = [string]$environment.Project.PropertyGroup.BepInExCore
        }
        if (!$BepInExCore -or $BepInExCore.Contains('$(')) { $BepInExCore = Join-Path $ValheimRoot 'BepInEx/core' }
    }
    $sdk = Join-Path $PSScriptRoot 'dotnet.ps1'
    & $sdk build CraftyQoL.sln -c Release --nologo "-p:ValheimRoot=$ValheimRoot" "-p:BepInExCore=$BepInExCore"
    if ($LASTEXITCODE -ne 0) { throw 'Build failed; no package produced.' }
    $plugin = Join-Path $projectRoot 'src\CraftyQoL\bin\Release\net472\CraftyQoL.dll'
    $verification = & $sdk run --project tests\CraftyQoL.Tests -c Release --no-build -- --api $gameAssembly --plugin $plugin
    $verification | Write-Output
    if ($LASTEXITCODE -ne 0) { throw 'Checks failed; no package produced.' }
    $result = $verification | Where-Object { $_ -match '^\d+ checks passed; 0 failed' } | Select-Object -Last 1
    if (!$result) { throw 'Verification summary missing.' }
    $tooltip = & $sdk run --project tests/CraftyQoL.Tests -c Release --no-build -- --tooltip-only (Join-Path $ValheimRoot 'valheim_Data/Managed/assembly_guiutils.dll') $plugin
    $tooltip | Write-Output
    if ($LASTEXITCODE -ne 0) { throw 'Tooltip/plugin checks failed; no package produced.' }
    $checks = [int]([regex]::Match($result, '^\d+').Value)
    [xml]$project = Get-Content -LiteralPath 'src\CraftyQoL\CraftyQoL.csproj' -Raw
    $version = [string]$project.Project.PropertyGroup.Version
    $output = Join-Path $projectRoot 'src\CraftyQoL\bin\Release\net472'
    $plugin = Join-Path $output 'CraftyQoL.dll'
    $unexpected = Get-ChildItem -LiteralPath $output -File | Where-Object { $_.Name -notin @('CraftyQoL.dll', 'CraftyQoL.pdb') }
    if ($unexpected) { throw 'Unexpected build output; audit copy-local references before packaging.' }
    if (& git status --porcelain) { throw 'Tracked files changed during verification.' }
    $artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
    $name = "CraftyQoL-$version-dev-$($revision.Substring(0, 8))"
    $stage = [IO.Path]::GetFullPath((Join-Path $artifactRoot $name))
    $zip = [IO.Path]::GetFullPath((Join-Path $artifactRoot ($name + '.zip')))
    if (!$stage.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid artifact path.' }
    if ((Test-Path -LiteralPath $stage) -or (Test-Path -LiteralPath $zip)) { throw 'This checkpoint already has an artifact; existing artifacts are preserved.' }
    New-Item -ItemType Directory -Path (Join-Path $stage 'BepInEx\plugins\CraftyQoL') -Force | Out-Null
    $files = [ordered]@{
        'BepInEx/plugins/CraftyQoL/CraftyQoL.dll' = $plugin
        'README.md' = 'docs/INSTALLATION.md'
        'CONFIGURATION.md' = 'docs/CONFIGURATION.md'
        'MANUAL_TESTS.md' = 'docs/MANUAL_TESTS.md'
        'CHANGELOG.md' = 'CHANGELOG.md'
        'LICENSE' = 'LICENSE'
        'THIRD_PARTY_NOTICES.md' = 'THIRD_PARTY_NOTICES.md'
    }
    foreach ($relative in $files.Keys) {
        Copy-Item -LiteralPath $files[$relative] -Destination (Join-Path $stage $relative)
    }
    $info = [ordered]@{
        name = 'CraftyQoL'; version = $version; status = 'development-preview-runtime-unvalidated'
        sourceCommit = $revision; builtUtc = [DateTime]::UtcNow.ToString('o')
        harmonyTargets = 9; fullRegressionRun = $true; benchmarkRun = $false; additionalTooltipChecks = 2
        targetGame = '1.0.21'; gameAssemblySha256 = (Get-FileHash -LiteralPath $gameAssembly -Algorithm SHA256).Hash
        bepinexPack = '5.4.2351'; framework = 'net472'; sdk = '10.0.402'
        checksPassed = $checks; gameExecuted = $false
        pluginSha256 = (Get-FileHash -LiteralPath $plugin -Algorithm SHA256).Hash
    }
    $info | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stage 'build-info.json') -Encoding UTF8
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $actual = @($archive.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName.Replace('\', '/') } | Sort-Object)
        $expected = @(@($files.Keys) + 'build-info.json' | Sort-Object)
        if (Compare-Object $expected $actual) { throw 'Archive allowlist audit failed.' }
    }
    finally { $archive.Dispose() }
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    "$hash  $name.zip" | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ASCII
    Write-Output "Audited development package: $zip"
    Write-Output "SHA256: $hash"
}
finally { Pop-Location }
