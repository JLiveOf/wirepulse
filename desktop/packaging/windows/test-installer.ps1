param([Parameter(Mandatory)][string]$Installer,[Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'
if ($env:CI -ne 'true') { throw 'Run these install/uninstall tests in the disposable CI account.' }
$Installer = (Resolve-Path $Installer).Path
$install = Join-Path $env:RUNNER_TEMP 'WirePulse Installation Test'
$logs = Join-Path $env:RUNNER_TEMP 'wirepulse-installer-logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'WirePulse.lnk'
$safeShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'WirePulse (modo compatible).lnk'
$arp = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{E547F934-57DE-4EED-A32D-1A5E1563B944}_is1'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$prefs = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'WirePulse'
New-Item -ItemType Directory -Force $prefs | Out-Null
$sentinel = Join-Path $prefs ("installer-preservation-" + [guid]::NewGuid() + '.txt')
Set-Content $sentinel 'keep-user-preferences'
function Invoke-Checked($File, $Arguments) {
    $p = Start-Process -FilePath $File -ArgumentList $Arguments -PassThru
    if (!$p.WaitForExit(120000)) { $p.Kill(); throw "Timed out: $File" }
    if ($p.ExitCode -ne 0) { throw "Process failed: $File ($($p.ExitCode))" }
}
try {
    foreach ($round in 1,2) {
        Invoke-Checked $Installer @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/DIR=`"$install`"", "/LOG=`"$logs\install-$round.log`"")
        foreach ($file in @('WirePulse.exe','WirePulse-SafeMode.cmd','Assets\controls.json','ThirdPartyNotices\COMPONENTS.txt','unins000.exe')) {
            if (!(Test-Path (Join-Path $install $file))) { throw "Missing installed file: $file" }
        }
        if (!(Test-Path $shortcut)) { throw 'Start menu shortcut was not created' }
        $link = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut)
        if ($link.TargetPath -ne (Join-Path $install 'WirePulse.exe')) { throw 'Shortcut target is incorrect' }
        if (!(Test-Path $safeShortcut)) { throw 'Safe-mode shortcut was not created' }
        $safeLink = (New-Object -ComObject WScript.Shell).CreateShortcut($safeShortcut)
        if ($safeLink.Arguments -ne '--renderer "CPU (compatibilidad)"') { throw 'Safe-mode arguments are incorrect' }
        if ((Get-ItemProperty $arp).DisplayVersion -ne $Version) { throw 'Uninstall registration has incorrect version' }
        Invoke-Checked (Join-Path $install 'WirePulse.exe') @('--self-test')
        if ((Get-Content $sentinel).Trim() -ne 'keep-user-preferences') { throw 'Upgrade changed user preferences' }
        Write-Output "PASS install/reinstall $round, menu shortcut, installed app self-test and preserved preferences"
    }
    New-Item -Path $runKey -Force | Out-Null
    New-ItemProperty $runKey -Name 'WirePulse' -Value "`"$install\WirePulse.exe`" --autostart --no-settings" -PropertyType String -Force | Out-Null
    Invoke-Checked (Join-Path $install 'unins000.exe') @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=`"$logs\uninstall.log`"")
    if (Test-Path (Join-Path $install 'WirePulse.exe')) { throw 'Uninstall left the app executable' }
    if (Test-Path $shortcut) { throw 'Uninstall left the menu shortcut' }
    if (Test-Path $safeShortcut) { throw 'Uninstall left safe-mode shortcut' }
    if (Test-Path $arp) { throw 'Uninstall left its registration' }
    if (Get-ItemProperty $runKey -Name WirePulse -ErrorAction SilentlyContinue) { throw 'Uninstall left startup registration for the removed executable' }
    if (!(Test-Path $sentinel)) { throw 'Uninstall deleted user preferences' }
    Write-Output 'PASS uninstall removes app, shortcuts and owned startup entry; user preferences remain'
} finally { Remove-Item $sentinel -Force -ErrorAction SilentlyContinue }
