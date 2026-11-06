param([string]$Rife = '.tools/rife/rife-ncnn-vulkan-20221029-windows/rife-ncnn-vulkan.exe', [int]$Gpu = 1)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $projectRoot
$localDotnet = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools/packages'
$env:DOTNET_NOLOGO = '1'
$model = Join-Path (Split-Path $Rife -Parent) 'rife-anime'
if (!(Test-Path $Rife)) { throw '请先下载 RIFE 20221029 Windows 包并解压至 .tools/rife；链接见 design/walk-v4/SOURCES.md。' }

& $dotnet run --project tools/AssetPack -- $projectRoot --walk-v4-prepare
if ($LASTEXITCODE -ne 0) { throw '关键姿势导出失败。' }
New-Item -ItemType Directory -Path 'artifacts/walk-v4/input-rgb','artifacts/walk-v4/intermediate','artifacts/walk-v4/interpolated' -Force | Out-Null
# This RIFE binary expects RGB input. Passing WPF's RGBA PNGs corrupts channel packing.
& ffmpeg -hide_banner -loglevel error -y -framerate 8 -i 'artifacts/walk-v4/input/%08d.png' -pix_fmt rgb24 'artifacts/walk-v4/input-rgb/%08d.png'
if ($LASTEXITCODE -ne 0) { throw 'RGB 导出失败。' }
# Two exact 2x passes preserve every original key and use the model that retained crossed hind legs.
# RIFE maps time as inputCount/outputCount. 17 -> 34 -> 68; use the first 64 and omit the 4 held seam frames.
& $Rife -i 'artifacts/walk-v4/input-rgb' -o 'artifacts/walk-v4/intermediate' -m $model -g $Gpu -x -j 1:2:2 -f '%08d.png' 2> 'artifacts/walk-v4/rife-pass1.log'
if ($LASTEXITCODE -ne 0) { throw '第一轮整帧补间失败。' }
& $Rife -i 'artifacts/walk-v4/intermediate' -o 'artifacts/walk-v4/interpolated' -m $model -g $Gpu -x -j 1:2:2 -f '%08d.png' 2> 'artifacts/walk-v4/rife-pass2.log'
if ($LASTEXITCODE -ne 0) { throw '整帧补间失败。' }
& $dotnet run --project tools/AssetPack -- $projectRoot --walk-v4-preview
if ($LASTEXITCODE -ne 0) { throw '预览导出失败。' }
& ffmpeg -hide_banner -loglevel error -y -framerate 60 -i 'artifacts/walk-v4/preview/%04d.png' -frames:v 64 -vf 'fps=50,split[s0][s1];[s0]palettegen=max_colors=192[p];[s1][p]paletteuse=dither=sierra2_4a' -loop 0 'design/walk-v4/walk-refined.gif'
if ($LASTEXITCODE -ne 0) { throw 'GIF 导出失败。' }
& ffmpeg -hide_banner -loglevel error -y -framerate 60 -i 'artifacts/walk-v4/travel/%04d.png' -frames:v 384 -c:v libx264 -crf 18 -pix_fmt yuv420p -movflags +faststart 'design/walk-v4/walk-refined.mp4'
if ($LASTEXITCODE -ne 0) { throw '视频导出失败。' }
Write-Output '行走预览已更新：design/walk-v4/index.html'
