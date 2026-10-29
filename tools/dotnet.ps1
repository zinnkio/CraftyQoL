# Use an optional isolated SDK, otherwise the installed SDK selected by global.json.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sdkExecutable = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $sdkExecutable)) {
    $installed = Get-Command dotnet -ErrorAction SilentlyContinue
    if (!$installed) { throw 'Install the SDK pinned in global.json. See docs/BUILDING.md.' }
    $sdkExecutable = $installed.Source
}
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\dotnet-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools\nuget-packages'
$env:NUGET_HTTP_CACHE_PATH = Join-Path $projectRoot '.tools\nuget-http-cache'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = '1'
$env:DOTNET_NOLOGO = '1'
& $sdkExecutable @args
exit $LASTEXITCODE
