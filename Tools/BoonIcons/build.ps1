# Rebuilds Assets/Resources/BoonIcons/*.png from the art listed in BoonIconSpec.cs (24 x 24, native pixel size).
# Run:  powershell -ExecutionPolicy Bypass -File Tools\BoonIcons\build.ps1     (contact sheet: Tools\BoonIcons\icons_sheet.png)
$here = Split-Path $MyInvocation.MyCommand.Path
Add-Type -AssemblyName System.Drawing
Add-Type -Path @((Join-Path $here "IconKit.cs"), (Join-Path $here "BoonIconSpec.cs")) -ReferencedAssemblies System.Drawing, System.IO.Compression
[IconKit]::Root = (Resolve-Path (Join-Path $here "..\..\Assets")).Path
[BoonIconSpec]::Build((Join-Path $here "..\..\Assets\Resources\BoonIcons"), (Join-Path $here "icons_sheet.png"))
