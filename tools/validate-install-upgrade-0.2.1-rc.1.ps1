[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$rcSetup = Join-Path $repo 'dist\0.2.1-rc.1\HankiSetup-0.2.1-rc.1.exe'
$publicSetup = Join-Path $repo 'dist\0.2.0\HankiSetup-0.2.0.exe'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\Yulbyte\Hanki'
$installedExe = Join-Path $installDir 'Hanki.exe'
$dataDir = Join-Path $env:LOCALAPPDATA 'Yulbyte\Hanki'
$runRoot = Join-Path $env:TEMP ("Hanki-021-install-validation-" + [guid]::NewGuid().ToString('N'))
$testData = Join-Path $runRoot 'data-copy'

foreach ($required in @($rcSetup, $publicSetup, $installedExe)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Missing required file: $required"
    }
}
if (@(Get-Process -Name Hanki -ErrorAction SilentlyContinue).Count -ne 0) {
    throw 'Hanki must be fully closed before the install/upgrade validation.'
}
$original = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installedExe)
if ($original.ProductVersion -ne '0.2.0') {
    throw "Expected installed public 0.2.0, found $($original.ProductVersion)"
}

function Get-DataFingerprint {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        return 'missing'
    }
    $lines = foreach ($file in Get-ChildItem -LiteralPath $Path -File -Recurse | Sort-Object FullName) {
        $relative = $file.FullName.Substring($Path.Length).TrimStart('\')
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        "$relative`t$($file.Length)`t$hash"
    }
    $bytes = [System.Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
}

function Invoke-Installer {
    param([string]$Path)
    $process = Start-Process -FilePath $Path -ArgumentList @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART',
        '/SP-',
        '/CURRENTUSER'
    ) -PassThru -Wait
    if ($process.ExitCode -ne 0) {
        throw "Installer failed with exit code $($process.ExitCode): $Path"
    }
}

function Invoke-IsolatedInstalledSmoke {
    param([string]$ExpectedVersion, [string]$Namespace)
    $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installedExe)
    if ($info.ProductVersion -ne $ExpectedVersion) {
        throw "Installed version mismatch. Expected $ExpectedVersion, found $($info.ProductVersion)"
    }
    $env:HANKI_DATA_DIRECTORY = $testData
    $env:HANKI_INSTANCE_NAMESPACE = $Namespace
    try {
        $process = Start-Process -FilePath $installedExe `
            -ArgumentList '--diagnostic-exit-after-ms=10000' `
            -WorkingDirectory $installDir `
            -PassThru
        $ready = $false
        for ($attempt = 0; $attempt -lt 40; $attempt++) {
            Start-Sleep -Milliseconds 250
            $process.Refresh()
            if ($process.HasExited) {
                break
            }
            if ($process.MainWindowHandle -ne 0) {
                $ready = $true
                break
            }
        }
        if (-not $ready) {
            throw "Installed $ExpectedVersion did not reach a ready main window."
        }
        if (-not $process.WaitForExit(18000) -or $process.ExitCode -ne 0) {
            throw "Installed $ExpectedVersion did not exit cleanly."
        }
    }
    finally {
        Remove-Item Env:HANKI_DATA_DIRECTORY -ErrorAction SilentlyContinue
        Remove-Item Env:HANKI_INSTANCE_NAMESPACE -ErrorAction SilentlyContinue
    }
}

$beforeFingerprint = Get-DataFingerprint $dataDir
New-Item -ItemType Directory -Path $testData -Force | Out-Null
foreach ($name in @('hanki.db', 'hanki.db-wal', 'hanki.db-shm')) {
    $source = Join-Path $dataDir $name
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination (Join-Path $testData $name)
    }
}

$rcInstallPassed = $false
$rcUninstallPassed = $false
$publicRestorePassed = $false
try {
    Invoke-Installer $rcSetup
    $rcInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installedExe)
    if ($rcInfo.ProductVersion -ne '0.2.1-rc.1' -or $rcInfo.FileVersion -ne '0.2.1.0') {
        throw "RC install metadata mismatch: $($rcInfo.ProductVersion) / $($rcInfo.FileVersion)"
    }
    Invoke-IsolatedInstalledSmoke '0.2.1-rc.1' 'rc1installedsmoke'
    $rcInstallPassed = $true

    $uninstaller = Join-Path $installDir 'unins000.exe'
    if (-not (Test-Path -LiteralPath $uninstaller)) {
        throw 'RC uninstaller was not found.'
    }
    $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @(
        '/VERYSILENT',
        '/SUPPRESSMSGBOXES',
        '/NORESTART'
    ) -PassThru -Wait
    if ($uninstall.ExitCode -ne 0) {
        throw "RC uninstall failed with exit code $($uninstall.ExitCode)"
    }
    if (Test-Path -LiteralPath $installedExe) {
        throw 'Installed RC executable remained after uninstall.'
    }
    if ((Get-DataFingerprint $dataDir) -ne $beforeFingerprint) {
        throw 'User data changed during RC install/uninstall.'
    }
    $rcUninstallPassed = $true

    Invoke-Installer $publicSetup
    Invoke-IsolatedInstalledSmoke '0.2.0' 'publicrestoresmoke'
    if ((Get-DataFingerprint $dataDir) -ne $beforeFingerprint) {
        throw 'User data changed after restoring public 0.2.0.'
    }
    $publicRestorePassed = $true

    [pscustomobject]@{
        UpgradeFrom = $original.ProductVersion
        UpgradeTo = $rcInfo.ProductVersion
        RCFileVersion = $rcInfo.FileVersion
        RCInstallAndRun = $rcInstallPassed
        RCUninstall = $rcUninstallPassed
        UserDataPreserved = $true
        PublicVersionRestored = $publicRestorePassed
        FinalInstalledVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installedExe).ProductVersion
        ResidualProcessCount = @(Get-Process -Name Hanki -ErrorAction SilentlyContinue).Count
    }
}
finally {
    Get-Process -Name Hanki -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $installedExe } |
        Stop-Process -ErrorAction SilentlyContinue
    if (-not (Test-Path -LiteralPath $installedExe) -or
        [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installedExe).ProductVersion -ne '0.2.0') {
        Invoke-Installer $publicSetup
    }
    Remove-Item Env:HANKI_DATA_DIRECTORY -ErrorAction SilentlyContinue
    Remove-Item Env:HANKI_INSTANCE_NAMESPACE -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $runRoot) {
        Remove-Item -LiteralPath $runRoot -Recurse -Force
    }
}
