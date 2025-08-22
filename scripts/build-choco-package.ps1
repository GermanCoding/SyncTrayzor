param()

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$rootDir = (Get-Item $scriptDir).Parent.FullName
$releaseDir = Join-Path $rootDir "release"
$chocolateyDir = Join-Path $rootDir "chocolatey"
$toolsDir = Join-Path $chocolateyDir "tools"
$installerFile = Join-Path $releaseDir "SyncTrayzorSetup-x64.exe"
$targetInstallerFile = Join-Path $toolsDir "SyncTrayzorSetup-x64.exe"
$nuspecFile = Join-Path $chocolateyDir "synctrayzor.nuspec"

$version = $env:SYNCTRAYZOR_VERSION
$releaseNotes = $env:SYNCTRAYZOR_RELEASE_NOTES

if (-not $version) {
    throw "SYNCTRAYZOR_VERSION environment variable must be set"
}

if (-not $releaseNotes) {
    throw "SYNCTRAYZOR_RELEASE_NOTES environment variable must be set"
}

# Copy installer file to chocolatey tools directory
Write-Host "Copying installer to chocolatey tools directory..."
if (-not (Test-Path $installerFile)) {
    throw "Installer file not found at $installerFile"
}

if (Test-Path $targetInstallerFile) {
    Remove-Item $targetInstallerFile -Force
}

Copy-Item $installerFile $targetInstallerFile -Force

# Fill version and release notes
Write-Host "Updating nuspec file with version and release notes..."
$xmlDoc = New-Object System.Xml.XmlDocument
$reader = New-Object System.Xml.XmlTextReader $nuspecFile
$reader.Close()
$xmlDoc.Load($nuspecFile)
$xmlDoc.package.metadata.version = $version
$xmlDoc.package.metadata.releaseNotes = $releaseNotes
$xmlWriter = New-Object System.Xml.XmlTextWriter($nuspecFile, [System.Text.Encoding]::UTF8)
$xmlWriter.Formatting = [System.Xml.Formatting]::Indented
$xmlDoc.Save($xmlWriter)
$xmlWriter.Close()

# Pack the Chocolatey package
Write-Host "Building Chocolatey package..."
$originalLocation = Get-Location
try {
    Set-Location $chocolateyDir
    choco pack
    if ($LASTEXITCODE -ne 0) {
        throw "Chocolatey pack failed with exit code $LASTEXITCODE"
    }
    
    # Move the generated .nupkg file to the release directory
    $nupkgFile = Get-ChildItem -Path $chocolateyDir -Filter "*.nupkg" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($nupkgFile) {
        $targetPath = Join-Path $releaseDir $nupkgFile.Name
        Move-Item $nupkgFile.FullName $targetPath -Force
        Write-Host "Package created: $targetPath"
    } else {
        throw "No .nupkg file was generated"
    }
} finally {
    Set-Location $originalLocation
}

Write-Host "Chocolatey package build completed successfully"