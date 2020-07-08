$ErrorActionPreference = 'Stop'; # stop on all errors

# vsixinstaller /u:5e84b709-1e60-4116-a702-4cdb1a282d6e
$installer = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\Common7\IDE\VSIXInstaller.exe"
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName=$installer
$psi.Arguments="/u:5e84b709-1e60-4116-a702-4cdb1a282d6e"
$s = [System.Diagnostics.Process]::Start($psi)
$s.WaitForExit()
$exitCode = $s.ExitCode
if($exitCode -gt 0) { 
  throw "There was an error uninstalling VSIX. The exit code returned was $exitCode."
}
