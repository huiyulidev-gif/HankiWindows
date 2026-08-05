[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$version = '0.2.1-rc.1'
$repo = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dist = Join-Path $repo "dist\$version"
$zip = Join-Path $dist "Hanki-$version-win-x64-portable.zip"
$setup = Join-Path $dist "HankiSetup-$version.exe"
$sums = Join-Path $dist "SHA256SUMS-$version.txt"
foreach ($required in @($zip, $setup, $sums)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Missing artifact: $required"
    }
}

$runRoot = Join-Path $env:TEMP ("Hanki-021-validation-" + [guid]::NewGuid().ToString('N'))
$extractRoot = Join-Path $runRoot 'extract'
$dataRoot = Join-Path $runRoot 'data'
New-Item -ItemType Directory -Path $extractRoot, $dataRoot -Force | Out-Null

try {
    Expand-Archive -LiteralPath $zip -DestinationPath $extractRoot
    $exe = Get-ChildItem -LiteralPath $extractRoot -Recurse -Filter Hanki.exe |
        Select-Object -First 1
    if (-not $exe) {
        throw 'Hanki.exe was not found after extraction.'
    }
    $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exe.FullName)
    if ($info.FileVersion -ne '0.2.1.0' -or $info.ProductVersion -ne $version) {
        throw "Portable version mismatch: $($info.FileVersion) / $($info.ProductVersion)"
    }

    $env:HANKI_DATA_DIRECTORY = $dataRoot
    $env:HANKI_INSTANCE_NAMESPACE = 'rc1validation'
    $first = Start-Process -FilePath $exe.FullName `
        -ArgumentList '--diagnostic-exit-after-ms=8000' `
        -WorkingDirectory $exe.DirectoryName `
        -PassThru
    Start-Sleep -Milliseconds 1800
    $first.Refresh()
    if ($first.HasExited -or $first.MainWindowHandle -eq 0) {
        throw 'Portable did not reach a ready main window.'
    }

    $second = Start-Process -FilePath $exe.FullName `
        -WorkingDirectory $exe.DirectoryName `
        -PassThru
    if (-not $second.WaitForExit(5000) -or $second.ExitCode -ne 0) {
        throw 'Second instance did not exit cleanly.'
    }
    $samePathCount = @(Get-Process -Name Hanki -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $exe.FullName }).Count
    if ($samePathCount -ne 1) {
        throw "Expected one portable process after second launch, found $samePathCount."
    }

    if (-not $first.WaitForExit(15000) -or $first.ExitCode -ne 0) {
        throw 'Portable did not exit cleanly.'
    }
    $residual = @(Get-Process -Name Hanki -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $exe.FullName }).Count
    if ($residual -ne 0) {
        throw "Residual portable process count: $residual"
    }

    $expected = @{}
    foreach ($line in Get-Content -LiteralPath $sums -Encoding UTF8) {
        if ($line -match '^([0-9A-Fa-f]{64})\s{2}(.+)$') {
            $expected[$matches[2]] = $matches[1].ToUpperInvariant()
        }
    }
    foreach ($artifact in @($zip, $setup)) {
        $name = [System.IO.Path]::GetFileName($artifact)
        $actual = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash
        if ($expected[$name] -ne $actual) {
            throw "Checksum mismatch: $name"
        }
    }

    [pscustomobject]@{
        PortableVersion = $info.ProductVersion
        FileVersion = $info.FileVersion
        MainWindowReady = $true
        SecondInstanceExitCode = $second.ExitCode
        FirstInstanceExitCode = $first.ExitCode
        ResidualProcessCount = $residual
        ZipChecksum = 'PASS'
        SetupChecksum = 'PASS'
    }
}
finally {
    Remove-Item Env:HANKI_DATA_DIRECTORY -ErrorAction SilentlyContinue
    Remove-Item Env:HANKI_INSTANCE_NAMESPACE -ErrorAction SilentlyContinue
    Get-Process -Name Hanki -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -like "$extractRoot*" } |
        Stop-Process -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $runRoot) {
        Remove-Item -LiteralPath $runRoot -Recurse -Force
    }
}
