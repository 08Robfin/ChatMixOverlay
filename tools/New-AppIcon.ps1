Add-Type -AssemblyName System.Drawing
$iconSizes = @(16,24,32,48,64,128,256)
$iconImages = @()
foreach($iconSize in $iconSizes) {
    $bitmap = [System.Drawing.Bitmap]::new($iconSize,$iconSize)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = 'AntiAlias'
    $g.ScaleTransform($iconSize/64.0,$iconSize/64.0)
    $shape=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $shape.AddArc(2,2,20,20,180,90); $shape.AddArc(42,2,20,20,270,90); $shape.AddArc(42,42,20,20,0,90); $shape.AddArc(2,42,20,20,90,90); $shape.CloseFigure()
    $fill=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(25,103,183)); $g.FillPath($fill,$shape)
    $pen=[System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(242,247,252),3.5); $pen.LineJoin='Round'; $pen.StartCap='Round'; $pen.EndCap='Round'
    $bubble=[System.Drawing.Drawing2D.GraphicsPath]::new(); $bubble.AddLines([System.Drawing.PointF[]]@([System.Drawing.PointF]::new(14,17),[System.Drawing.PointF]::new(50,17),[System.Drawing.PointF]::new(50,42),[System.Drawing.PointF]::new(32,42),[System.Drawing.PointF]::new(23,49),[System.Drawing.PointF]::new(23,42),[System.Drawing.PointF]::new(14,42))); $bubble.CloseFigure(); $g.DrawPath($pen,$bubble)
    $g.DrawLine($pen,22,29,42,29)
    $dot=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(120,235,212)); $g.FillEllipse($dot,28,24,10,10)
    $memory=[System.IO.MemoryStream]::new(); $bitmap.Save($memory,[System.Drawing.Imaging.ImageFormat]::Png); $iconImages+= ,$memory.ToArray()
    $g.Dispose(); $bitmap.Dispose(); $memory.Dispose(); $fill.Dispose(); $dot.Dispose(); $pen.Dispose(); $shape.Dispose(); $bubble.Dispose()
}
$stream=[System.IO.File]::Create((Join-Path $PSScriptRoot '../src/app.ico')); $writer=[System.IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$iconSizes.Count)
$offset=6+16*$iconSizes.Count
for($i=0;$i -lt $iconSizes.Count;$i++) { $sizeByte=if($iconSizes[$i] -eq 256){0}else{$iconSizes[$i]}; $writer.Write([byte]$sizeByte); $writer.Write([byte]$sizeByte); $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$iconImages[$i].Length); $writer.Write([uint32]$offset); $offset+=$iconImages[$i].Length }
foreach($iconBytes in $iconImages) { $writer.Write([byte[]]$iconBytes) }; $writer.Dispose()
