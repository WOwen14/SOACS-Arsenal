[CmdletBinding()]
param(
    [string]$Repository,
    [string]$Manifest,
    [switch]$AutoReboot,
    [switch]$AllowFirmware
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Resolve the deployment root after the param block. In Windows PowerShell 5.1,
# $ScriptRoot can be empty while parameter default expressions are evaluated.
$ScriptRoot = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ScriptRoot)) {
    $ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
}
if ([string]::IsNullOrWhiteSpace($ScriptRoot)) {
    $ScriptRoot = (Get-Location).Path
}
if ([string]::IsNullOrWhiteSpace($Repository)) {
    $Repository = Join-Path $ScriptRoot 'Repository'
}
if ([string]::IsNullOrWhiteSpace($Manifest)) {
    $Manifest = Join-Path $ScriptRoot 'PatchManifest.xml'
}
$LogDir = Join-Path $ScriptRoot 'Logs'
New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$LogPath = Join-Path $LogDir "ArsenalDeployment_$Stamp.log"
$CsvPath = Join-Path $LogDir "ArsenalDeployment_$Stamp.csv"
$Results = New-Object System.Collections.Generic.List[object]

function Write-Log([string]$Message) {
    $line = "{0}  {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Message
    $line | Tee-Object -FilePath $LogPath -Append
}
function Assert-Administrator {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object Security.Principal.WindowsPrincipal($id)
    if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'This deployment must be run as Administrator.'
    }
}
function Add-Result($Package,$Status,$ExitCode,$Message) {
    $Results.Add([pscustomobject]@{Sequence=$Package.Sequence;File=$Package.FileName;Category=$Package.Category;Status=$Status;ExitCode=$ExitCode;Message=$Message;Time=(Get-Date)})
}
function Get-RuleCatalog {
    $ruleFile = Join-Path $ScriptRoot 'Rules\ApplicationRules.xml'
    if (-not (Test-Path $ruleFile)) { return @() }
    [xml]$xml = Get-Content $ruleFile
    return @($xml.ApplicationRules.Rule)
}
function Test-DeploymentHashes {
    $sumFile = Join-Path $ScriptRoot 'SHA256SUMS.txt'
    if (-not (Test-Path $sumFile)) { throw 'SHA256SUMS.txt is missing.' }
    foreach ($line in Get-Content $sumFile) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^([A-Fa-f0-9]{64})\s+\*(.+)$') { throw "Invalid hash line: $line" }
        $expected = $matches[1].ToUpperInvariant()
        $relative = $matches[2].Replace('/','\')
        $path = Join-Path $ScriptRoot $relative
        if (-not (Test-Path $path)) { throw "Deployment file missing: $relative" }
        $actual = (Get-FileHash -Algorithm SHA256 -Path $path).Hash.ToUpperInvariant()
        if ($actual -ne $expected) { throw "Deployment hash mismatch: $relative" }
    }
}
function Get-ExeArguments([string]$Path,$Rules) {
    $name = [IO.Path]::GetFileName($Path)
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    foreach ($r in $Rules) {
        $pattern = [string]$r.FileNamePattern
        $publisher = [string]$r.PublisherContains
        $product = [string]$r.ProductContains
        $ok = $true
        if ($pattern -and $name -notlike $pattern) { $ok = $false }
        if ($publisher -and ([string]$info.CompanyName) -notlike "*$publisher*") { $ok = $false }
        if ($product -and ([string]$info.ProductName) -notlike "*$product*") { $ok = $false }
        if ($ok) { return [string]$r.SilentArguments }
    }
    return $null
}
function Invoke-Installer($Package,$Rules) {
    $path = Join-Path $Repository $Package.RelativePath
    if (-not (Test-Path $path)) { throw "Package not found: $($Package.RelativePath)" }
    $ext = [IO.Path]::GetExtension($path).ToLowerInvariant()
    $category = [string]$Package.Category
    if ($category -match 'Firmware|BIOS' -and -not $AllowFirmware) {
        return @{Status='Manual';ExitCode=$null;Message='Firmware blocked. Re-run with -AllowFirmware after model/vendor qualification.'}
    }
    switch ($ext) {
        '.msu' { $exe='wusa.exe'; $args='"{0}" /quiet /norestart' -f $path }
        '.cab' { $exe='dism.exe'; $args='/Online /Add-Package /PackagePath:"{0}" /Quiet /NoRestart' -f $path }
        '.msi' { $exe='msiexec.exe'; $args='/i "{0}" /qn /norestart' -f $path }
        '.msp' { $exe='msiexec.exe'; $args='/p "{0}" /qn /norestart' -f $path }
        '.appx' { $exe='dism.exe'; $args='/Online /Add-ProvisionedAppxPackage /PackagePath:"{0}" /SkipLicense' -f $path }
        '.appxbundle' { $exe='dism.exe'; $args='/Online /Add-ProvisionedAppxPackage /PackagePath:"{0}" /SkipLicense' -f $path }
        '.msix' { $exe='dism.exe'; $args='/Online /Add-ProvisionedAppxPackage /PackagePath:"{0}" /SkipLicense' -f $path }
        '.msixbundle' { $exe='dism.exe'; $args='/Online /Add-ProvisionedAppxPackage /PackagePath:"{0}" /SkipLicense' -f $path }
        '.exe' {
            $silent = Get-ExeArguments $path $Rules
            if ([string]::IsNullOrWhiteSpace($silent)) { return @{Status='Manual';ExitCode=$null;Message='No matching silent-install rule.'} }
            $exe=$path; $args=$silent
        }
        default { return @{Status='Manual';ExitCode=$null;Message="Unsupported package type: $ext"} }
    }
    Write-Log "Starting: $($Package.FileName)"
    $p = Start-Process -FilePath $exe -ArgumentList $args -Wait -PassThru -WindowStyle Hidden
    $code = $p.ExitCode
    if ($code -in 0,1641,3010) {
        $msg = if ($code -in 1641,3010) {'Success; reboot required.'} else {'Success.'}
        return @{Status='Completed';ExitCode=$code;Message=$msg}
    }
    return @{Status='Failed';ExitCode=$code;Message="Installer returned exit code $code"}
}

