$ErrorActionPreference = 'Stop'

# End-to-end check that the lobby's TCP relay accepts the exact header the
# engine writes and splices the two peers. Written independently of the Go
# client so a change on either side shows up here.
$base = 'http://127.0.0.1:8099'
$relayPort = 8098

function Post($path, $body, $token) {
    $headers = @{}
    if ($token) { $headers['Authorization'] = "Bearer $token" }
    $json = if ($null -ne $body) { $body | ConvertTo-Json -Compress } else { '{}' }
    Invoke-RestMethod -Method Post -Uri "$base$path" -Body $json -ContentType 'application/json' -Headers $headers -TimeoutSec 5
}

$a = Post '/api/session' @{ name = 'HostPlayer'; port = 7600 } $null
$b = Post '/api/session' @{ name = 'GuestPlayer'; port = 7600 } $null
Write-Host "sessions: $($a.id) / $($b.id)"

$room = (Post '/api/rooms/create' @{ name = 'TestRoom'; rollback = $true } $a.token).room
Post '/api/rooms/join' @{ roomId = $room.id } $b.token | Out-Null
Post '/api/match/start' $null $a.token | Out-Null

$ra = Post '/api/relay/allocate' $null $a.token
$rb = Post '/api/relay/allocate' $null $b.token
Write-Host "relay key $($ra.key) tcpPort $($ra.tcpPort) slots $($ra.slot)/$($rb.slot)"

if ($ra.key -ne $rb.key) { throw "peers got different relay keys" }
if ($ra.tcpPort -eq 0) { throw "server advertised no TCP relay port" }

function Connect-Relay($keyHex, $slot) {
    $c = New-Object System.Net.Sockets.TcpClient
    $c.Connect('127.0.0.1', $relayPort)
    $s = $c.GetStream()
    $hdr = [System.Collections.Generic.List[byte]]::new()
    $hdr.AddRange([Text.Encoding]::ASCII.GetBytes('IKTCPRLY'))
    for ($i = 0; $i -lt $keyHex.Length; $i += 2) {
        $hdr.Add([Convert]::ToByte($keyHex.Substring($i, 2), 16))
    }
    $hdr.Add([byte]$slot)
    $s.Write($hdr.ToArray(), 0, $hdr.Count)
    $s.Flush()
    return @{ Client = $c; Stream = $s }
}

$ca = Connect-Relay $ra.key $ra.slot
$cb = Connect-Relay $rb.key $rb.slot

# Ikemen's own handshake, in both directions.
$tok = [Text.Encoding]::ASCII.GetBytes('IKEMENGO')
$ca.Stream.Write($tok, 0, $tok.Length); $ca.Stream.Flush()

$buf = New-Object byte[] 8
$cb.Stream.ReadTimeout = 5000
$read = 0
while ($read -lt 8) { $read += $cb.Stream.Read($buf, $read, 8 - $read) }
$got = [Text.Encoding]::ASCII.GetString($buf)
if ($got -ne 'IKEMENGO') { throw "guest received '$got'" }

$cb.Stream.Write($tok, 0, $tok.Length); $cb.Stream.Flush()
$ca.Stream.ReadTimeout = 5000
$read = 0
while ($read -lt 8) { $read += $ca.Stream.Read($buf, $read, 8 - $read) }
$got = [Text.Encoding]::ASCII.GetString($buf)
if ($got -ne 'IKEMENGO') { throw "host received '$got'" }

# Binary payload with embedded zeros, as netplay actually sends.
$payload = [byte[]](0x00, 0xC7, 0x7C, 0x00, 0xFF)
$ca.Stream.Write($payload, 0, $payload.Length); $ca.Stream.Flush()
$pb = New-Object byte[] 5
$read = 0
while ($read -lt 5) { $read += $cb.Stream.Read($pb, $read, 5 - $read) }
if (($pb -join ',') -ne ($payload -join ',')) { throw "payload corrupted: $($pb -join ',')" }

$ca.Client.Close(); $cb.Client.Close()
Write-Host "INTEROP OK: handshake and binary payload relayed both ways"
