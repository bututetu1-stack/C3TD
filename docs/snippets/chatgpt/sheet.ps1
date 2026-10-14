param([string]$Dir, [string]$Out, [string]$Names, [int]$Cell = 300, [int]$Cols = 4)
Add-Type -AssemblyName System.Drawing
$list = $Names -split ','
$rows = [Math]::Ceiling($list.Count / $Cols)
$bmp = New-Object System.Drawing.Bitmap ($Cols * $Cell), ($rows * ($Cell + 24))
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 40, 46, 60))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font = New-Object System.Drawing.Font 'Arial', 12
for ($i = 0; $i -lt $list.Count; $i++) {
  $p = Join-Path $Dir ($list[$i] + '.png')
  if (-not (Test-Path $p)) { continue }
  $img = [System.Drawing.Image]::FromFile($p)
  $x = ($i % $Cols) * $Cell; $y = [Math]::Floor($i / $Cols) * ($Cell + 24)
  $g.DrawImage($img, $x + 4, $y + 4, $Cell - 8, $Cell - 8)
  $g.DrawString($list[$i], $font, [System.Drawing.Brushes]::White, $x + 6, $y + $Cell)
  $img.Dispose()
}
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
"ok $Out"
