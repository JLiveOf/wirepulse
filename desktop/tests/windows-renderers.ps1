$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force test-results/renderers | Out-Null
foreach ($mode in @('Automático','DirectX 11','Vulkan','OpenGL','CPU (compatibilidad)')) {
    $name = @{'Automático'='auto';'DirectX 11'='dx11';'Vulkan'='vulkan';'OpenGL'='opengl';'CPU (compatibilidad)'='cpu'}[$mode]
    $report = Join-Path $PWD "test-results/renderers/$name.json"
    $process = Start-Process -FilePath "$PWD/publish/WirePulse.exe" -ArgumentList @('--renderer-smoke-test','--renderer',('"'+$mode+'"'),'--probe-output',('"'+$report+'"')) -PassThru -RedirectStandardOutput "test-results/renderers/$name.log" -RedirectStandardError "test-results/renderers/$name.err"
    if (!$process.WaitForExit(25000)) { $process.Kill($true); throw "Renderer timed out: $mode" }
    if ($process.ExitCode -ne 0 -or !(Test-Path $report)) { Get-Content "test-results/renderers/$name.err"; throw "Renderer failed: $mode" }
    $result = Get-Content $report -Raw | ConvertFrom-Json
    if (!$result.valid -or $result.frames -lt 5) { throw "No native frames for $mode" }
    if ($mode -eq 'CPU (compatibilidad)' -and $result.active -ne 'CPU (compatibilidad)') { throw 'CPU request was ignored' }
    Write-Output "PASS $mode -> $($result.active): $($result.frames) native frames; $($result.description)"
}
# Exercise the exact attachment path against classic and raised desktop-like parents.
# The fixture owns all HWNDs and never reparents or modifies Explorer itself.
foreach ($layout in @('classic','raised')) {
    foreach ($mode in @('CPU (compatibilidad)','OpenGL')) {
        $name = $layout + '-' + $(if ($mode -eq 'OpenGL') {'opengl'} else {'cpu'})
        $report = Join-Path $PWD "test-results/renderers/$name.json"
        $arguments = @('--renderer-smoke-test','--desktop-fixture','--renderer',('"'+$mode+'"'),'--probe-output',('"'+$report+'"'))
        if ($layout -eq 'raised') { $arguments += '--raised-desktop' }
        $process = Start-Process -FilePath "$PWD/publish/WirePulse.exe" -ArgumentList $arguments -PassThru -RedirectStandardOutput "test-results/renderers/$name.log" -RedirectStandardError "test-results/renderers/$name.err"
        if (!$process.WaitForExit(25000)) { $process.Kill($true); throw "Desktop fixture timed out: $name" }
        if ($process.ExitCode -ne 0 -or !(Test-Path $report)) { Get-Content "test-results/renderers/$name.err"; throw "Desktop fixture failed: $name" }
        $result = Get-Content $report -Raw | ConvertFrom-Json
        if (!$result.visible -or $result.matching -lt 7) { throw "Wallpaper pixels are not visible: $name" }
        if ($layout -eq 'raised' -and ($result.presentation.window.classHasOwnDc -or !$result.presentation.window.layered -or $result.presentation.frames -lt 1)) { throw "Incompatible layered-window state: $name" }
        Write-Output "PASS ${name}: $($result.matching)/$($result.samples) desktop pixels visible; $($result.active)"
    }
}
# Regression control: the old class flag must reproduce the invisible raised CPU wallpaper.
$report = Join-Path $PWD 'test-results/renderers/legacy-raised-cpu.json'
$process = Start-Process -FilePath "$PWD/publish/WirePulse.exe" -ArgumentList @('--renderer-smoke-test','--desktop-fixture','--raised-desktop','--legacy-class-style','--renderer','"CPU (compatibilidad)"','--probe-output',('"'+$report+'"')) -PassThru -RedirectStandardOutput 'test-results/renderers/legacy-raised-cpu.log' -RedirectStandardError 'test-results/renderers/legacy-raised-cpu.err'
if (!$process.WaitForExit(25000)) { $process.Kill($true); throw 'Regression control timed out' }
if (!(Test-Path $report)) { throw 'Regression control did not produce a report' }
$result = Get-Content $report -Raw | ConvertFrom-Json
if ($process.ExitCode -ne 1 -or $result.visible -or !$result.presentation.classHasOwnDc) { throw 'Old presentation no longer reproduces the bug; inspect fixture' }
Write-Output 'PASS old class flags reproduce invisible wallpaper; corrected flags show desktop pixels'
