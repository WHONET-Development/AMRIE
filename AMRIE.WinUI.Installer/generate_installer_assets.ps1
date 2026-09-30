Add-Type -AssemblyName System.Drawing

# 1. Update AMRIE.ico with AppIcon.ico
Copy-Item -Path "AMRIE.WinUI\Assets\AppIcon.ico" -Destination "AMRIE.WinUI.Installer\AMRIE.ico" -Force
Write-Host "Updated AMRIE.ico with new AppIcon.ico"

# 2. Generate Dialog.bmp (493 x 312)
$logoPath = "AMRIE.WinUI\Assets\Square150x150Logo.scale-200.png"
$logo = [System.Drawing.Image]::FromFile($logoPath)

$dialogBmp = New-Object System.Drawing.Bitmap 493, 312, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = [System.Drawing.Graphics]::FromImage($dialogBmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

# Right area: Clean white for wizard text and controls
$whiteBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$g.FillRectangle($whiteBrush, 164, 0, 329, 312)

# Left sidebar: Gradient from #1E293B (top) to #0F172A (bottom)
$cTop = [System.Drawing.Color]::FromArgb(30, 41, 59)
$cBottom = [System.Drawing.Color]::FromArgb(15, 23, 42)
$sidebarRect = New-Object System.Drawing.Rectangle 0, 0, 164, 312
$gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush $sidebarRect, $cTop, $cBottom, 90.0
$g.FillRectangle($gradient, $sidebarRect)

# Sidebar divider line
$linePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(51, 65, 85)), 1
$g.DrawLine($linePen, 163, 0, 163, 312)

# Draw Logo on sidebar (72x72, centered horizontally: (164 - 72)/2 = 46)
$g.DrawImage($logo, 46, 40, 72, 72)

# Text on sidebar
$fontTitle = [System.Drawing.Font]::new("Segoe UI", [float]15.0, [System.Drawing.FontStyle]::Bold)
$fontSub = [System.Drawing.Font]::new("Segoe UI", [float]8.5, [System.Drawing.FontStyle]::Regular)
$fontMuted = [System.Drawing.Font]::new("Segoe UI", [float]7.5, [System.Drawing.FontStyle]::Regular)

$whiteTextBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
$blueTextBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(56, 189, 248))
$grayTextBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(148, 163, 184))

$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center

$g.DrawString("AMRIE", $fontTitle, $whiteTextBrush, (New-Object System.Drawing.RectangleF 0, 122, 164, 28), $sf)
$g.DrawString("Antimicrobial Resistance", $fontSub, $whiteTextBrush, (New-Object System.Drawing.RectangleF 0, 154, 164, 18), $sf)
$g.DrawString("Interpretation Engine", $fontSub, $blueTextBrush, (New-Object System.Drawing.RectangleF 0, 172, 164, 18), $sf)

$g.DrawString("Brigham and Women's Hospital", $fontMuted, $grayTextBrush, (New-Object System.Drawing.RectangleF 0, 260, 164, 20), $sf)

$g.Dispose()
$dialogBmp.Save("AMRIE.WinUI.Installer\Dialog.bmp", [System.Drawing.Imaging.ImageFormat]::Bmp)
$dialogBmp.Dispose()
Write-Host "Generated Dialog.bmp (493x312)"

# 3. Generate Banner.bmp (493 x 58)
$bannerBmp = New-Object System.Drawing.Bitmap 493, 58, ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g2 = [System.Drawing.Graphics]::FromImage($bannerBmp)
$g2.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

$g2.FillRectangle($whiteBrush, 0, 0, 493, 58)
# Subtle bottom border line
$borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(203, 213, 225)), 1
$g2.DrawLine($borderPen, 0, 57, 493, 57)

# Draw Logo on right side (44x44 at X=438, Y=7)
$g2.DrawImage($logo, 438, 7, 44, 44)

$g2.Dispose()
$bannerBmp.Save("AMRIE.WinUI.Installer\Banner.bmp", [System.Drawing.Imaging.ImageFormat]::Bmp)
$bannerBmp.Dispose()
Write-Host "Generated Banner.bmp (493x58)"

$logo.Dispose()
