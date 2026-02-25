$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "HelloWpf\HelloWpf.csproj"
$installerDir = Join-Path $PSScriptRoot "Installer"
$publishDir = Join-Path $installerDir "publish"
$wixProjPath = Join-Path $installerDir "HelloWpfSetup.wixproj"
$installerBin = Join-Path $installerDir "bin"
$installerObj = Join-Path $installerDir "obj"
$releaseDir = Join-Path $installerDir "bin\\x64\\Release"

function Invoke-AndCheck {
    param([scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE"
    }
}

Write-Host "Publishing application..."
Write-Host "Cleaning old installer outputs..."
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (Test-Path $installerBin) { Remove-Item -Recurse -Force $installerBin }
if (Test-Path $installerObj) { Remove-Item -Recurse -Force $installerObj }

Invoke-AndCheck {
    dotnet publish $projectPath -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false /p:PublishTrimmed=false -o $publishDir
}

Write-Host "Building MSI installer..."
Invoke-AndCheck {
    dotnet build $wixProjPath -c Release -p:Platform=x64 -t:Rebuild
}

$defaultMsiPath = Join-Path $installerDir "bin\\x64\\Release\\HelloWpfSetup.msi"
$brandedMsiPath = Join-Path $installerDir "bin\\x64\\Release\\NxERPSetup.msi"

if (-not (Test-Path $releaseDir)) {
    throw "Installer release directory not found: $releaseDir"
}

Get-ChildItem -Path $releaseDir -Filter "*.msi" -ErrorAction SilentlyContinue |
    ForEach-Object {
        if ($_.FullName -ne $defaultMsiPath) {
            Remove-Item -Force $_.FullName
        }
    }

Copy-Item -Force $defaultMsiPath $brandedMsiPath

Write-Host "Done. Installer created at $brandedMsiPath"

