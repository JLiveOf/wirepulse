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
