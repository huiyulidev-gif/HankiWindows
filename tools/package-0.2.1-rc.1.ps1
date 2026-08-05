[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$version = '0.2.1-rc.1'
$binaryVersion = '0.2.1.0'
$repo = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distRoot = [System.IO.Path]::GetFullPath((Join-Path $repo "dist\$version"))
$expectedDistRoot = [System.IO.Path]::GetFullPath((Join-Path $repo 'dist\0.2.1-rc.1'))
if (-not [string]::Equals($distRoot, $expectedDistRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Unexpected output directory: $distRoot"
}

if (Test-Path -LiteralPath $distRoot) {
    if (-not $Force) {
        throw "$distRoot already exists. Re-run with -Force only after confirming this RC directory may be replaced."
    }
    Remove-Item -LiteralPath $distRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

$payload = Join-Path $distRoot "Hanki-$version-win-x64"
$project = Join-Path $repo 'src\Hanki.App\Hanki.App.csproj'
dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:PathMap="$repo=/_/HankiWindows" `
    --output $payload
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$privateAuthConfig = Join-Path $repo 'src\Hanki.App\hanki.auth.config.json'
if (Test-Path -LiteralPath $privateAuthConfig) {
    Copy-Item -LiteralPath $privateAuthConfig -Destination (Join-Path $payload 'hanki.auth.config.json')
}
Copy-Item -LiteralPath (Join-Path $repo 'README.md') -Destination (Join-Path $payload 'README.md')
$payloadDocs = Join-Path $payload 'docs'
New-Item -ItemType Directory -Path $payloadDocs -Force | Out-Null
foreach ($document in @(
    'PRIVACY.md',
    'KNOWN_LIMITATIONS.md',
    'COMPATIBILITY.md',
    'DIAGNOSTICS_PRIVACY.md',
    'INPUT_PIPELINE.md'
)) {
    Copy-Item -LiteralPath (Join-Path $repo "docs\$document") -Destination (Join-Path $payloadDocs $document)
}

$guideName = "Hanki-$version-diagnostics-guide.md"
$guidePath = Join-Path $distRoot $guideName
Copy-Item -LiteralPath (Join-Path $repo 'docs\DIAGNOSTICS_GUIDE_0.2.1_RC1.md') -Destination $guidePath
Copy-Item -LiteralPath $guidePath -Destination (Join-Path $payload $guideName)
Copy-Item -LiteralPath (Join-Path $repo 'RELEASE_NOTES_0.2.1_RC1.md') `
    -Destination (Join-Path $distRoot "RELEASE_NOTES-$version.md")

$exe = Join-Path $payload 'Hanki.exe'
$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if ($versionInfo.FileVersion -ne $binaryVersion) {
    throw "Unexpected FileVersion: $($versionInfo.FileVersion)"
}
if ($versionInfo.ProductVersion -ne $version) {
    throw "Unexpected ProductVersion: $($versionInfo.ProductVersion)"
}

$portableZip = Join-Path $distRoot "Hanki-$version-win-x64-portable.zip"
Compress-Archive -LiteralPath $payload -DestinationPath $portableZip -CompressionLevel Optimal

$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) {
    throw 'Inno Setup 6 ISCC.exe was not found.'
}
& $iscc (Join-Path $repo 'installer\Hanki.0.2.1-rc.1.iss')
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE"
}

$setup = Join-Path $distRoot "HankiSetup-$version.exe"
if (-not (Test-Path -LiteralPath $setup)) {
    throw "Installer was not created: $setup"
}

$checksumPath = Join-Path $distRoot "SHA256SUMS-$version.txt"
$checksumLines = foreach ($file in @($portableZip, $setup, $guidePath)) {
    $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    "$hash  $([System.IO.Path]::GetFileName($file))"
}
[System.IO.File]::WriteAllLines($checksumPath, $checksumLines, [System.Text.UTF8Encoding]::new($false))

[pscustomobject]@{
    Version = $version
    FileVersion = $versionInfo.FileVersion
    ProductVersion = $versionInfo.ProductVersion
    Payload = $payload
    PortableZip = $portableZip
    Setup = $setup
    Checksums = $checksumPath
    DiagnosticsGuide = $guidePath
}
