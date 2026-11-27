# Run as admin over SSH: launches run-test.ps1 inside $User's interactive session via a one-off task.
param([string]$User = "", [int]$Seconds = 75)
# Task Scheduler wants a qualified account name (e.g. MYPC\Sam) for interactive principals.
if ($User -notmatch "\\") { $User = "$env:COMPUTERNAME\$User" }
$a = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File C:\ZoeTest\run-test.ps1 -Seconds $Seconds"
$pr = New-ScheduledTaskPrincipal -UserId $User -LogonType Interactive -RunLevel Limited
Register-ScheduledTask -TaskName "ZoeTest" -Action $a -Principal $pr -Force | Out-Null
Start-ScheduledTask -TaskName "ZoeTest"
"started"
