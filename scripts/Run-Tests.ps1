[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("x64")]
    [string]$Platform = "x64",

    [string]$ResultsDirectory
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

$testRunner = Join-Path $installationPath `
    "Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
if (-not (Test-Path -LiteralPath $testRunner)) {
    throw "The Visual Studio test runner was not found."
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$testOutput = Join-Path $repositoryRoot "PhoenixEngine.Tests\bin\$Platform\$Configuration"
$testAssembly = Join-Path $testOutput "PhoenixEngine.Tests.dll"
$testAdapter = Join-Path $testOutput "MSTest.TestAdapter.dll"
if (-not (Test-Path -LiteralPath $testAssembly)) {
    throw "The PhoenixEngine test assembly was not found: $testAssembly"
}
if (-not (Test-Path -LiteralPath $testAdapter)) {
    throw "The pinned MSTest adapter was not found: $testAdapter"
}

$arguments = @(
    $testAssembly
    "/Platform:$Platform"
    "/TestAdapterPath:$testOutput"
    "/Logger:Console;Verbosity=normal"
)

if (-not [string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $resolvedResultsDirectory = if ([IO.Path]::IsPathRooted($ResultsDirectory)) {
        [IO.Path]::GetFullPath($ResultsDirectory)
    }
    else {
        [IO.Path]::GetFullPath((Join-Path $repositoryRoot $ResultsDirectory))
    }

    New-Item -ItemType Directory -Path $resolvedResultsDirectory -Force | Out-Null
    $arguments += "/ResultsDirectory:$resolvedResultsDirectory"
    $arguments += "/Logger:trx;LogFileName=PhoenixEngine.Tests.trx"
}

& $testRunner @arguments
$testExitCode = $LASTEXITCODE

if (-not [string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $trxPath = Join-Path $resolvedResultsDirectory "PhoenixEngine.Tests.trx"
    if (Test-Path -LiteralPath $trxPath -PathType Leaf) {
        $document = [Xml.XmlDocument]::new()
        $document.PreserveWhitespace = $true
        $document.Load($trxPath)
        $namespaceManager = [Xml.XmlNamespaceManager]::new($document.NameTable)
        $namespaceManager.AddNamespace("trx", "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")

        $testRun = $document.SelectSingleNode("/trx:TestRun", $namespaceManager)
        $testRun.SetAttribute("name", "PhoenixEngine test run")
        $testRun.SetAttribute("runUser", "test-user")

        $deployment = $document.SelectSingleNode("//trx:Deployment", $namespaceManager)
        if ($null -ne $deployment) {
            $deployment.SetAttribute("runDeploymentRoot", "test-results")
        }

        foreach ($result in $document.SelectNodes("//trx:UnitTestResult", $namespaceManager)) {
            $result.SetAttribute("computerName", "test-host")
        }

        foreach ($definition in $document.SelectNodes("//trx:UnitTest", $namespaceManager)) {
            $definition.SetAttribute("storage", [IO.Path]::GetFileName($definition.GetAttribute("storage")))
        }

        foreach ($method in $document.SelectNodes("//trx:TestMethod", $namespaceManager)) {
            $method.SetAttribute("codeBase", [IO.Path]::GetFileName($method.GetAttribute("codeBase")))
        }

        foreach ($node in $document.SelectNodes("//text() | //@*")) {
            $node.Value = [Text.RegularExpressions.Regex]::Replace(
                $node.Value,
                [Text.RegularExpressions.Regex]::Escape($repositoryRoot),
                "PhoenixEngine",
                [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        }

        $document.Save($trxPath)
    }
}

if ($testExitCode -ne 0) {
    throw "PhoenixEngine tests failed with exit code $testExitCode."
}
