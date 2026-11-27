# Runs INSIDE the logged-in user's session (via a scheduled task). Starts Zoe in diag mode and takes a
# screenshot every few seconds of the area around her. No keyboard/mouse input is sent.
param([int]$Seconds = 75)
$dir = "C:\ZoeTest"
Remove-Item -Recurse -Force "$dir\shots" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "$dir\shots" | Out-Null
# Surprise guard: if someone has used this PC in the last 10 minutes, don't show Zoe at all.
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Idle {
  [StructLayout(LayoutKind.Sequential)] struct LII { public uint cb; public uint t; }
  [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LII l);
  public static double Seconds() { var l = new LII(); l.cb = 8; GetLastInputInfo(ref l); return unchecked((uint)Environment.TickCount - l.t) / 1000.0; }
}
"@
$idle = [Idle]::Seconds()
"idle seconds at start: $idle" | Out-File "$dir\shots\idle.txt"
if ($idle -lt 600) { "aborted: user active $([int]$idle)s ago" | Out-File "$dir\shots\done.txt"; exit }
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$p = Start-Process "$dir\Zoe.exe" -ArgumentList "--diag","--exit-after",$Seconds -PassThru
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
for ($i = 0; $i -lt [math]::Floor($Seconds / 3); $i++) {
    Start-Sleep -Seconds 3
    if ($p.HasExited) { break }
    try {
        $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size, [System.Drawing.CopyPixelOperation]::SourceCopy)
        $small = New-Object System.Drawing.Bitmap $bmp, ([int]($b.Width / 2)), ([int]($b.Height / 2))
        $small.Save(("$dir\shots\{0:D3}.png" -f ($i * 3 + 3)))
        $g.Dispose(); $bmp.Dispose(); $small.Dispose()
    } catch { "shot $i failed: $_" | Out-File -Append "$dir\shots\errors.txt" }
}
$p.WaitForExit(15000) | Out-Null
"exit code: $($p.ExitCode)" | Out-File "$dir\shots\done.txt"
