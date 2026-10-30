$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$assets=Join-Path (Split-Path $PSScriptRoot -Parent) 'Assets'
function Export-Icon([string]$name){
$source=[Drawing.Image]::FromFile((Join-Path $assets ($name+'.png')))
try {
 $frames=@()
 foreach($size in 16,24,32,48,64,128,256){
  $bitmap=[Drawing.Bitmap]::new($size,$size)
  $graphics=[Drawing.Graphics]::FromImage($bitmap)
  try {
   $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
   $graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
   $graphics.Clear([Drawing.Color]::Transparent)
   $graphics.DrawImage($source,0,0,$size,$size)
   $stream=[IO.MemoryStream]::new()
   $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
   $frames+=,@{Size=$size;Bytes=$stream.ToArray()}
   $stream.Dispose()

  }finally{$graphics.Dispose();$bitmap.Dispose()}
 }
 $file=[IO.File]::Create((Join-Path $assets ($name+'.ico')))
 $writer=[IO.BinaryWriter]::new($file)
 try{
  $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
  $offset=6+16*$frames.Count
  foreach($frame in $frames){$dimension=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length}
  foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
 }finally{$writer.Dispose();$file.Dispose()}
}finally{$source.Dispose()}

}
Export-Icon 'dual-steam'
Export-Icon 'second-steam'