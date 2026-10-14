param([string]$Project = (Join-Path (Split-Path $PSScriptRoot -Parent) 'TwinInspector'))
$ErrorActionPreference = 'Stop'
$env:UNITY_NO_BANNER = '1'
$env:UNITY_NON_INTERACTIVE = '1'
$env:UNITY_NO_PAGER = '1'
$cli = 'C:\Users\zackz\AppData\Local\Unity\bin\unity.exe'
& $cli command recompile_status --project-path $Project --result-only --timeout 120
if ($LASTEXITCODE -ne 0) { throw 'Unity compilation status unavailable.' }
& $cli command eval_file --file (Join-Path $PSScriptRoot 'unity\validate.cs') --project-path $Project --result-only --timeout 120
if ($LASTEXITCODE -ne 0) { throw 'OpenXR validation unavailable.' }
& $cli command console --level error --tail 30 --project-path $Project --result-only --timeout 120
if ($LASTEXITCODE -ne 0) { throw 'Unity console unavailable.' }
