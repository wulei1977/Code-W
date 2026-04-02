Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath {
    param(
        [System.Drawing.RectangleF]$Rect,
        [float]$Radius
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2

    $path.AddArc($Rect.X, $Rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rect.X, $Rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-CodeWBitmap {
    param(
        [int]$Size
    )

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $outer = [System.Drawing.RectangleF]::new($Size * 0.0625, $Size * 0.0625, $Size * 0.875, $Size * 0.875)
    $inner = [System.Drawing.RectangleF]::new($Size * 0.109375, $Size * 0.109375, $Size * 0.78125, $Size * 0.78125)
    $radiusOuter = $Size * 0.21875
    $radiusInner = $Size * 0.171875

    $pathOuter = New-RoundedRectPath -Rect $outer -Radius $radiusOuter
    $pathInner = New-RoundedRectPath -Rect $inner -Radius $radiusInner

    $startColor = [System.Drawing.Color]::FromArgb(255, 15, 108, 189)
    $endColor = [System.Drawing.Color]::FromArgb(255, 10, 155, 142)
    $overlayColor = [System.Drawing.Color]::FromArgb(18, 255, 255, 255)

    $gradient = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new($outer.Left, $outer.Top),
        [System.Drawing.PointF]::new($outer.Right, $outer.Bottom),
        $startColor,
        $endColor)
    $overlayBrush = [System.Drawing.SolidBrush]::new($overlayColor)

    $graphics.FillPath($gradient, $pathOuter)
    $graphics.FillPath($overlayBrush, $pathInner)

    $leftChevron = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(190, 255, 255, 255), [float]($Size * 0.055))
    $leftChevron.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $leftChevron.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $leftChevron.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawLines($leftChevron, @(
        [System.Drawing.PointF]::new($Size * 0.328, $Size * 0.352),
        [System.Drawing.PointF]::new($Size * 0.219, $Size * 0.5),
        [System.Drawing.PointF]::new($Size * 0.328, $Size * 0.648)))
    $graphics.DrawLines($leftChevron, @(
        [System.Drawing.PointF]::new($Size * 0.672, $Size * 0.352),
        [System.Drawing.PointF]::new($Size * 0.781, $Size * 0.5),
        [System.Drawing.PointF]::new($Size * 0.672, $Size * 0.648)))

    $shadowPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(56, 6, 49, 94), [float]($Size * 0.109))
    $shadowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $shadowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $shadowPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawLines($shadowPen, @(
        [System.Drawing.PointF]::new($Size * 0.363, $Size * 0.305),
        [System.Drawing.PointF]::new($Size * 0.445, $Size * 0.703),
        [System.Drawing.PointF]::new($Size * 0.5, $Size * 0.5),
        [System.Drawing.PointF]::new($Size * 0.555, $Size * 0.703),
        [System.Drawing.PointF]::new($Size * 0.641, $Size * 0.305)))

    $wBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.PointF]::new($Size * 0.5, $Size * 0.219),
        [System.Drawing.PointF]::new($Size * 0.5, $Size * 0.781),
        [System.Drawing.Color]::FromArgb(255, 247, 251, 255),
        [System.Drawing.Color]::FromArgb(255, 211, 255, 245))

    $wPen = [System.Drawing.Pen]::new($wBrush, [float]($Size * 0.07))
    $wPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $wPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $wPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawLines($wPen, @(
        [System.Drawing.PointF]::new($Size * 0.359, $Size * 0.289),
        [System.Drawing.PointF]::new($Size * 0.445, $Size * 0.695),
        [System.Drawing.PointF]::new($Size * 0.5, $Size * 0.5),
        [System.Drawing.PointF]::new($Size * 0.555, $Size * 0.695),
        [System.Drawing.PointF]::new($Size * 0.641, $Size * 0.289)))

    $accentBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(242, 183, 255, 240))
    $graphics.FillEllipse($accentBrush, $Size * 0.719, $Size * 0.176, $Size * 0.094, $Size * 0.094)

    $graphics.Dispose()
    $pathOuter.Dispose()
    $pathInner.Dispose()
    $gradient.Dispose()
    $overlayBrush.Dispose()
    $leftChevron.Dispose()
    $shadowPen.Dispose()
    $wBrush.Dispose()
    $wPen.Dispose()
    $accentBrush.Dispose()

    return $bitmap
}

$artDir = Join-Path $PSScriptRoot "..\\src\\CodeW\\art"
[System.IO.Directory]::CreateDirectory($artDir) | Out-Null

$icon32 = New-CodeWBitmap -Size 32
$icon32.Save((Join-Path $artDir "code-w-icon-32.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$icon32.Dispose()

$preview200 = New-CodeWBitmap -Size 200
$preview200.Save((Join-Path $artDir "code-w-preview-200.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$preview200.Dispose()
