$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# One tall, main-palette pose per character file, tiled into a single image so
# each F0C** file can be matched to a character by eye.
$src = 'D:\Working\Projects\SamuraiAssets'
$out = "$env:TEMP\lineup.png"
$cell = 170
$cols = 6

$picks = @()
foreach ($dir in Get-ChildItem $src -Directory | Sort-Object Name) {
    $best = Get-ChildItem $dir.FullName -Filter '*_p0256.png' |
        ForEach-Object {
            if ($_.Name -match '_(\d+)x(\d+)_') {
                [pscustomobject]@{
                    File = $_.FullName
                    W    = [int]$Matches[1]
                    H    = [int]$Matches[2]
                }
            }
        } |
        Where-Object { $_.H -ge 90 -and $_.H -le 140 -and $_.W -le 110 } |
        Sort-Object H -Descending | Select-Object -First 1

    if ($best) { $picks += [pscustomobject]@{ Name = $dir.Name; File = $best.File } }
}

$rows = [math]::Ceiling($picks.Count / $cols)
$bmp = New-Object System.Drawing.Bitmap ($cols * $cell), ($rows * $cell)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 24, 24, 32))
$g.InterpolationMode = 'NearestNeighbor'
$g.PixelOffsetMode = 'Half'
$font = New-Object System.Drawing.Font 'Consolas', 14
$brush = [System.Drawing.Brushes]::White

for ($i = 0; $i -lt $picks.Count; $i++) {
    $img = [System.Drawing.Image]::FromFile($picks[$i].File)
    $cx = ($i % $cols) * $cell
    $cy = [math]::Floor($i / $cols) * $cell
    $scale = [math]::Min(($cell - 30) / $img.Width, ($cell - 30) / $img.Height)
    $w = [int]($img.Width * $scale)
    $h = [int]($img.Height * $scale)
    $g.DrawImage($img, ($cx + ($cell - $w) / 2), ($cy + 24), $w, $h)
    $g.DrawString($picks[$i].Name, $font, $brush, ($cx + 6), ($cy + 4))
    $img.Dispose()
}

$g.Dispose()
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "wrote $out with $($picks.Count) characters"
