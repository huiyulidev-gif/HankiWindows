[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Hanki.CompatibilityHarness.csproj'
dotnet run --project $project --configuration $Configuration
exit $LASTEXITCODE
