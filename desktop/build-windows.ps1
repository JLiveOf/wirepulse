param([string]$Dotnet='dotnet',[switch]$Test)
$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
 python ../scripts/build-info.py Assets/build-info.json
 if($LASTEXITCODE -ne 0){throw 'Build identity failed'}
 & $Dotnet publish WirePulse.csproj -c Release -f net10.0-windows10.0.19041.0 -r win-x64 --self-contained true -o publish -p:DebugType=None -p:DebugSymbols=false
 if($LASTEXITCODE -ne 0){throw 'Windows renderer build failed'}
 & $Dotnet publish WindowsSettings/WirePulse.Settings.csproj -c Release -o publish/WindowsSettings -p:DebugType=None -p:DebugSymbols=false
 if($LASTEXITCODE -ne 0){throw 'WinUI settings build failed'}
 if(!(Test-Path publish/WindowsSettings/WirePulse.Settings.pri)){throw 'WinUI resource index was not published'}
 # Redistribute notices from the exact restored WinUI dependencies as well as the renderer.
 foreach($dependencySet in @(@{Assets="obj/project.assets.json";Name="Renderer"},@{Assets="WindowsSettings/obj/project.assets.json";Name="WindowsSettings"})){
 $assets=Get-Content $dependencySet.Assets -Raw | ConvertFrom-Json
 $noticeDir=Join-Path $PWD ('publish/ThirdPartyNotices/'+$dependencySet.Name)
 New-Item -ItemType Directory -Force $noticeDir | Out-Null
 $index=[Collections.Generic.List[string]]::new()
 foreach($library in $assets.libraries.PSObject.Properties){
  if($library.Value.type -ne 'package'){continue}
  $index.Add($library.Name)
  $package=$null
  foreach($folder in $assets.packageFolders.PSObject.Properties.Name){$candidate=Join-Path $folder $library.Value.path;if(Test-Path $candidate){$package=$candidate;break}}
  if(!$package){continue}
  foreach($spec in Get-ChildItem $package -Filter '*.nuspec'){
   [xml]$xml=Get-Content $spec.FullName -Raw
   $metadata=$xml.package.metadata
   $index.Add("License: $($metadata.license) $($metadata.licenseUrl)")
  }
  $destination=Join-Path $noticeDir ($library.Name.Replace('/','-'))
  Get-ChildItem $package -Recurse -File | Where-Object { $_.Name -match 'license|licence|notice|copying' -and $_.Extension -in @('','.txt','.md','.html','.rtf') } | ForEach-Object {
   New-Item -ItemType Directory -Force $destination | Out-Null
   $relative=$_.FullName.Substring($package.Length).TrimStart('\','/').Replace('\','_').Replace('/','_')
   Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $destination $relative)
  }
 }
 $index | Set-Content (Join-Path $noticeDir 'COMPONENTS.txt') -Encoding utf8
 }
 Copy-Item ThirdParty/* publish/ThirdPartyNotices -Recurse -Force
 "Renderer and WindowsSettings subdirectories contain the exact dependency notices." | Set-Content publish/ThirdPartyNotices/COMPONENTS.txt -Encoding utf8
 if($Test){
  New-Item -ItemType Directory -Force test-results | Out-Null
  & "$PWD/publish/WirePulse.exe" --self-test
  if($LASTEXITCODE -ne 0){throw "Windows self-test failed before UI probes"}
  foreach($case in @(
   @{Name='editor-save';Args=@('--winui-editor-test-output','test-results/editor-save.json')},
   @{Name='editor-discard';Args=@('--winui-editor-test-output','test-results/editor-discard.json','--editor-test-discard')},
   @{Name='monitor-fps';Args=@('--windows-fps-test-output','test-results/monitor-fps.json')},
   @{Name='winui-native';Args=@('--winui-test-output','test-results/winui-native.json','--probe-seconds','35','--autostart')},
   @{Name='video-cadence';Args=@('--windows-video-test','--verify-video-cadence','--background-fixture','tests/fixtures/background.mp4','--probe-output','test-results/video-cadence.json')},
   @{Name='video-lifecycle';Args=@('--windows-video-test','--stress-disposal','--background-fixture','tests/fixtures/background.mp4','--probe-output','test-results/video-lifecycle.json')},
   @{Name='audio-loopback';Args=@('--windows-audio-test','--probe-output','test-results/audio-loopback.json')}
  )){
   $process=Start-Process -WindowStyle Hidden -FilePath "$PWD/publish/WirePulse.exe" -ArgumentList $case.Args -PassThru -RedirectStandardOutput "test-results/$($case.Name).log" -RedirectStandardError "test-results/$($case.Name).err"
   if(!$process.WaitForExit(45000)){$process.Kill($true);throw "$($case.Name) timed out"}
   foreach($diagnostic in @('WirePulse-editor-error.txt','WirePulse-settings-error.txt','WirePulse-editor-state.json')){if(Test-Path "$env:TEMP/$diagnostic"){Copy-Item "$env:TEMP/$diagnostic" "test-results/$($case.Name)-$diagnostic" -Force}}
   if(!(Test-Path "test-results/$($case.Name).json")){Get-Content "test-results/$($case.Name).err" -ErrorAction SilentlyContinue;throw "$($case.Name) produced no report"}
   $report=Get-Content "test-results/$($case.Name).json" -Raw | ConvertFrom-Json
   if($process.ExitCode -ne 0 -or !$report.valid){Get-Content "test-results/$($case.Name)*.json","test-results/$($case.Name)*.txt" -ErrorAction SilentlyContinue;throw "$($case.Name) failed; inspect test-results"}
   Write-Output "PASS $($case.Name)"
  }
 }
} finally { Pop-Location }
