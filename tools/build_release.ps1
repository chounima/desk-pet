# 打包桌寵成可以獨立執行的版本：同步資料 → Godot 匯出 → 壓縮成 zip
# 用法：powershell -File tools/build_release.ps1
# 產出：build/DeskPet/（DeskPet.exe + data_DeskPet_windows_x86_64 .NET 資料夾，兩者要放在一起）
#       與 build/DeskPet-<日期>.zip
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "godot"
$outDir = Join-Path $root "build\DeskPet"

$console = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter "Godot_v4*_mono_win64_console.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $console) { throw "找不到 Godot .NET 版，請先執行：winget install GodotEngine.GodotEngine.Mono" }
$version = (& $console.FullName --version).Trim() -replace '\.official\..*$', ''     # 例：4.7.2.stable.mono
$templates = Join-Path $env:APPDATA "Godot\export_templates\$version"
if (-not (Test-Path (Join-Path $templates "windows_release_x86_64.exe"))) {
    throw "缺少匯出模板 $version，請到 Godot 編輯器「編輯器 > 管理匯出模板」下載，或解壓縮官方 export_templates.tpz 到 $templates"
}

Write-Host "1/4 同步角色與動作資料"
python (Join-Path $root "tools\sync_content.py")

# Godot 的警告會寫到 stderr；Windows PowerShell 5.1 會把它當成錯誤而中斷腳本，
# 所以透過 cmd 執行並把輸出寫進記錄檔，再自己挑出錯誤
function Invoke-Godot([string]$arguments, [string]$log) {
    cmd /c "`"$($console.FullName)`" $arguments > `"$log`" 2>&1"
    $errors = @(Get-Content $log -Encoding utf8 | Select-String "^ERROR:")
    $errors | Select-Object -First 5 | ForEach-Object { Write-Warning $_.Line }
    return $errors.Count
}
# 匯出時 Godot 會呼叫 dotnet 編譯 C#；MSBuild 節點／編譯伺服器預設會常駐並繼承記錄檔的輸出管線，
# 造成 cmd 一直等不到結束，所以關掉常駐，最後再保險關一次
$env:MSBUILDDISABLENODEREUSE = "1"
$env:DOTNET_CLI_USE_MSBUILD_SERVER = "0"
$env:UseSharedCompilation = "false"
$logDir = Join-Path $root "build"
New-Item -ItemType Directory -Force $logDir | Out-Null

Write-Host "2/4 匯入資源"
Invoke-Godot "--headless --path `"$project`" --import" (Join-Path $logDir "import.log") | Out-Null

Write-Host "3/4 匯出 Windows 版"
if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
New-Item -ItemType Directory -Force $outDir | Out-Null
$exe = Join-Path $outDir "DeskPet.exe"
$exportErrors = Invoke-Godot "--headless --path `"$project`" --export-release `"Windows Desktop`" `"$exe`"" (Join-Path $logDir "export.log")
dotnet build-server shutdown | Out-Null
if (-not (Test-Path $exe)) { throw "匯出失敗：沒有產生 DeskPet.exe（詳見 build\export.log）" }
# 匯出過程有錯誤時 exe 可能還是會產生，但功能不完整（例如 C# 沒打包進去），直接判定失敗
if ($exportErrors -gt 0) { throw "匯出有 $exportErrors 個錯誤（詳見 build\export.log），產物可能無法正常執行" }

Write-Host "4/4 壓縮"
$zip = Join-Path $root ("build\DeskPet-{0}.zip" -f (Get-Date -Format "yyyyMMdd"))
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zip
$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "完成：$outDir"
Write-Host "      $zip（$size MB）"
