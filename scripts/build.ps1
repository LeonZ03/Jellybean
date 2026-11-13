param([switch]$Package, [switch]$Check, [switch]$Publish)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $projectRoot
$localDotnet = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/cli-home'
$env:NUGET_PACKAGES = Join-Path $projectRoot '.tools/packages'
$env:DOTNET_NOLOGO = '1'

& $dotnet run --project tools/AssetPack/AssetPack.csproj -c Release -- $projectRoot
if ($LASTEXITCODE -ne 0) { throw '动画打包失败。' }
& $dotnet build src/Jellybean/Jellybean.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw '编译失败。请先退出正在运行的开发版。' }

if ($Check) {
    & $dotnet run --project tests/Jellybean.Smoke/Jellybean.Smoke.csproj -c Release -- (Join-Path $projectRoot 'artifacts')
    if ($LASTEXITCODE -ne 0) { throw '关键功能检查失败。' }
}
if ($Publish -or $Package) {
    $version = ([xml](Get-Content src/Jellybean/Jellybean.csproj)).Project.PropertyGroup.Version
    $folder = Join-Path $projectRoot "artifacts/Jellybean-$version"
    & $dotnet publish src/Jellybean/Jellybean.csproj -c Release -r win-x64 --self-contained true -o $folder --nologo
    if ($LASTEXITCODE -ne 0) { throw '发布失败。' }
    Copy-Item -LiteralPath packaging/使用说明.md -Destination (Join-Path $folder '使用说明.md')
    if ($Package) {
        $archive = Join-Path $projectRoot "artifacts/Jellybean-$version-win-x64.zip"
        Compress-Archive -Path "$folder/*" -DestinationPath $archive -Force
        Get-Item $archive | Select-Object FullName,Length
    } else {
        Get-Item -LiteralPath (Join-Path $folder 'Jellybean.exe') | Select-Object FullName
    }
}
