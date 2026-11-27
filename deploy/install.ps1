# Installs Zoe for one Windows user and (optionally) starts her right now in that user's session.
# Run elevated on the target PC, from the folder containing Zoe.exe, sprites\ and zoe.json:
#
#   powershell -ExecutionPolicy Bypass -File install.ps1                       # for whoever is signed in at the screen
#   powershell -ExecutionPolicy Bypass -File install.ps1 -User Sam            # install, start at next logon
#   powershell -ExecutionPolicy Bypass -File install.ps1 -User Sam -StartNow  # ...and appear immediately
#   powershell -ExecutionPolicy Bypass -File install.ps1 -Uninstall
#
# Zoe must run inside her owner's interactive session (a SYSTEM service can't draw on her desktop),
# so she's launched by a scheduled task with an "at logon of her owner" trigger and an interactive principal.
# That also lets the task be started remotely over SSH while her owner is logged in: the surprise button.
param(
    [string]$User = "",
    [switch]$StartNow,
    [switch]$Uninstall
)
# Default: the account signed in at the screen (not the elevated admin, if UAC asked for other credentials).
# Win32_ComputerSystem gives the local name even for Microsoft-account logins, e.g. MYLAPTOP\sam.
if (-not $User -and -not $Uninstall) {
    $User = (Get-CimInstance Win32_ComputerSystem).UserName
    if (-not $User) { throw "Nobody is signed in at the screen; pass -User <name>" }
}
# Task Scheduler wants a qualified account name (e.g. MYPC\Sam) for interactive principals.
if ($User -and $User -notmatch "\\") { $User = "$env:COMPUTERNAME\$User" }
$ErrorActionPreference = "Stop"
$Dest = Join-Path $env:ProgramFiles "Zoe"   # admin-write-only, so nothing can be planted beside Zoe.exe
$Task = "Zoe"

if ($Uninstall) {
    Stop-ScheduledTask -TaskName $Task -ErrorAction SilentlyContinue
    Get-Process Zoe -ErrorAction SilentlyContinue | Stop-Process -Force
    Unregister-ScheduledTask -TaskName $Task -Confirm:$false -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force $Dest -ErrorAction SilentlyContinue
    Remove-Item -Force "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Zoe.lnk" -ErrorAction SilentlyContinue
    "Zoe uninstalled."
    return
}

$src = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not (Test-Path "$src\Zoe.exe")) { $src = (Get-Location).Path }
if (-not (Test-Path "$src\Zoe.exe")) { throw "Zoe.exe not found next to install.ps1 or in the current folder" }

# Stop a running copy so the exe can be replaced.
Get-Process Zoe -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

New-Item -ItemType Directory -Force $Dest | Out-Null
Copy-Item -Force "$src\Zoe.exe" $Dest
Remove-Item -Recurse -Force "$Dest\sprites" -ErrorAction SilentlyContinue
Copy-Item -Recurse -Force "$src\sprites" $Dest
# Keep an already-tuned zoe.json unless this is a first install.
if (-not (Test-Path "$Dest\zoe.json")) { Copy-Item "$src\zoe.json" $Dest }
# Program Files already denies writes to ordinary users; make it explicit and drop anything inherited.
icacls $Dest /inheritance:r /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F" "*S-1-5-32-545:(OI)(CI)RX" | Out-Null
icacls $Dest /setowner "*S-1-5-32-544" /T | Out-Null

# --autostart lets Zoe's own menu ("Start when I sign in") decline the sign-in start without admin rights.
$action = New-ScheduledTaskAction -Execute "$Dest\Zoe.exe" -Argument "--autostart" -WorkingDirectory $Dest
# A Start menu entry, so she can be brought back after "Goodbye for today".
$lnk = (New-Object -ComObject WScript.Shell).CreateShortcut("$env:ProgramData\Microsoft\Windows\Start Menu\Programs\Zoe.lnk")
$lnk.TargetPath = "$Dest\Zoe.exe"; $lnk.WorkingDirectory = $Dest; $lnk.Description = "Zoe the desk pet"; $lnk.Save()
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $User
$trigger.Delay = "PT20S"   # let the desktop settle before she drops in
$principal = New-ScheduledTaskPrincipal -UserId $User -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -StartWhenAvailable
Register-ScheduledTask -TaskName $Task -Action $action -Trigger $trigger -Principal $principal `
    -Settings $settings -Description "Zoe the desk pet" -Force | Out-Null
"Installed to $Dest; starts at $User's logon."

if ($StartNow) {
    Start-ScheduledTask -TaskName $Task
    # The first start unpacks the single-file exe, which can take several seconds.
    for ($i = 0; $i -lt 30 -and -not (Get-Process Zoe -ErrorAction SilentlyContinue); $i++) { Start-Sleep 1 }
    $short = $User.Split("\")[-1]
    if (Get-Process Zoe -ErrorAction SilentlyContinue) { "Zoe is running." }
    else { "Task started but Zoe isn't running - is $User logged in, and is 'Start when I sign in' ticked in her menu? Log: C:\Users\$short\AppData\Local\Zoe\zoe.log" }
}
