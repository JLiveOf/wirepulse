param([Parameter(Mandatory)][string]$SourceDir,[Parameter(Mandatory)][string]$Version,[Parameter(Mandatory)][string]$OutputDir)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be major.minor.patch' }
$SourceDir = (Resolve-Path $SourceDir).Path
New-Item -ItemType Directory -Force $OutputDir | Out-Null
$OutputDir = (Resolve-Path $OutputDir).Path
foreach ($file in @('WirePulse.exe','Assets\controls.json','ThirdPartyNotices\COMPONENTS.txt')) {
    if (!(Test-Path (Join-Path $SourceDir $file))) { throw "Installer source is incomplete: $file" }
}
$compiler = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (!$compiler) { throw 'Install Inno Setup 6.4 or newer before building the Windows installer.' }
& $compiler "/DAppVersion=$Version" "/DSourceDir=$SourceDir" "/O$OutputDir" (Join-Path $PSScriptRoot 'WirePulse.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed: $LASTEXITCODE" }
$installer = Join-Path $OutputDir "WirePulse-$Version-win-x64-Setup.exe"
if (!(Test-Path $installer)) { throw 'Inno Setup did not create the installer' }
Write-Output $installer
