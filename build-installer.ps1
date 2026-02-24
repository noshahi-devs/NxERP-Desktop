$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "HelloWpf\HelloWpf.csproj"
$installerDir = Join-Path $PSScriptRoot "Installer"
$publishDir = Join-Path $installerDir "publish"
$wixProjPath = Join-Path $installerDir "HelloWpfSetup.wixproj"

function Invoke-AndCheck {
    param([scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE"
    }
}

Write-Host "Publishing application..."
Invoke-AndCheck {
    dotnet publish $projectPath -c Release -r win-x64 --self-contained true /p:PublishSingleFile=false /p:PublishTrimmed=false -o $publishDir
}

Write-Host "Building MSI installer..."
Invoke-AndCheck {
    dotnet build $wixProjPath -c Release -p:Platform=x64
}

$defaultMsiPath = Join-Path $installerDir "bin\\x64\\Release\\HelloWpfSetup.msi"
$brandedMsiPath = Join-Path $installerDir "bin\\x64\\Release\\NxERPSetup.msi"
try {
    Copy-Item -Force $defaultMsiPath $brandedMsiPath
}
catch {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $brandedMsiPath = Join-Path $installerDir "bin\\x64\\Release\\NxERPSetup-$stamp.msi"
    Copy-Item -Force $defaultMsiPath $brandedMsiPath
}

Write-Host "Done. Installer created at $brandedMsiPath"

