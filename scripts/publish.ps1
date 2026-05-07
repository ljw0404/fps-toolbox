# FPSToolbox 发布脚本
# 构建四个 exe（FPSToolbox、CrosshairTool、GammaTool、NightVisionTool）为 framework-dependent 发布
# 并按安装器期望的目录结构组装到 dist\payload\
#
#   dist\payload\
#     FPSToolbox.exe   (+ 依赖)
#     tools\
#       CrosshairTool\CrosshairTool.exe
#       GammaTool\GammaTool.exe
#       NightVisionTool\NightVisionTool.exe
#
# 用法：powershell -ExecutionPolicy Bypass -File scripts\publish.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

Write-Host "=== FPSToolbox Publish ===" -ForegroundColor Cyan

$payload = Join-Path $root "dist\payload"
if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }
New-Item -ItemType Directory -Path $payload | Out-Null

function Publish-Project {
    param([string]$csproj, [string]$outDir)
    Write-Host "  publishing: $csproj" -ForegroundColor Gray
    & dotnet publish $csproj `
        -c Release --no-self-contained `
        "-p:DebugType=None" "-p:DebugSymbols=false" `
        -o $outDir 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "publish failed: $csproj"
    }
}

# 为每个子工具生成 manifest.json，安装器打包时会一并复制进去
# 这样 AutoDiscover 就能读到正确版本，不会被误判为"有新版本"
function Write-ToolManifest {
    param([string]$ToolName, [string]$OutDir, [string]$CsprojPath)
    $xml     = [xml](Get-Content $CsprojPath -Raw)
    $version = $xml.Project.PropertyGroup.Version
    $exePath = Join-Path $OutDir "$ToolName.exe"
    $sha     = if (Test-Path $exePath) { (Get-FileHash $exePath -Algorithm SHA256).Hash } else { "" }
    $manifest = [ordered]@{
        name    = $ToolName
        version = $version
        exeName = "$ToolName.exe"
        sha256  = $sha
    } | ConvertTo-Json
    Set-Content (Join-Path $OutDir "manifest.json") -Value $manifest -Encoding UTF8
    Write-Host "  manifest.json  v$version" -ForegroundColor Gray
}

# 1. 主程序
Write-Host "[1/5] FPSToolbox" -ForegroundColor Yellow
Publish-Project (Join-Path $root "src\FPSToolbox\FPSToolbox.csproj") $payload

# 2. CrosshairTool
Write-Host "[2/5] CrosshairTool" -ForegroundColor Yellow
$crosshairOut = Join-Path $payload "tools\CrosshairTool"
Publish-Project (Join-Path $root "src\CrosshairTool\CrosshairTool.csproj") $crosshairOut
Write-ToolManifest "CrosshairTool" $crosshairOut (Join-Path $root "src\CrosshairTool\CrosshairTool.csproj")

# 3. GammaTool
Write-Host "[3/5] GammaTool" -ForegroundColor Yellow
$gammaOut = Join-Path $payload "tools\GammaTool"
Publish-Project (Join-Path $root "src\GammaTool\GammaTool.csproj") $gammaOut
Write-ToolManifest "GammaTool" $gammaOut (Join-Path $root "src\GammaTool\GammaTool.csproj")

# 4. NightVisionTool
Write-Host "[4/5] NightVisionTool" -ForegroundColor Yellow
$nightOut = Join-Path $payload "tools\NightVisionTool"
Publish-Project (Join-Path $root "src\NightVisionTool\NightVisionTool.csproj") $nightOut
Write-ToolManifest "NightVisionTool" $nightOut (Join-Path $root "src\NightVisionTool\NightVisionTool.csproj")

# 5. MouseTool
Write-Host "[5/5] MouseTool" -ForegroundColor Yellow
$mouseOut = Join-Path $payload "tools\MouseTool"
Publish-Project (Join-Path $root "src\MouseTool\MouseTool.csproj") $mouseOut
Write-ToolManifest "MouseTool" $mouseOut (Join-Path $root "src\MouseTool\MouseTool.csproj")

Write-Host ""
Write-Host "Output layout:" -ForegroundColor Green
Get-ChildItem $payload | Select-Object Name, @{n="Size";e={
    if ($_.PSIsContainer) { (Get-ChildItem $_.FullName -Recurse | Measure-Object Length -Sum).Sum } else { $_.Length }
}} | Format-Table -AutoSize

$totalMb = [Math]::Round((Get-ChildItem $payload -Recurse | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "Total: ${totalMb} MB" -ForegroundColor White
Write-Host ""
Write-Host "Next: open installer\FPSToolbox.iss with Inno Setup 6 and compile." -ForegroundColor Cyan
