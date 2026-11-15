# Regenerate the Windows icon from the shared vector artwork.
Add-Type -AssemblyName PresentationCore,WindowsBase
$assetPath = Join-Path $PSScriptRoot '..\CallMonitor\Assets'
[xml]$svg = Get-Content (Join-Path $assetPath 'app.svg') -Raw
$sizes = @(16,24,32,48,64,128,256)
$frames = @()
foreach ($size in $sizes) {
    $visual = New-Object System.Windows.Media.DrawingVisual
    $drawing = $visual.RenderOpen()
    $drawing.PushTransform((New-Object System.Windows.Media.ScaleTransform ($size/48.0),($size/48.0)))
    foreach ($node in $svg.DocumentElement.ChildNodes) {
        $fill = if ($node.fill -and $node.fill -ne 'none') { [System.Windows.Media.BrushConverter]::new().ConvertFromString([string]$node.fill) } else { $null }
        $pen = $null
        if ($node.stroke) {
            $pen = [System.Windows.Media.Pen]::new([System.Windows.Media.BrushConverter]::new().ConvertFromString([string]$node.stroke), [double]$node.'stroke-width')
            $pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round'; $pen.LineJoin = 'Round'
        }
        switch ($node.LocalName) {
            'rect' { $drawing.DrawRoundedRectangle($fill,$pen,[System.Windows.Rect]::new(0,0,[double]$node.width,[double]$node.height),[double]$node.rx,[double]$node.rx) }
            'path' { $drawing.DrawGeometry($fill,$pen,[System.Windows.Media.Geometry]::Parse([string]$node.d)) }
            'circle' { $drawing.DrawEllipse($fill,$pen,[System.Windows.Point]::new([double]$node.cx,[double]$node.cy),[double]$node.r,[double]$node.r) }
        }
    }
    $drawing.Pop(); $drawing.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $memory = [System.IO.MemoryStream]::new(); $encoder.Save($memory)
    $frames += ,$memory.ToArray(); $memory.Dispose()
}
[System.IO.File]::WriteAllBytes((Join-Path $assetPath 'app.png'), $frames[-1])
$stream = [System.IO.File]::Create((Join-Path $assetPath 'app.ico'))
$writer = [System.IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16*$sizes.Count
    for ($index=0; $index -lt $sizes.Count; $index++) {
        $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frames[$index].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$index].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose(); $stream.Dispose() }
Write-Output 'Generated Assets/app.ico (16–256 px).'

