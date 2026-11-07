# Discord 連線的自動測試：啟動假伺服器（tools/mock_discord.ps1），讓桌寵連過去，檢查收到的事件
# 用法：powershell -File tools/test_discord.ps1     （使用者的桌寵、真的 Discord 開著也能跑）
$root = Split-Path $PSScriptRoot -Parent
$godot = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter "Godot_v4*_mono_win64_console.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $godot) { throw "找不到 Godot .NET 版" }
$pipe = "deskpet-mock-discord-$PID"
$log = Join-Path $env:TEMP "deskpet-test-discord.log"
$mockLog = Join-Path $env:TEMP "deskpet-mock-discord.log"

$mock = Start-Process powershell -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$root\tools\mock_discord.ps1`"", "-Pipe", $pipe) `
    -RedirectStandardOutput $mockLog -PassThru -WindowStyle Hidden
Start-Sleep 1
$env:DESKPET_DISCORD_PIPE = $pipe
$pet = Start-Process -FilePath $godot.FullName -ArgumentList @("--path", "`"$root\godot`"", "--", "--sandbox", "--mute", "--dump", "--no-single", "--discord=999:faketoken") `
    -RedirectStandardOutput $log -PassThru -WindowStyle Hidden
Remove-Item Env:DESKPET_DISCORD_PIPE
Wait-Process -Id $mock.Id -Timeout 40 -ErrorAction SilentlyContinue
Start-Sleep 2
Stop-Process -Id $pet.Id -Force -ErrorAction SilentlyContinue
Stop-Process -Id $mock.Id -Force -ErrorAction SilentlyContinue

$out = Get-Content $log -Encoding utf8
$expected = @(
    '\[guest\] \+ Alice \(discord:222\)',
    '\[presence\] discord Mute Bob\(333\)',
    '\[presence\] discord Speaking \(222\)',
    '\[presence\] discord Silent \(222\)',
    '\[presence\] discord Unmute Bob\(333\)',
    '\[guest\] \+ Carol \(discord:444\)',
    '\[presence\] discord Leave \(222\)',
    '\[presence\] discord Leave \(333\)',
    '\[presence\] discord Leave \(444\)'
)
$missing = $expected | Where-Object { -not ($out -match $_) }
$self = $out -match 'Join Me|discord:111'
if ($self) { $missing += "自己（111）不應該變成客人" }
if ($missing) {
    Write-Output "✗ Discord 測試失敗，少了："
    $missing | ForEach-Object { Write-Output "  $_" }
    Write-Output "--- 桌寵輸出（$log）"; $out | Select-String "presence|guest|discord" | Select-Object -First 30
    exit 1
}
Write-Output "✓ Discord 測試通過（$($expected.Count) 個事件）"