try {
    Assert-Administrator
    Write-Log 'SOACS Arsenal standalone deployment started.'
    if (-not (Test-Path $Repository)) { throw "Repository not found: $Repository" }
    if (-not (Test-Path $Manifest)) { throw "Manifest not found: $Manifest" }
    Test-DeploymentHashes
    Write-Log 'Deployment hash validation passed.'
    [xml]$doc = Get-Content $Manifest
    $packages = @($doc.ArsenalDeployment.InstallPlan.Package | Sort-Object {[int]$_.Sequence})
    $rules = Get-RuleCatalog
    $rebootRequired = $false
    foreach ($pkg in $packages) {
        if ([string]$pkg.Automated -ne 'true') {
            Add-Result $pkg 'Manual' $null ([string]$pkg.Result)
            Write-Log "Manual: $($pkg.FileName)"
            continue
        }
        try {
            $r = Invoke-Installer $pkg $rules
            Add-Result $pkg $r.Status $r.ExitCode $r.Message
            Write-Log "$($r.Status): $($pkg.FileName) - $($r.Message)"
            if ($r.ExitCode -in 1641,3010) { $rebootRequired = $true }
            if ($r.Status -eq 'Failed') {
                Write-Host ("FAILED: {0} - {1}" -f $pkg.FileName, $r.Message) -ForegroundColor Red
                continue
            }
        } catch {
            Add-Result $pkg 'Failed' $null $_.Exception.Message
            Write-Log "Failed: $($pkg.FileName) - $($_.Exception.Message)"
            Write-Host ("FAILED: {0} - {1}" -f $pkg.FileName, $_.Exception.Message) -ForegroundColor Red
            continue
        }
    }
    $Results | Export-Csv -Path $CsvPath -NoTypeInformation
    Write-Log "Results written to $CsvPath"
    if ($rebootRequired) {
        Write-Log 'A reboot is required.'
        if ($AutoReboot) { Restart-Computer -Force }
    }
    $completedCount = @($Results | Where-Object { $_.Status -eq 'Completed' }).Count
    $failedCount = @($Results | Where-Object { $_.Status -eq 'Failed' }).Count
    $manualCount = @($Results | Where-Object { $_.Status -eq 'Manual' }).Count
    Write-Host ""
    Write-Host "SOACS Arsenal deployment complete." -ForegroundColor Cyan
    Write-Host ("Completed: {0}" -f $completedCount) -ForegroundColor Green
    Write-Host ("Failed:    {0}" -f $failedCount) -ForegroundColor $(if ($failedCount -gt 0) { 'Red' } else { 'Gray' })
    Write-Host ("Manual:    {0}" -f $manualCount) -ForegroundColor $(if ($manualCount -gt 0) { 'Yellow' } else { 'Gray' })
    Write-Host "Log: $LogPath"
    if ($failedCount -gt 0) { exit 2 }
} catch {
    Write-Log "FATAL: $($_.Exception.Message)"
    $Results | Export-Csv -Path $CsvPath -NoTypeInformation
    Write-Error $_
    exit 1
}
