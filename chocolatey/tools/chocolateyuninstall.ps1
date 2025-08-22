$ErrorActionPreference = 'Stop'

$pp = Get-PackageParameters

if ($pp.DeleteAll) {
    Write-Output "Performing FULL uninstall: removing all syncthing configuration."
    $extraArgs = "/DELETEALL"
} else {
    Write-Output "Performing SOFT uninstall: leaving configuration intact."
    $extraArgs = ""
}

$packageArgs = @{
  packageName   = $env:ChocolateyPackageName
  fileType      = 'exe'
  silentArgs   = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- ' + $extraArgs
  validExitCodes= @(0)
}

[array]$key = Get-UninstallRegistryKey -SoftwareName 'SyncTrayzor*'

if ($key.Count -eq 1) {
  $key | % {
    $packageArgs['file'] = "$($_.UninstallString)"

    if ($packageArgs['fileType'] -eq 'MSI') {
      $packageArgs['silentArgs'] = "$($_.PSChildName) $($packageArgs['silentArgs'])"

      $packageArgs['file'] = ''
    } else {
    }

    Uninstall-ChocolateyPackage @packageArgs
  }
} elseif ($key.Count -eq 0) {
  Write-Warning "SyncTrayzor has already been uninstalled by other means."
} elseif ($key.Count -gt 1) {
  Write-Warning "$($key.Count) matches found!"
  Write-Warning "To prevent accidental data loss, no programs will be uninstalled."
  Write-Warning "Please alert package maintainer the following keys were matched:"
  $key | % {Write-Warning "- $($_.DisplayName)"}
}
