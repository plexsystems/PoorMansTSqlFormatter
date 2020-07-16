## Install MSI
$ErrorActionPreference = 'Stop'; # stop on all errors
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$fileLocation = Join-Path $toolsDir 'PoorMansTSqlFormatterSSMSPackage.Setup.msi'
$checksumPathMsi = Join-Path $toolsDir 'PoorMansTSqlFormatterSSMSPackage.Setup.msi.hash'
$msichecksum = [IO.File]::ReadAllText($checksumPathMsi)
# $checksumPathVsix = Join-Path $toolsDir 'PoorMansTSqlFormatterVSPackage2019.vsix.hash'
# $vsixChecksum = [IO.File]::ReadAllText($checksumPathVsix)

$packageArgs = @{
  packageName   = $env:ChocolateyPackageName
  unzipLocation = $toolsDir
  fileType      = 'MSI' 
  file          = $fileLocation
  softwareName  = 'PoorMansTSqlFormatter.Plex*'
  checksum      = $msichecksum
  checksumType  = 'md5'
  silentArgs    = "/qn /norestart /l*v `"$($env:TEMP)\$($packageName).$($env:chocolateyPackageVersion).MsiInstall.log`"" # ALLUSERS=1 DISABLEDESKTOPSHORTCUT=1 ADDDESKTOPICON=0 ADDSTARTMENU=0
  validExitCodes= @(0, 3010, 1641)  
}
Install-ChocolateyInstallPackage @packageArgs # https://chocolatey.org/docs/helpers-install-chocolatey-install-package

. .\tools\Get-VisualStudioVsixInstaller.ps1
. .\tools\Get-WillowInstalledProducts.ps1
$vsixInstaller = Get-VisualStudioVsixInstaller -Latest
Write-Verbose ('Found VSIXInstaller version {0}: {1}' -f $vsixInstaller.Version, $vsixInstaller.Path)
$installer = $vsixInstaller.Path

$vsix = Join-Path $toolsDir 'PoorMansTSqlFormatterVSPackage2019.vsix'
# The Install-ChocolateyVsixPackage cmdlet below can't ever seem to locate the install location so we're punting with the function above to locate vsixInstaller
# Install-ChocolateyVsixPackage -PackageName "PoorMansTSqlFormatterVSPackage2019" $vsix -VsVersion 16
$exitCode = Write-Host "Installing $vsix using $installer" 
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName=$installer
$psi.Arguments= $vsix
$s = [System.Diagnostics.Process]::Start($psi)
$s.WaitForExit()
$exitCode = $s.ExitCode
if($exitCode -gt 0 -and $exitCode -ne 1001) { #1001: Already installed
  throw "There was an error installing '$packageName'. The exit code returned was $exitCode."
}
