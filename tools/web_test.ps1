# 用無頭 Edge 跑網頁測試頁，依頁面標題（PASS／FAIL n／ERROR）決定結束碼
# 用法：powershell -File tools/web_test.ps1 [-Page preview/tests/golden.html] [-Shot 截圖.png]
param(
    [string]$Page = "preview/tests/golden.html",
    [string]$Shot = "",
    [int]$Port = 8793,
    [int]$Budget = 20000
)
$root = Split-Path $PSScriptRoot -Parent
$edge = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw "找不到 Microsoft Edge" }

# 先關掉上次沒關乾淨、還佔著這個連接埠的伺服器
Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
$server = Start-Process python -ArgumentList @("`"$root\tools\serve.py`"", $Port, "--no-open") -PassThru -WindowStyle Hidden
$code = 1
$profile = Join-Path $env:TEMP "deskpet-webtest-$Port"
try {
    Start-Sleep -Milliseconds 800
    $url = "http://127.0.0.1:$Port/$Page"
    $dom = Join-Path $env:TEMP "deskpet-webtest-$Port.html"
    $args = @("--headless=new", "--disable-gpu", "--user-data-dir=`"$profile`"", "--virtual-time-budget=$Budget", "--dump-dom", $url)
    Start-Process -FilePath $edge -ArgumentList $args -RedirectStandardOutput $dom -Wait -WindowStyle Hidden
    if ($Shot) {
        Start-Process -FilePath $edge -ArgumentList @("--headless=new", "--disable-gpu", "--user-data-dir=`"$profile`"", "--window-size=1300,900",
            "--virtual-time-budget=$Budget", "--screenshot=`"$Shot`"", $url) -Wait -WindowStyle Hidden
    }
    $html = Get-Content $dom -Raw -Encoding utf8
    $title = if ($html -match '<title>(.*?)</title>') { $Matches[1] } else { "（沒有讀到頁面）" }
    $body = ($html -replace '(?s)<(script|style)\b.*?</\1>', '' -replace '<[^>]+>', ' ' -replace '\s+', ' ').Trim()
    Write-Output "[$title] $Page"
    Write-Output ($body.Substring(0, [Math]::Min(1500, $body.Length)))
    $code = if ($title -eq "PASS") { 0 } else { 1 }
} finally {
    # python 可能是啟動器（真正的伺服器是子程序），所以依連接埠找出來關掉
    Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
}
# exit 放在 finally 之後：在 try 裡 exit 不會執行 finally（伺服器會一直開著）
exit $code
