$ErrorActionPreference = 'Stop'; # stop on all errors

# vsixinstaller /u:5e84b709-1e60-4116-a702-4cdb1a282d6e
. .\tools\Get-VisualStudioVsixInstaller.ps1
. .\tools\Get-WillowInstalledProducts.ps1
$vsixInstaller = Get-VisualStudioVsixInstaller -Latest
Write-Verbose ('Found VSIXInstaller version {0}: {1}' -f $vsixInstaller.Version, $vsixInstaller.Path)
$installer = $vsixInstaller.Path
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName=$installer
$psi.Arguments="/u:5e84b709-1e60-4116-a702-4cdb1a282d6e"
$s = [System.Diagnostics.Process]::Start($psi)
$s.WaitForExit()
$exitCode = $s.ExitCode
if($exitCode -gt 0) { 
  throw "There was an error uninstalling VSIX. The exit code returned was $exitCode."
}
