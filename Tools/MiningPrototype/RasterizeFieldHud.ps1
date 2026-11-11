param([string]$Folder="")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
if(!$Folder){$Folder=Join-Path $PSScriptRoot '../../artifacts/hud-field-kit'}
function Color([string]$value){[Drawing.ColorTranslator]::FromHtml($value)}
foreach($file in Get-ChildItem -LiteralPath $folder -Filter '*.svg'){
 [xml]$svg=Get-Content -LiteralPath $file.FullName -Raw
 $w=[int]$svg.svg.width;$h=[int]$svg.svg.height
 $bmp=[Drawing.Bitmap]::new($w,$h);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::Transparent);$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
 if($svg.svg.viewBox -eq '0 0 24 24'){$g.ScaleTransform($w/24.0,$h/24.0)}
 foreach($node in $svg.SelectNodes('//*[local-name()="path" or local-name()="circle" or local-name()="polygon"]')){
  $path=[Drawing.Drawing2D.GraphicsPath]::new();$fill=$node.GetAttribute('fill');if(!$fill -and $node.ParentNode.LocalName -eq 'g'){$fill=$node.ParentNode.GetAttribute('fill')}
  if($node.LocalName -eq 'circle'){$r=[float]$node.r;$path.AddEllipse([float]$node.cx-$r,[float]$node.cy-$r,2*$r,2*$r)}
  elseif($node.LocalName -eq 'polygon'){$pts=[Collections.Generic.List[Drawing.PointF]]::new();foreach($pair in $node.points.Split(' ')){$xy=$pair.Split(',');$pts.Add([Drawing.PointF]::new([float]$xy[0],[float]$xy[1]))};$path.AddPolygon($pts.ToArray())}
  else {
   $tokens=[regex]::Matches($node.d,'[A-Za-z]|-?\d*\.?\d+');$idx=0;$x=0.0;$y=0.0;$sx=0.0;$sy=0.0;$command=''
   while($idx -lt $tokens.Count){
    if($tokens[$idx].Value -match '^[A-Za-z]$'){$command=$tokens[$idx].Value;$idx++;if($command -in @('z','Z')){$path.CloseFigure();$x=$sx;$y=$sy;continue}}
    $oldx=$x;$oldy=$y;$lower=$command.ToLowerInvariant();$relative=$command -cmatch '^[a-z]$'
    if($lower -in @('m','l')){$a=[float]$tokens[$idx++].Value;$b=[float]$tokens[$idx++].Value;if($relative){$x+=$a;$y+=$b}else{$x=$a;$y=$b}}
    elseif($lower -eq 'h'){$a=[float]$tokens[$idx++].Value;if($relative){$x+=$a}else{$x=$a}}
    elseif($lower -eq 'v'){$a=[float]$tokens[$idx++].Value;if($relative){$y+=$a}else{$y=$a}}
    else{throw "Unsupported path command $command"}
    if($lower -eq 'm'){$path.StartFigure();$sx=$x;$sy=$y;$command=if($relative){'l'}else{'L'}}else{$path.AddLine([float]$oldx,[float]$oldy,[float]$x,[float]$y)}
   }
  }
  if($fill -and $fill -ne 'none'){$c=Color $fill;if($node.HasAttribute('fill-opacity')){$c=[Drawing.Color]::FromArgb([int](255*[float]$node.GetAttribute('fill-opacity')),$c)};$brush=[Drawing.SolidBrush]::new($c);$g.FillPath($brush,$path);$brush.Dispose()}
  if(!$node.HasAttribute('stroke') -and $node.ParentNode.LocalName -eq 'g' -and $node.ParentNode.HasAttribute('stroke')){$node.SetAttribute('stroke',$node.ParentNode.GetAttribute('stroke'));$node.SetAttribute('stroke-width',$node.ParentNode.GetAttribute('stroke-width'))}
  if($node.HasAttribute('stroke')){$pen=[Drawing.Pen]::new((Color $node.stroke),[float]$node.GetAttribute('stroke-width'));$pen.LineJoin=[Drawing.Drawing2D.LineJoin]::Round;$g.DrawPath($pen,$path);$pen.Dispose()}
  $path.Dispose()
 }
 $g.Dispose();$bmp.Save((Join-Path $file.DirectoryName ($file.BaseName+'.png')),[Drawing.Imaging.ImageFormat]::Png);$bmp.Dispose()
}
Write-Output 'Rasterized original vector HUD artwork into transparent PNG assets.'

