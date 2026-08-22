# Counts how many times the foreground window changes.
#
# Every change is a repaint for whoever lost it and for whoever got it, and that
# repaint is what a browser tab strip flashing actually is. One capture should
# cost exactly two: into the overlay and back out again.
#
# Run it, then press the capture hotkey once and Escape once, then wait for it
# to finish and read the list.
#
# ASCII only: this console mangles UTF-8 in scripts.
param(
    [int]$Seconds = 20
)

$ErrorActionPreference = "Stop"

$code = @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public class Fg {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  public static string Describe(IntPtr h) {
    if (h == IntPtr.Zero) return "(none)";
    var title = new StringBuilder(160); GetWindowText(h, title, title.Capacity);
    var cls = new StringBuilder(160); GetClassName(h, cls, cls.Capacity);
    return cls.ToString() + "  \"" + title.ToString() + "\"";
  }
}
'@
Add-Type -TypeDefinition $code

Write-Host ("Watching the foreground for " + $Seconds + " seconds.") -ForegroundColor Cyan
Write-Host "Press the capture hotkey ONCE, select nothing, press Escape. Then leave the keyboard alone."
Write-Host ""

$last = [IntPtr]::Zero
$changes = @()
$stop = (Get-Date).AddSeconds($Seconds)

while ((Get-Date) -lt $stop) {
    $now = [Fg]::GetForegroundWindow()

    if ($now -ne $last) {
        $changes += [pscustomobject]@{
            At   = (Get-Date).ToString("HH:mm:ss.fff")
            What = [Fg]::Describe($now)
        }
        $last = $now
    }

    Start-Sleep -Milliseconds 8
}

Write-Host "--- foreground changes ---"
foreach ($change in $changes) { Write-Host ($change.At + "  " + $change.What) }

# The first entry is whatever was in front when the script started, so it is not
# a change anybody caused.
$caused = [Math]::Max(0, $changes.Count - 1)

Write-Host ""
Write-Host ("Changes caused during the run: " + $caused)
if ($caused -le 2) {
    Write-Host "Two or fewer: the overlay takes the foreground once and gives it back once." -ForegroundColor Green
} else {
    Write-Host "More than two: something is taking the foreground more often than it needs to." -ForegroundColor Yellow
}
