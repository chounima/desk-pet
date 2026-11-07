# 假的 Discord 本機 RPC 伺服器：測試 DiscordRpc 的封包與事件處理（不需要真的 Discord 和帳號）
# 用法：powershell -File tools/mock_discord.ps1 [-Pipe deskpet-mock-discord]
#   一般由 tools/test_discord.ps1 啟動；桌寵用環境變數 DESKPET_DISCORD_PIPE 連過來
# 劇本：READY → 認證 → 頻道 general 裡有自己(111)、Alice(222)、Bob(333，靜音)
#       → Alice 講話、自己講話 → Bob 解除靜音、Carol(444) 進來 → Alice 離開 → 自己離開語音頻道
param([string]$Pipe = "deskpet-mock-discord")
$ErrorActionPreference = "Stop"
$server = New-Object System.IO.Pipes.NamedPipeServerStream($Pipe, [System.IO.Pipes.PipeDirection]::InOut, 1)
Write-Output "waiting on $Pipe"
$server.WaitForConnection()
Write-Output "connected"

function Read-Frame {
    $h = New-Object byte[] 8; $n = 0
    while ($n -lt 8) { $r = $server.Read($h, $n, 8 - $n); if ($r -le 0) { throw "closed" }; $n += $r }
    $len = [BitConverter]::ToInt32($h, 4); $b = New-Object byte[] $len; $n = 0
    while ($n -lt $len) { $r = $server.Read($b, $n, $len - $n); if ($r -le 0) { throw "closed" }; $n += $r }
    $text = [Text.Encoding]::UTF8.GetString($b)
    [Console]::Out.WriteLine("<< $text")
    return ($text | ConvertFrom-Json)
}
function Send-Frame($obj) {
    $text = $obj | ConvertTo-Json -Depth 10 -Compress
    $b = [Text.Encoding]::UTF8.GetBytes($text)
    $f = [BitConverter]::GetBytes([int]1) + [BitConverter]::GetBytes([int]$b.Length) + $b
    $server.Write($f, 0, $f.Length); $server.Flush()
    [Console]::Out.WriteLine(">> $text")
}
function Voice($id, $name, $mute) {
    @{ nick = $name; mute = $false; voice_state = @{ self_mute = $mute; self_deaf = $false; mute = $false; deaf = $false }; user = @{ id = $id; username = $name.ToLower() } }
}
function Event($evt, $data) { Send-Frame @{ cmd = "DISPATCH"; evt = $evt; data = $data } }
# 回應指令，直到收到 untilEvt 的 SUBSCRIBE（或 count 個指令）
function Answer([string]$untilEvt = "", [int]$count = 0) {
    $i = 0
    while ($true) {
        $m = Read-Frame
        $data = switch ($m.cmd) {
            "AUTHENTICATE" { @{ user = @{ id = "111" } } }
            "GET_SELECTED_VOICE_CHANNEL" { @{ id = "900"; name = "general"; voice_states = @((Voice "111" "Me" $false), (Voice "222" "Alice" $false), (Voice "333" "Bob" $true)) } }
            default { @{ evt = $m.evt } }
        }
        Send-Frame @{ cmd = $m.cmd; nonce = $m.nonce; data = $data }
        $i++
        if ($untilEvt -and $m.cmd -eq "SUBSCRIBE" -and $m.evt -eq $untilEvt) { return }
        if ($count -and $i -ge $count) { return }
    }
}

$null = Read-Frame                    # 握手
Event "READY" @{ v = 1; user = @{ id = "111"; username = "me" } }
Answer -untilEvt "SPEAKING_STOP"      # 認證、訂閱、取得頻道、訂閱頻道事件
Start-Sleep 3
Event "SPEAKING_START" @{ channel_id = "900"; user_id = "222" }
Event "SPEAKING_START" @{ channel_id = "900"; user_id = "111" }
Start-Sleep 3
Event "SPEAKING_STOP" @{ channel_id = "900"; user_id = "111" }
Event "SPEAKING_STOP" @{ channel_id = "900"; user_id = "222" }
Event "VOICE_STATE_UPDATE" (Voice "333" "Bob" $false)
Event "VOICE_STATE_CREATE" (Voice "444" "Carol" $false)
Start-Sleep 3
Event "VOICE_STATE_DELETE" (Voice "222" "Alice" $false)
Start-Sleep 2
Event "VOICE_CHANNEL_SELECT" @{ channel_id = $null }
Answer -count 5                       # 退訂 5 種頻道事件
Start-Sleep 3
Write-Output "mock done"
