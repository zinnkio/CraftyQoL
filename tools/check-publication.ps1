# Source-only public-tree guard. Does not load game code or require game assemblies.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $files = @(& git -c core.quotepath=false ls-files)
    if ($LASTEXITCODE -ne 0 -or !$files.Count) { throw 'A Git source checkout is required.' }
    $top = @('.gitignore','CHANGELOG.md','CraftyQoL.code-workspace','CraftyQoL.sln',
        'Directory.Build.props','Directory.Build.targets','Environment.props.example',
        'global.json','LICENSE','README.md','THIRD_PARTY_NOTICES.md')
    $docs = @('ARCHITECTURE.md','BUILDING.md','COMPATIBILITY.md','CONFIGURATION.md',
        'DEVELOPMENT_HISTORY.md','INSTALLATION.md','MANUAL_TESTS.md','RELEASE_INSTALL.md')
    $scripts = @('dotnet.ps1','package.ps1','check-publication.ps1')
    $allowed = '^src/CraftyQoL/(?:Core/)?[A-Za-z0-9.]+\.(?:cs|csproj)$|^tests/CraftyQoL.Tests/[A-Za-z0-9.]+\.(?:cs|csproj)$|^tests/CraftyQoL.Tests/Data/[A-Za-z0-9_.-]+\.(?:json|md)$'
    foreach ($file in $files) {
        $ok = $file -in $top -or $file -match $allowed -or
            ($file.StartsWith('docs/') -and $file.Substring(5) -in $docs) -or
            ($file.StartsWith('tools/') -and $file.Substring(6) -in $scripts) -or
            $file -eq '.github/workflows/source-check.yml'
        if (!$ok) { throw "Unexpected publication file: $file" }
        $content = [IO.File]::ReadAllText((Join-Path $root $file))
        # Known accidental credential/private-path shapes; manual review is still required.
        $sensitive = '(?i)(?:[A-Z]:[\\/](?:Users|Documents|SteamLibrary)[\\/]|/[U]sers/|/[h]ome/)[^\s]+|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'
        if ($content -match $sensitive) { throw "Potential private content in $file (value withheld)." }
        if ($file.EndsWith('.md')) {
            foreach ($link in [regex]::Matches($content, '\]\(([^)]+)\)')) {
                $target = $link.Groups[1].Value.Split('#')[0]
                if (!$target -or $target -match '^[a-z]+:' -or $target.StartsWith('#')) { continue }
                $resolved = Join-Path (Split-Path (Join-Path $root $file) -Parent) $target
                if (!(Test-Path -LiteralPath $resolved)) { throw "Broken documentation link in ${file}: $target" }
            }
        }
    }
    $badTracked = @(& git ls-files -ci --exclude-standard)
    if ($LASTEXITCODE -ne 0 -or $badTracked.Count) { throw 'Tracked files overlap ignore rules; inspect git ls-files -ci --exclude-standard.' }
    $samples = @('Environment.props','.tools/probe.txt','.references/probe.txt','.inspection/probe.txt',
        '.backups/probe.txt','.publication/probe.txt','artifacts/probe.txt','src/CraftyQoL/bin/probe.txt',
        'src/CraftyQoL/obj/probe.txt','probe.dll','probe.pdb','probe.zip','.env','probe.pem','OPTIMISATION_AUDIT.md')
    foreach ($sample in $samples) {
        & git check-ignore --quiet --no-index -- $sample
        if ($LASTEXITCODE -ne 0) { throw "Missing ignore protection for $sample" }
    }
    Write-Output "Publication checks passed: $($files.Count) source files; allowlist, path/credential patterns, local links and ignore rules. Manual content/history review remains required."
} finally { Pop-Location }
