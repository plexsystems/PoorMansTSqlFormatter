## Install MSI
$ErrorActionPreference = 'Stop'; # stop on all errors
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$fileLocation = Join-Path $toolsDir 'PoorMansTSqlFormatterSSMSPackage.Setup.msi'
$checksumPath = Join-Path $toolsDir 'PoorMansTSqlFormatterSSMSPackage.Setup.msi.hash'
$msichecksum = [IO.File]::ReadAllText($checksumPath)

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

$vsix     = Join-Path $toolsDir 'PoorMansTSqlFormatterVSPackage2019.vsix'
# The Install-ChocolateyVsixPackage cmdlet below can't ever seem to locate the install location so we're punting to a "known" path for the *2019* VsixInstaller.exe for now
# Install-ChocolateyVsixPackage -PackageName "PoorMansTSqlFormatterVSPackage2019" $vsix -VsVersion 16
$installer = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\IDE\VSIXInstaller.exe"
# $exitCode = Install-Vsix "$installer" "$vsix"
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
