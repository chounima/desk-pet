# 建置並啟動桌寵（Godot 版）
# 用法：powershell -File tools/run_pet.ps1 [-Char chouma] [-Action typing] [-Dump] [-NoBuild]
#   結束：系統匣圖示右鍵 →「結束」
param(
    [string]$Char = "",
    [string]$Action = "",
    [switch]$Dump,
    [switch]$NoBuild
)

$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "godot"

$godot = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter "Godot_v4*_mono_win64.exe" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -notlike "*console*" } | Select-Object -First 1
if (-not $godot) { throw "找不到 Godot .NET 版，請先執行：winget install GodotEngine.GodotEngine.Mono" }

if (-not $NoBuild) {
    Push-Location $project
    dotnet build --nologo -v q
    if ($LASTEXITCODE -ne 0) { Pop-Location; throw "建置失敗" }
    if (-not (Test-Path ".godot\imported")) { & ($godot.FullName -replace '\.exe$', '_console.exe') --headless --path . --import | Out-Null }
    Pop-Location
}

$userArgs = @()
if ($Char) { $userArgs += "--char=$Char" }
if ($Action) { $userArgs += "--action=$Action" }
if ($Dump) { $userArgs += "--dump" }
$argList = @("--path", $project)
if ($userArgs) { $argList += "--"; $argList += $userArgs }

Start-Process -FilePath $godot.FullName -ArgumentList $argList
"桌寵已啟動（結束：系統匣圖示右鍵 →「結束」）"
