# Read-only: who's logged in, is the session locked, display setup, admin rights.
$os = Get-CimInstance Win32_OperatingSystem
"OS: $($os.Caption) build $($os.BuildNumber)"
"Sessions:"; quser 2>&1
"LogonUI (lock screen) running: $([bool](Get-Process LogonUI -ErrorAction SilentlyContinue))"
Add-Type -AssemblyName System.Windows.Forms
"Admin: $(([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(544))"
Get-CimInstance Win32_VideoController | ForEach-Object { "GPU: $($_.Name) $($_.CurrentHorizontalResolution)x$($_.CurrentVerticalResolution)" }
Get-CimInstance -Namespace root\wmi WmiMonitorBasicDisplayParams -ErrorAction SilentlyContinue | ForEach-Object { "Monitor: active=$($_.Active)" }
"Local users:"; Get-LocalUser | Where-Object Enabled | ForEach-Object { "  $($_.Name)" }
