[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$expectedProjects = @(
    "PhoenixEngine\PhoenixEngine.csproj"
    "PhoenixEngine.Tests\PhoenixEngine.Tests.csproj"
)

$actualProjects = Get-ChildItem -LiteralPath $repositoryRoot -Filter "*.csproj" -File -Recurse |
    ForEach-Object { [IO.Path]::GetRelativePath($repositoryRoot, $_.FullName) } |
    Sort-Object
$unexpectedProjects = @($actualProjects | Where-Object { $_ -notin $expectedProjects })
$missingProjects = @($expectedProjects | Where-Object { $_ -notin $actualProjects })
if ($unexpectedProjects.Count -gt 0 -or $missingProjects.Count -gt 0) {
    throw "The repository project layout differs from the two canonical solution projects."
}

$solutionText = Get-Content -LiteralPath (Join-Path $repositoryRoot "PhoenixEngine.sln") -Raw
foreach ($project in $expectedProjects) {
    if (-not $solutionText.Contains($project)) {
        throw "PhoenixEngine.sln does not reference the canonical project: $project"
    }
}

$packageManifests = @(Get-ChildItem -LiteralPath $repositoryRoot -Filter "packages.config" -File -Recurse)
if ($packageManifests.Count -ne 0) {
    throw "Legacy packages.config manifests are not supported."
}

$trackedFiles = & git -C $repositoryRoot ls-files -- `
    "*.csproj" `
    "*.sln" `
    "*.props" `
    "*.targets" `
    "README.md" `
    ".github/workflows/*.yml" `
    "scripts/*.ps1"
if ($LASTEXITCODE -ne 0) {
    throw "Tracked repository files could not be enumerated."
}

$forbiddenPattern = 'SSELexicon|(?:^|[\\/])packages[\\/]'
foreach ($relativePath in $trackedFiles) {
    if ($relativePath -eq "scripts/Test-RepositoryLayout.ps1") {
        continue
    }

    $fullPath = Join-Path $repositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        continue
    }

    $text = Get-Content -LiteralPath $fullPath -Raw -ErrorAction SilentlyContinue
    if ($null -ne $text -and $text -match $forbiddenPattern) {
        throw "Tracked file contains an obsolete neighboring or repository-local package path: $relativePath"
    }
}

Write-Host "Repository layout is canonical and self-contained."
