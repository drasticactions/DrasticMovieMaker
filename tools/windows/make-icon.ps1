# Makes tools\windows\AvaMovieMaker.ico, the Windows executable's icon, from the 512 px PNG the other packages use
# (tools\icons\icon-512.png): 16, 20, 24, 32, 40, 48, 64 and 256 px, each a PNG entry.
# The .ico is committed (the app's csproj embeds it at build time); run this again after changing the icon.
# Usage: pwsh tools/windows/make-icon.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$source = [System.Drawing.Image]::FromFile((Join-Path $root 'tools\icons\icon-512.png'))
$sizes = 16, 20, 24, 32, 40, 48, 64, 256
try {
    $images = foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bitmap)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.DrawImage($source, 0, 0, $size, $size)
        $g.Dispose()
        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
        , $stream.ToArray()
    }
}
finally {
    $source.Dispose()
}

# ICONDIR, one ICONDIRENTRY per size (0 means 256), then the PNG data.
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($png in $images) { $w.Write($png) }
$w.Flush()
$ico = Join-Path $PSScriptRoot 'AvaMovieMaker.ico'
[System.IO.File]::WriteAllBytes($ico, $out.ToArray())
Write-Host "$ico ($($out.Length) bytes)"
