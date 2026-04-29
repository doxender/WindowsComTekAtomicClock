# build-icon.ps1
#
# Generates src/ComTekAtomicClock.UI/Assets/AppIcon.ico from a WPF
# DrawingGroup definition. Run once after design changes; commit the
# resulting .ico alongside this script.
#
# Design: Atomic Lab face overlay on a stylized atom.
#   - Outer:   silver brushed bezel (the clock-face ring).
#   - Inner:   navy radial gradient face.
#   - Atom:    three amber elliptical electron orbits crossing through
#              the center at 0deg / 60deg / 120deg.
#   - Nucleus: bright amber dot with a small inner highlight.
#
# The .ico contains 6 entries (256, 128, 64, 48, 32, 16) using PNG
# sub-frames per the modern ICO format (Vista+). Windows picks the
# best size for each surface — taskbar, Alt+Tab, file explorer,
# minimized window-square, future tray icon.

Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------
# Build the WPF DrawingGroup at canonical 256x256
# ---------------------------------------------------------------

$dg = New-Object System.Windows.Media.DrawingGroup
$dc = $dg.Open()

$cx = 128.0
$cy = 128.0

# Bezel ring: silver brushed gradient
$bezelBrush = New-Object System.Windows.Media.LinearGradientBrush
$bezelBrush.StartPoint = [System.Windows.Point]::new(0.5, 0.0)
$bezelBrush.EndPoint   = [System.Windows.Point]::new(0.5, 1.0)
$null = $bezelBrush.GradientStops.Add((New-Object System.Windows.Media.GradientStop ([System.Windows.Media.ColorConverter]::ConvertFromString('#E0E3E8')), 0.0))
$null = $bezelBrush.GradientStops.Add((New-Object System.Windows.Media.GradientStop ([System.Windows.Media.ColorConverter]::ConvertFromString('#7C8088')), 0.5))
$null = $bezelBrush.GradientStops.Add((New-Object System.Windows.Media.GradientStop ([System.Windows.Media.ColorConverter]::ConvertFromString('#2C3038')), 1.0))
$bezelBrush.Freeze()
$dc.DrawEllipse($bezelBrush, $null, [System.Windows.Point]::new($cx, $cy), 124.0, 124.0)

# Face: navy radial gradient
$faceBrush = New-Object System.Windows.Media.RadialGradientBrush
$faceBrush.Center         = [System.Windows.Point]::new(0.5, 0.4)
$faceBrush.GradientOrigin = [System.Windows.Point]::new(0.5, 0.4)
$faceBrush.RadiusX = 0.7
$faceBrush.RadiusY = 0.7
$null = $faceBrush.GradientStops.Add((New-Object System.Windows.Media.GradientStop ([System.Windows.Media.ColorConverter]::ConvertFromString('#1A2A4A')), 0.0))
$null = $faceBrush.GradientStops.Add((New-Object System.Windows.Media.GradientStop ([System.Windows.Media.ColorConverter]::ConvertFromString('#060D1A')), 1.0))
$faceBrush.Freeze()
$dc.DrawEllipse($faceBrush, $null, [System.Windows.Point]::new($cx, $cy), 110.0, 110.0)

# Three amber elliptical electron orbits, 60deg apart
$amberColor = [System.Windows.Media.ColorConverter]::ConvertFromString('#FFB000')
$amberBrush = New-Object System.Windows.Media.SolidColorBrush $amberColor
$amberBrush.Freeze()
$orbitPen = New-Object System.Windows.Media.Pen $amberBrush, 5.0
$orbitPen.Freeze()

foreach ($angle in 0, 60, 120) {
    $rt = New-Object System.Windows.Media.RotateTransform $angle, $cx, $cy
    $rt.Freeze()
    $dc.PushTransform($rt)
    $dc.DrawEllipse($null, $orbitPen, [System.Windows.Point]::new($cx, $cy), 96.0, 38.0)
    $dc.Pop()
}

# Nucleus: amber dot with bright inner highlight
$dc.DrawEllipse($amberBrush, $null, [System.Windows.Point]::new($cx, $cy), 18.0, 18.0)
$highlightBrush = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.ColorConverter]::ConvertFromString('#FFF5D8'))
$highlightBrush.Freeze()
$dc.DrawEllipse($highlightBrush, $null, [System.Windows.Point]::new($cx - 4.0, $cy - 4.0), 6.0, 6.0)

$dc.Close()
$dg.Freeze()

# ---------------------------------------------------------------
# Render at standard ICO sizes and capture PNG bytes
# ---------------------------------------------------------------

$sizes = 256, 128, 64, 48, 32, 16
$pngEntries = @()

foreach ($size in $sizes) {
    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap `
        $size, $size, 96.0, 96.0, ([System.Windows.Media.PixelFormats]::Pbgra32)

    $visual = New-Object System.Windows.Media.DrawingVisual
    $vdc = $visual.RenderOpen()
    $scale = $size / 256.0
    $st = New-Object System.Windows.Media.ScaleTransform $scale, $scale
    $vdc.PushTransform($st)
    $vdc.DrawDrawing($dg)
    $vdc.Pop()
    $vdc.Close()

    $rtb.Render($visual)

    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $null = $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $ms = New-Object System.IO.MemoryStream
    $encoder.Save($ms)
    $bytes = $ms.ToArray()
    $ms.Close()

    $pngEntries += [PSCustomObject]@{ Size = $size; Bytes = $bytes }
    Write-Host ("Rendered {0,3}x{0,-3}  ({1,6} bytes)" -f $size, $bytes.Length)
}

# ---------------------------------------------------------------
# Combine into ICO (PNG entries; modern Vista+ ICO format)
# ---------------------------------------------------------------

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms

# ICONDIR header
$bw.Write([uint16]0)                       # Reserved
$bw.Write([uint16]1)                       # Type: 1 = ICON
$bw.Write([uint16]$pngEntries.Count)       # Count

$entrySize = 16
$offset = 6 + ($entrySize * $pngEntries.Count)

foreach ($entry in $pngEntries) {
    $dim = if ($entry.Size -ge 256) { 0 } else { [byte]$entry.Size }
    $bw.Write([byte]$dim)                  # Width
    $bw.Write([byte]$dim)                  # Height
    $bw.Write([byte]0)                     # ColorCount
    $bw.Write([byte]0)                     # Reserved
    $bw.Write([uint16]1)                   # Planes
    $bw.Write([uint16]32)                  # BitCount
    $bw.Write([uint32]$entry.Bytes.Length) # BytesInRes
    $bw.Write([uint32]$offset)             # ImageOffset
    $offset += $entry.Bytes.Length
}

foreach ($entry in $pngEntries) {
    $bw.Write($entry.Bytes)
}

$bw.Flush()

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot 'src\ComTekAtomicClock.UI\Assets'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$outPath = Join-Path $outDir 'AppIcon.ico'
[System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
$ms.Close()

$sizeKb = [math]::Round((Get-Item $outPath).Length / 1024.0, 1)
Write-Host ""
Write-Host "Wrote $outPath ($sizeKb KB)"
