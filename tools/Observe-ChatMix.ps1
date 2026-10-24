param([int]$Seconds = 30)
$ErrorActionPreference = 'Stop'
$core = Get-Content "$env:ProgramData\SteelSeries\GG\coreProps.json" | ConvertFrom-Json
$discovery = Invoke-RestMethod "https://$($core.ggEncryptedAddress)/subApps" -SkipCertificateCheck
$base = $discovery.subApps.sonar.metadata.webServerAddress
$route = '/v1/chatMix'
try { $initial = Invoke-RestMethod "$base$route" } catch { if ([int]$_.Exception.Response.StatusCode -ne 404) { throw }; $route = '/chatMix'; $initial = Invoke-RestMethod "$base$route" }
"$(Get-Date -Format o) GET $route $($initial | ConvertTo-Json -Compress)"
$socket = [System.Net.WebSockets.ClientWebSocket]::new()
$cts = [System.Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds($Seconds))
try {
  $null = $socket.ConnectAsync([uri]($base.Replace('http:', 'ws:') + '/sock'), $cts.Token).GetAwaiter().GetResult()
  $buffer = [byte[]]::new(65536)
  $message = [System.IO.MemoryStream]::new()
  while (!$cts.IsCancellationRequested) {
    $r = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $cts.Token).GetAwaiter().GetResult()
    if ($r.MessageType -eq 'Close') { break }
    $message.Write($buffer, 0, $r.Count)
    if ($r.EndOfMessage) {
      $text = [Text.Encoding]::UTF8.GetString($message.ToArray())
      if ($text -match 'CHATMIX') { "$(Get-Date -Format o) WS $text" }
      $message.SetLength(0)
    }
  }
} catch { if (!$cts.IsCancellationRequested) { throw } }
finally { $socket.Dispose(); $cts.Dispose() }
