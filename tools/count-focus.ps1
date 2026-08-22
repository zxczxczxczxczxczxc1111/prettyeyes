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
# a change anybody caused. Everything else is counted per capture rather than
# per run: the first version of this script announced "more than two" after four
# perfectly good cycles, which is a fine way to report a fix as a failure.
$caused = [Math]::Max(0, $changes.Count - 1)
$cycles = ($changes | Where-Object { $_.What -like "Avalonia-*" } | Measure-Object).Count
$ours = ($changes | Where-Object { $_.What -like "Avalonia-*" } | ForEach-Object { ($_.What -split "  ")[0] } | Sort-Object -Unique)

Write-Host ""
Write-Host ("Foreground changes: " + $caused + " over " + $cycles + " capture(s)")

if ($cycles -eq 0) {
    Write-Host "No capture happened. Press the hotkey while it is watching." -ForegroundColor Yellow
    exit 0
}

Write-Host ("Our windows that took the foreground: " + $ours.Count)
foreach ($window in $ours) { Write-Host ("  " + $window) }

$each = [Math]::Round($caused / $cycles, 1)
Write-Host ("Per capture: " + $each)

if ($ours.Count -gt 1) {
    Write-Host "More than one of our windows takes the foreground: that is one repaint per monitor." -ForegroundColor Yellow
} elseif ($each -le 2.5) {
    Write-Host "The overlay takes the foreground once and gives it back once. That is the floor." -ForegroundColor Green
} else {
    Write-Host "More than two per capture: something takes the foreground more often than it needs to." -ForegroundColor Yellow
}
