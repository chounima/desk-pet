# 效能量測：客人角色 1、8、20 隻時每幀處理時間（_Process，不含繪製），結果附加到 build/perf.txt
# 用法：powershell -File tools/perf.ps1 [-Label 升級前]
param([string]$Label = "", [int[]]$Counts = @(1, 8, 20), [string]$Extra = "")
$root = Split-Path $PSScriptRoot -Parent
$godot = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter "Godot_v4*_mono_win64_console.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $godot) { throw "找不到 Godot .NET 版" }
$commit = (git -C $root log --oneline -1) -join ""
$lines = @("## $(Get-Date -Format 'yyyy-MM-dd HH:mm') $Label（$commit）")
foreach ($n in $Counts) {
    $log = Join-Path $env:TEMP "deskpet-perf-$n.log"
    Remove-Item $log -ErrorAction SilentlyContinue        # 不要讀到上一次的結果
    $argList = @("--path", "`"$root\godot`"", "--", "--sandbox", "--mute", "--no-single", "--perf=$n")
    if ($Extra) { $argList += $Extra }
    $p = Start-Process -FilePath $godot.FullName -ArgumentList $argList `
        -RedirectStandardOutput $log -PassThru -WindowStyle Hidden
    Wait-Process -Id $p.Id -Timeout 60 -ErrorAction SilentlyContinue
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    $line = Get-Content $log -Encoding utf8 | Select-String "\[perf\] 客人" | Select-Object -Last 1
    $lines += if ($line) { "- $($line.Line -replace '^\[perf\] ', '')" } else { "- 客人 ${n}：沒有結果（看 $log）" }
}
New-Item -ItemType Directory -Force (Join-Path $root "build") | Out-Null
Add-Content -Path (Join-Path $root "build\perf.txt") -Value ($lines + "") -Encoding utf8
$lines
