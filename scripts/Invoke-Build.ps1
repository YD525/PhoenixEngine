[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("x64")]
    [string]$Platform = "x64",

    [switch]$UpdateLockFiles
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw "Visual Studio Installer discovery tool was not found."
}

$installationPath = & $vswhere `
    -latest `
    -products * `
    -requires Microsoft.Component.MSBuild `
    -property installationPath
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($installationPath)) {
    throw "Visual Studio with MSBuild was not found."
}

$msbuild = Join-Path $installationPath "MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path -LiteralPath $msbuild)) {
    throw "MSBuild was not found in the selected Visual Studio installation."
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solution = Join-Path $repositoryRoot "PhoenixEngine.sln"
$restoreArguments = @(
    $solution
    "/t:Restore"
    "/m"
    "/p:Configuration=$Configuration"
    "/p:Platform=$Platform"
)
if (-not $UpdateLockFiles) {
    $restoreArguments += "/p:RestoreLockedMode=true"
}

& $msbuild @restoreArguments
if ($LASTEXITCODE -ne 0) {
    throw "Package restore failed with exit code $LASTEXITCODE."
}

& $msbuild $solution `
    /t:Rebuild `
    /m `
    "/p:Configuration=$Configuration" `
    "/p:Platform=$Platform" `
    /p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) {
    throw "PhoenixEngine build failed with exit code $LASTEXITCODE."
}
