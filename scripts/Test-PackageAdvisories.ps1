[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solution = Join-Path $repositoryRoot "PhoenixEngine.sln"
$reportPath = Join-Path ([IO.Path]::GetTempPath()) ("PhoenixEngine-advisories-{0}.json" -f [Guid]::NewGuid())

function Get-OptionalItems {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName
    )

    $property = $InputObject.PSObject.Properties[$PropertyName]
    if ($null -eq $property -or $null -eq $property.Value) {
        return @()
    }

    return @($property.Value)
}

try {
    & dotnet list $solution package --vulnerable --include-transitive --format json --output-version 1 |
        Set-Content -LiteralPath $reportPath -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "NuGet advisory inspection failed with exit code $LASTEXITCODE."
    }

    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $vulnerablePackages = @(
        foreach ($project in $report.projects) {
            foreach ($framework in Get-OptionalItems $project "frameworks") {
                $packages = @(Get-OptionalItems $framework "topLevelPackages") +
                    @(Get-OptionalItems $framework "transitivePackages")
                foreach ($package in $packages) {
                    $vulnerabilities = @(Get-OptionalItems $package "vulnerabilities")
                    if ($vulnerabilities.Count -gt 0) {
                        $package
                    }
                }
            }
        }
    )

    if ($vulnerablePackages.Count -gt 0) {
        $packageNames = $vulnerablePackages | ForEach-Object { $_.id } | Sort-Object -Unique
        throw "Vulnerable NuGet packages were found: $($packageNames -join ', ')"
    }

    Write-Host "No known NuGet vulnerabilities were found in direct or transitive packages."
}
finally {
    Remove-Item -LiteralPath $reportPath -Force -ErrorAction SilentlyContinue
}
