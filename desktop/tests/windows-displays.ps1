$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
if(!('WirePulseTransparentOverlay' -as [type])){Add-Type -ReferencedAssemblies @('System.Windows.Forms','System.ComponentModel.Primitives','System.Runtime','System.Windows.Forms.Primitives') @'
using System.Windows.Forms;
public class WirePulseTransparentOverlay:Form {
 protected override bool ShowWithoutActivation { get { return true; } }
 protected override CreateParams CreateParams { get { var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x20;return p; } }
}
'@}
$screens=[System.Windows.Forms.Screen]::AllScreens
function Get-Coverage($screen){
 $b=$screen.Bounds
 $rect="[$($b.X),$($b.Y),$($b.Width),$($b.Height)]"
 (& ./publish/WirePulse.exe --coverage-probe $rect | ConvertFrom-Json).reason
}
$results=@()
foreach($screen in $screens){
 $beforeOverlay=Get-Coverage $screen
 $overlay=New-Object WirePulseTransparentOverlay
 try{
  $overlay.FormBorderStyle='None';$overlay.ShowInTaskbar=$false;$overlay.StartPosition='Manual';$overlay.Bounds=$screen.Bounds;$overlay.BackColor=[System.Drawing.Color]::Magenta;$overlay.TransparencyKey=[System.Drawing.Color]::Magenta;$overlay.TopMost=$true;$overlay.Show();[System.Windows.Forms.Application]::DoEvents()
  if((Get-Coverage $screen) -ne $beforeOverlay){throw 'Transparent nonactivating overlay suspended the wallpaper'}
 }finally{$overlay.Close();$overlay.Dispose()}
 $form=New-Object System.Windows.Forms.Form
 try{
  $form.Text='WirePulse · prueba de pantalla';$form.StartPosition='Manual';$form.Location=New-Object System.Drawing.Point(($screen.Bounds.X+50),($screen.Bounds.Y+50));$form.Size=New-Object System.Drawing.Size(400,240);$form.TopMost=$true;$form.Show();[System.Windows.Forms.Application]::DoEvents()
  $baseline=@{};foreach($other in $screens){if($other.DeviceName -ne $screen.DeviceName){$baseline[$other.DeviceName]=Get-Coverage $other}}
  $form.WindowState='Maximized';[System.Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 200;[System.Windows.Forms.Application]::DoEvents()
  $reason=Get-Coverage $screen; if($reason -ne 'ventana maximizada'){throw "Maximized window not detected on $($screen.DeviceName): reason=$reason; bounds=$($screen.Bounds); form=$($form.Bounds); actual=$([System.Windows.Forms.Screen]::FromHandle($form.Handle).DeviceName)"}
  foreach($other in $screens){if($other.DeviceName -ne $screen.DeviceName -and (Get-Coverage $other) -ne $baseline[$other.DeviceName]){throw 'Maximizing one screen changed suspension on another'}}
  $form.WindowState='Minimized';[System.Windows.Forms.Application]::DoEvents()
  if((Get-Coverage $screen) -eq 'ventana maximizada'){throw 'Minimized fixture still suspends its screen'}
  $results+=@{display=$screen.DeviceName;bounds=$screen.Bounds.ToString();valid=$true}
  Write-Output "PASS maximized/minimized coverage is independent on $($screen.DeviceName)"
 }finally{$form.Close();$form.Dispose()}
}
New-Item -ItemType Directory -Force test-results | Out-Null
@{valid=$true;count=$screens.Count;displays=$results} | ConvertTo-Json -Depth 5 | Set-Content test-results/windows-displays.json -Encoding utf8
