param (
    [string]$Version
)

$ErrorActionPreference = "Stop"
$outdir = "syncthing"

if (Test-Path "$outdir/syncthing.exe") {
    Write-Host "Syncthing already downloaded in $outdir. Skipping download."
    exit 0
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
$extractedRoot = Join-Path $outdir "syncthing-windows-$arch-$Version"
# Left over from an interrupted run? ExtractToDirectory doesn't overwrite.
if (Test-Path $extractedRoot) { Remove-Item $extractedRoot -Recurse -Force }
# Expand-Archive fails on GitHub's Windows runners ("Cannot find an overload for ExtractToDirectory"), so use .NET directly
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $zipPath).Path, (Resolve-Path $outdir).Path)

# Optionally move binaries up from the nested folder
if (Test-Path $extractedRoot) {
    Move-Item -Path (Join-Path $extractedRoot '*') -Destination $outdir -Force
    Remove-Item $extractedRoot -Recurse -Force
}

Remove-Item $zipPath -Force
Remove-Item $sigPath -Force