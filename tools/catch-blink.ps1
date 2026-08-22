# Films a strip of the screen fast enough to see what a flash is made of.
#
# The eye reports "it blinked twice". That is not enough to fix anything: a
# blink can be the window going inactive, the frozen screenshot arriving late,
# or the overlay fading out over the real desktop. All three look the same at
# full speed and completely different at a hundred frames a second.
#
# Point it at the browser tab strip, run it, press the capture hotkey once and
# Escape once. It writes every frame and a contact sheet of every Nth frame.
#
# ASCII only: this console mangles UTF-8 in scripts.
param(
    # Whose window to film. The strip is taken from the top of it, which is
    # where a browser keeps its tabs and Telegram keeps its header.
    [string]$Process = "chrome",
    [int]$Height = 44,
    [double]$Seconds = 4,
    [int]$Every = 3,
    # Only if the automatic hunt picks the wrong window.
    [int]$X = -1,
    [int]$Y = -1,
    [int]$Width = -1
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$finder = @'
using System;
using System.Runtime.InteropServices;
public class Rects {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
'@
Add-Type -TypeDefinition $finder

if ($X -lt 0 -or $Y -lt 0 -or $Width -lt 0) {
    # The biggest visible window of that process: a browser has a pile of
    # invisible helper windows and exactly one anybody is looking at.
    $best = $null
    $area = 0

    foreach ($candidate in Get-Process -Name $Process -ErrorAction SilentlyContinue) {
        if ($candidate.MainWindowHandle -eq [IntPtr]::Zero) { continue }

        $r = New-Object Rects+RECT
        [void][Rects]::GetWindowRect($candidate.MainWindowHandle, [ref]$r)
        $size = ($r.R - $r.L) * ($r.B - $r.T)

        if ($size -gt $area) { $area = $size; $best = $r }
    }

    if ($null -eq $best) {
        Write-Host ("No visible window found for process '" + $Process + "'.") -ForegroundColor Red
        Write-Host "Give -X -Y -Width by hand, or name another process."
        exit 1
    }

    $X = $best.L
    $Y = $best.T
    $Width = [Math]::Min(1200, $best.R - $best.L)
}

$out = Join-Path $env:TEMP ("prettyeyes-blink-" + (Get-Date -Format "HHmmss"))
New-Item -ItemType Directory -Path $out | Out-Null

Write-Host ("Filming " + $Width + "x" + $Height + " at " + $X + "," + $Y + " for " + $Seconds + " s") -ForegroundColor Cyan
Write-Host "Press the capture hotkey ONCE and Escape ONCE while it runs."
Write-Host "Starting in 2 seconds..."
Start-Sleep -Seconds 2

$frames = New-Object System.Collections.ArrayList
$stop = (Get-Date).AddSeconds($Seconds)

# Captured into memory first: writing PNGs inside the loop would halve the rate
# and the gaps are exactly what we are trying to see.
while ((Get-Date) -lt $stop) {
    $bmp = New-Object System.Drawing.Bitmap($Width, $Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($X, $Y, 0, 0, (New-Object System.Drawing.Size($Width, $Height)))
    $g.Dispose()
    [void]$frames.Add(@{ At = (Get-Date); Image = $bmp })
}

Write-Host ("Captured " + $frames.Count + " frames, writing them out...")

$index = 0
foreach ($frame in $frames) {
    $name = "{0:d4}-{1}.png" -f $index, $frame.At.ToString("HHmmss.fff")
    $frame.Image.Save((Join-Path $out $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $index++
}

# The contact sheet: every Nth frame stacked top to bottom, which turns a
# timeline into one picture that can be looked at all at once.
$picked = @()
for ($i = 0; $i -lt $frames.Count; $i += $Every) { $picked += $frames[$i] }

$sheet = New-Object System.Drawing.Bitmap($Width, ($picked.Count * $Height))
$paint = [System.Drawing.Graphics]::FromImage($sheet)
$row = 0
foreach ($frame in $picked) {
    $paint.DrawImage($frame.Image, 0, ($row * $Height), $Width, $Height)
    $row++
}
$paint.Dispose()
$sheetPath = Join-Path $out "sheet.png"
$sheet.Save($sheetPath, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()

foreach ($frame in $frames) { $frame.Image.Dispose() }

$span = ($frames[$frames.Count - 1].At - $frames[0].At).TotalMilliseconds
$rate = [Math]::Round($frames.Count / ($span / 1000), 1)

Write-Host ""
Write-Host ("Rate: " + $rate + " frames per second, one frame every " + [Math]::Round(1000 / $rate, 1) + " ms")
Write-Host ("Frames: " + $out)
Write-Host ("Contact sheet: " + $sheetPath)
