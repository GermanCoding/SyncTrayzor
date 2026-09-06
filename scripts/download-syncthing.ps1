param (
    [string]$Version
)

$ErrorActionPreference = "Stop"
$outdir = "syncthing"

if (Test-Path "$outdir/syncthing.exe") {
    $versionOutput = & "$outdir/syncthing.exe" version 2>$null | Select-Object -First 1
    $currentVersion = ($versionOutput -split '\s+')[1]
    if ($currentVersion -eq $Version) {
        Write-Host "Syncthing $Version already downloaded in $outdir. Skipping download."
        exit 0
    }

    Write-Host "Found Syncthing $currentVersion in $outdir; replacing it with $Version."
}

. "$PSScriptRoot\helpers\get-arch.ps1"
$arch = Get-Arch
$filename = "syncthing-windows-$arch-$Version.zip"
$url = "https://github.com/syncthing/syncthing/releases/download/$Version/$filename"
$signatureUrl = "https://github.com/syncthing/syncthing/releases/download/$Version/sha256sum.txt.asc"
$zipPath = Join-Path $env:TEMP $filename
$sigPath = Join-Path $env:TEMP "sha256sum.txt.asc"

Write-Host "Downloading $url..."
Invoke-WebRequest -Uri $url -OutFile $zipPath

Write-Host "Validating signature..."
Invoke-WebRequest -Uri $signatureUrl -OutFile $sigPath
dotnet run -c Release --project src/ChecksumUtil/ChecksumUtil.csproj -- verify "$sigPath" "SHA-256" "security/syncthing_releases_cert.asc" "$zipPath"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Signature verification failed. Exiting."
    Remove-Item $zipPath -Force
    Remove-Item $sigPath -Force
    exit $LASTEXITCODE
}

if (!(Test-Path $outdir)) { New-Item -ItemType Directory -Path $outdir | Out-Null }

Write-Host "Extracting to $outdir..."
Expand-Archive -Path $zipPath -DestinationPath $outdir -Force

# Optionally move binaries up from the nested folder
$extractedRoot = Join-Path $outdir "syncthing-windows-$arch-$Version"
if (Test-Path $extractedRoot) {
    Copy-Item -Path (Join-Path $extractedRoot '*') -Destination $outdir -Recurse -Force
    Remove-Item $extractedRoot -Recurse -Force
}

Remove-Item $zipPath -Force
Remove-Item $sigPath -Force
