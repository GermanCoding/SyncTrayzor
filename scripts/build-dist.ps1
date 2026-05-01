param(
    [string]$Variant
)

$ErrorActionPreference = "Stop"

$dotnetTargetSyncTrayzor = "net10.0-windows10.0.17763.0"
. "$PSScriptRoot\helpers\get-arch.ps1"
$arch = Get-Arch
$dotnetArch = switch ($arch) {
    "amd64" { "win-x64" }
    "arm64" { "win-arm64" }
    default { throw "Unknown architecture: $arch" }
}
$syncthingExe = ".\syncthing\syncthing.exe"
$publishDir = ".\src\SyncTrayzor\bin\Release\$dotnetTargetSyncTrayzor\$dotnetArch\publish"
$mergedDir = ".\dist"

Write-Host "Building SyncTrayzor for $Variant"

# Clean publish dir first
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

dotnet publish -c Release -p:DebugType=None -p:DebugSymbols=false -p:SelfContained=true -p:AppConfigVariant=$Variant src/SyncTrayzor/SyncTrayzor.csproj
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build SyncTrayzor. Exiting."
    exit $LASTEXITCODE
}

# Remove and recreate merged directory
if (Test-Path $mergedDir) {
    Remove-Item $mergedDir -Recurse -Force
}
New-Item -ItemType Directory -Path $mergedDir | Out-Null
Copy-Item "$publishDir\*" $mergedDir -Recurse -Force

$additionalFiles = @(
    $syncthingExe,
    # Also include VC++ runtime files for systems that do not have it
    "C:\Windows\System32\msvcp140.dll",
    "C:\Windows\System32\vcruntime140.dll",
    "C:\Windows\System32\vcruntime140_1.dll"
)
foreach ($file in $additionalFiles) {
    if (Test-Path $file) {
        Copy-Item $file $mergedDir -Force
    }
    else {
        Write-Error "File not found: $file"
        exit 1
    }
}
