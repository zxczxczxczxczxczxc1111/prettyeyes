# What the application costs when somebody leans on it.
#
# "Feels fine" is not a measurement, and a leak is invisible for exactly as
# long as nobody counts. This drives the real application on the real desktop
# far harder than a person would - a hundred captures back to back, the
# hotkey pressed faster than a capture can finish, thousands of strokes, the
# pointer switched on and off forty times - and counts four things that a leak
# shows up in before the memory does: handles, GDI objects, USER objects and
# windows.
#
# The rule for reading the output: every counter has to come back down after
# the idle phase. Memory that stays high can be a heap that has not been
# handed back yet; GDI objects, USER objects or windows that stay high are a
# leak, because nothing releases those lazily.
#
# Runs against the check build on purpose - it keeps its own settings folder,
# so the hotkeys written here do not touch the installed copy.
#
# ASCII only: this console mangles UTF-8 in scripts.
param(
    [string]$Exe = "D:\claude11\_rabota\pe-check\PrettyEyes.App.exe",
    [string]$Settings = "$env:APPDATA\prettyeyes-check\settings.json",
    # One capture is open-drag-annotate-escape. A hundred is more than a heavy
    # day of use and takes about three minutes.
    [int]$Shots = 100,
    # Captures asked for faster than one can finish, to see what re-entry does.
    [int]$Hammer = 30,
    # Freehand strokes per capture in the drawing phase: every one of them is a
    # document edit, an undo entry and a re-render.
    [int]$Strokes = 40,
    # The over-the-screen pointer, switched on and off. Each round builds a
    # window per monitor and tears it down again.
    [int]$Toggles = 40,
    # Seconds of holding the button down and flooding positions.
    [int]$DrawSeconds = 20,
    # Long enough for anything lazy to have happened.
    [int]$SettleSeconds = 30
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class LoadProbe {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte s, uint f, UIntPtr e);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr process, uint flags);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr data);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);

  public delegate bool EnumProc(IntPtr h, IntPtr data);

  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

  public const uint MOVE = 0x0001, LDOWN = 0x0002, LUP = 0x0004, KEYUP = 0x0002;
  public const uint GDI = 0, USER = 1;

  // Whose window is under a point. The overlay never takes the foreground -
  // it is shown without activating - so "is it up yet" cannot be asked of
  // GetForegroundWindow. It can be asked of the pixel it covers.
  public static uint OwnerAt(int x, int y) {
    var p = new POINT(); p.X = x; p.Y = y;
    uint pid;
    GetWindowThreadProcessId(WindowFromPoint(p), out pid);
    return pid;
  }

  // Visible top-level windows belonging to one process. A mode that leaks a
  // window leaks it here first, long before it costs a megabyte.
  public static int Windows(uint pid) {
    int count = 0;
    EnumWindows(delegate(IntPtr h, IntPtr d) {
      uint owner;
      GetWindowThreadProcessId(h, out owner);
      if (owner == pid && IsWindowVisible(h)) count++;
      return true;
    }, IntPtr.Zero);
    return count;
  }
}
'@

function Aim([int]$x, [int]$y) {
    [void][LoadProbe]::SetCursorPos($x, $y)
    [LoadProbe]::mouse_event([LoadProbe]::MOVE, 0, 0, 0, [UIntPtr]::Zero)
}

function Down { [LoadProbe]::mouse_event([LoadProbe]::LDOWN, 0, 0, 0, [UIntPtr]::Zero) }
function Up   { [LoadProbe]::mouse_event([LoadProbe]::LUP, 0, 0, 0, [UIntPtr]::Zero) }

# A gesture with time in it. Positions poured in with no gap at all arrive as
# one jump: the selection came out eleven pixels tall and the strokes phase
# quietly turned into forty re-drags of the frame, which loads nothing. The
# flood belongs in the pointer phase, where the input rate is the thing being
# tested; here the work per gesture is.

function Chord([byte[]]$keys) {
    foreach ($k in $keys) { [LoadProbe]::keybd_event($k, 0, 0, [UIntPtr]::Zero) }
    [array]::Reverse($keys)
    foreach ($k in $keys) { [LoadProbe]::keybd_event($k, 0, [LoadProbe]::KEYUP, [UIntPtr]::Zero) }
}

# Alt + G, Escape, Ctrl + Alt + L: the region capture, the way out, the pointer.
function Region { Chord @([byte]0x12, [byte]0x47) }
function Escape { Chord @([byte]0x1B) }
function Laser  { Chord @([byte]0x11, [byte]0x12, [byte]0x4C) }

function Snapshot([System.Diagnostics.Process]$proc, [string]$label) {
    $proc.Refresh()

    [pscustomobject]@{
        Phase   = $label
        WorkMB  = [math]::Round($proc.WorkingSet64 / 1MB, 1)
        PrivMB  = [math]::Round($proc.PrivateMemorySize64 / 1MB, 1)
        Handles = $proc.HandleCount
        Gdi     = [int][LoadProbe]::GetGuiResources($proc.Handle, [LoadProbe]::GDI)
        User    = [int][LoadProbe]::GetGuiResources($proc.Handle, [LoadProbe]::USER)
        Threads = $proc.Threads.Count
        Windows = [LoadProbe]::Windows([uint32]$proc.Id)
    }
}

# How long from asking for a capture to the overlay covering the screen. The
# number people mean by "does it feel instant".
function WaitOverlay([uint32]$owner, [int]$budgetMs = 4000) {
    $clock = [System.Diagnostics.Stopwatch]::StartNew()

    while ($clock.ElapsedMilliseconds -lt $budgetMs) {
        if ([LoadProbe]::OwnerAt(1280, 700) -eq $owner) { return [int]$clock.ElapsedMilliseconds }
        Start-Sleep -Milliseconds 4
    }

    return -1
}

# How long from wanting out to the screen being back. Escape walks down a
# ladder - drop the tool, then close - so the way out is pressed until it
# works and the whole walk is what gets timed. Timing one press would time
# whichever rung it happened to land on.
function LeaveOverlay([uint32]$owner) {
    $clock = [System.Diagnostics.Stopwatch]::StartNew()

    for ($press = 0; $press -lt 4; $press++) {
        Escape

        for ($wait = 0; $wait -lt 40; $wait++) {
            if ([LoadProbe]::OwnerAt(1280, 700) -ne $owner) { return [int]$clock.ElapsedMilliseconds }
            Start-Sleep -Milliseconds 5
        }
    }

    return -1
}

function Stats([int[]]$values, [string]$what) {
    $good = @($values | Where-Object { $_ -ge 0 })

    if ($good.Count -eq 0) {
        "$what : nothing measured"
        return
    }

    $sorted = $good | Sort-Object
    $median = $sorted[[int]($sorted.Count / 2)]
    $p95 = $sorted[[math]::Min($sorted.Count - 1, [int]($sorted.Count * 0.95))]
    $lost = $values.Count - $good.Count

    "{0}: min {1} ms, median {2} ms, p95 {3} ms, max {4} ms, over budget {5}" -f `
        $what, $sorted[0], $median, $p95, $sorted[-1], $lost
}

# --- the run -----------------------------------------------------------------

Get-Process -Name PrettyEyes.App -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $Exe } |
    ForEach-Object { $_.Kill(); [void]$_.WaitForExit(4000) }

# Written while nothing is running, or the application saves its own copy over
# it on the way out. A default tool means the drawing phase needs no toolbar
# button, whose position depends on where the selection ended up.
$json = Get-Content $Settings -Raw | ConvertFrom-Json
$json | Add-Member -NotePropertyName DefaultTool -NotePropertyValue 4 -Force
$json | Add-Member -NotePropertyName LaserHotkey -NotePropertyValue ([pscustomobject]@{ Modifiers = 3; VirtualKey = 76 }) -Force
$json | ConvertTo-Json -Depth 8 | Set-Content $Settings -Encoding UTF8

$proc = Start-Process -FilePath $Exe -PassThru
Start-Sleep -Seconds 4
$ours = [uint32]$proc.Id

$rows = @()
$rows += Snapshot $proc "start"

try {
    # 1. Captures, one after another, each one opened, dragged and dropped.
    $open = @()
    $close = @()

    # Two halves with a snapshot between them, because the question a leak
    # answers to is not "did anything grow" - the first capture builds D3D, the
    # Skia surfaces and half the font cache, and that growth is once. It is
    # "did the second half grow as much as the first". Equal halves are a leak;
    # a first half and then nothing is a warm-up.
    $half = [int]($Shots / 2)

    for ($i = 0; $i -lt $Shots; $i++) {
        if ($i -eq $half) { $rows += Snapshot $proc "after the first $half captures" }

        Region
        $open += WaitOverlay $ours

        Aim 700 400
        Start-Sleep -Milliseconds 40
        Down
        foreach ($step in 1..8) {
            Aim (700 + $step * 100) (400 + $step * 60)
            Start-Sleep -Milliseconds 5
        }
        Up
        Start-Sleep -Milliseconds 80

        $close += LeaveOverlay $ours
        Start-Sleep -Milliseconds 60
    }

    $rows += Snapshot $proc "after all $Shots captures"

    # 2. The same key pressed faster than a capture can be built. Nothing is
    #    checked here except that the application is still answering afterwards.
    for ($i = 0; $i -lt $Hammer; $i++) {
        Region
        Start-Sleep -Milliseconds 70
        Escape
        Start-Sleep -Milliseconds 70
    }

    Start-Sleep -Milliseconds 800
    Escape
    Escape
    Start-Sleep -Milliseconds 500

    $rows += Snapshot $proc "after $Hammer hammered captures"

    # 3. One capture, drawn to death: every stroke is a document edit and an
    #    undo entry, and none of them are ever undone.
    Region
    $drawOpen = WaitOverlay $ours

    Aim 400 300
    Start-Sleep -Milliseconds 60
    Down
    foreach ($step in 1..12) {
        Aim (400 + $step * 130) (300 + $step * 55)
        Start-Sleep -Milliseconds 8
    }
    Up
    Start-Sleep -Milliseconds 400

    for ($s = 0; $s -lt $Strokes; $s++) {
        $y = 380 + (($s % 20) * 25)
        Aim 500 $y
        Start-Sleep -Milliseconds 20
        Down
        foreach ($step in 1..30) {
            Aim (500 + $step * 30) ($y + [math]::Sin($step / 3.0) * 12)
            Start-Sleep -Milliseconds 4
        }
        Up
        Start-Sleep -Milliseconds 20
    }

    $rows += Snapshot $proc "after $Strokes strokes, overlay still open"

    # Proof that the strokes were strokes. Without the picture this phase can
    # silently degrade into dragging the frame around, which loads nothing.
    $bmp = New-Object System.Drawing.Bitmap 1200, 700
    $canvas = [System.Drawing.Graphics]::FromImage($bmp)
    $canvas.CopyFromScreen(400, 300, 0, 0, $bmp.Size)
    $canvas.Dispose()
    $shotPath = Join-Path ([System.IO.Path]::GetTempPath()) "load-test-strokes.png"
    $bmp.Save($shotPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()

    [void](LeaveOverlay $ours)
    Start-Sleep -Milliseconds 600

    $rows += Snapshot $proc "after that overlay closed"

    # 4. The pointer over the live screen: a window per monitor, built and torn
    #    down every round.
    # Split in half for the same reason the captures are: the first round
    # builds the panes' surfaces once and for all.
    $halfT = [int]($Toggles / 2)

    for ($i = 0; $i -lt $Toggles; $i++) {
        if ($i -eq $halfT) { $rows += Snapshot $proc "after the first $halfT pointer rounds" }

        Laser
        Start-Sleep -Milliseconds 250
        Laser
        Start-Sleep -Milliseconds 250
    }

    Start-Sleep -Milliseconds 800
    $rows += Snapshot $proc "after all $Toggles pointer rounds"

    # 5. The pointer drawn on, at a rate no arm produces: this is the frame
    #    budget under the worst input the mode can be given.
    Laser
    Start-Sleep -Milliseconds 900

    Aim 600 700
    Down

    $clock = [System.Diagnostics.Stopwatch]::StartNew()
    $moves = 0

    while ($clock.Elapsed.TotalSeconds -lt $DrawSeconds) {
        $t = $moves / 90.0
        Aim ([int](1280 + 620 * [math]::Cos($t))) ([int](700 + 330 * [math]::Sin($t * 1.3)))
        $moves++
    }

    Up
    $rate = [math]::Round($moves / $clock.Elapsed.TotalSeconds)
    Start-Sleep -Milliseconds 1500

    $rows += Snapshot $proc "after $DrawSeconds s of drawing at $rate positions a second"

    Laser
    Start-Sleep -Milliseconds 800

    $rows += Snapshot $proc "after the pointer was put away"

    # 6. Left alone. Anything that only settles lazily has now had its chance.
    Start-Sleep -Seconds $SettleSeconds
    $rows += Snapshot $proc "after $SettleSeconds s idle"
}
finally {
    # A button left down turns the rest of the day into a drag gesture.
    Up
    Escape
}

""
$rows | Format-Table -AutoSize | Out-String -Width 200

$first = $rows[0]
$last = $rows[-1]

"deltas from start to the end of the idle phase:"
"  working set {0:+0.0;-0.0;0} MB, private {1:+0.0;-0.0;0} MB" -f `
    ($last.WorkMB - $first.WorkMB), ($last.PrivMB - $first.PrivMB)
"  handles {0:+0;-0;0}, GDI {1:+0;-0;0}, USER {2:+0;-0;0}, threads {3:+0;-0;0}, windows {4:+0;-0;0}" -f `
    ($last.Handles - $first.Handles), ($last.Gdi - $first.Gdi), ($last.User - $first.User), `
    ($last.Threads - $first.Threads), ($last.Windows - $first.Windows)

$halves = @($rows | Where-Object { $_.Phase -like "*captures" -and $_.Phase -notlike "*hammered*" })

$rounds = @($rows | Where-Object { $_.Phase -like "*pointer rounds" })
$beforeRounds = $rows[[array]::IndexOf($rows, $rounds[0]) - 1]

if ($halves.Count -eq 2) {
    ""
    "the two halves of the capture phase, which is the leak question:"
    "  first  half: private {0:+0.0;-0.0;0} MB, handles {1:+0;-0;0}, GDI {2:+0;-0;0}, USER {3:+0;-0;0}" -f `
        ($halves[0].PrivMB - $rows[0].PrivMB), ($halves[0].Handles - $rows[0].Handles), `
        ($halves[0].Gdi - $rows[0].Gdi), ($halves[0].User - $rows[0].User)
    "  second half: private {0:+0.0;-0.0;0} MB, handles {1:+0;-0;0}, GDI {2:+0;-0;0}, USER {3:+0;-0;0}" -f `
        ($halves[1].PrivMB - $halves[0].PrivMB), ($halves[1].Handles - $halves[0].Handles), `
        ($halves[1].Gdi - $halves[0].Gdi), ($halves[1].User - $halves[0].User)
}

if ($rounds.Count -eq 2) {
    ""
    "and the two halves of the pointer phase, where a window is built and torn down each round:"
    "  first  half: private {0:+0.0;-0.0;0} MB, handles {1:+0;-0;0}, GDI {2:+0;-0;0}, USER {3:+0;-0;0}, windows {4:+0;-0;0}" -f `
        ($rounds[0].PrivMB - $beforeRounds.PrivMB), ($rounds[0].Handles - $beforeRounds.Handles), `
        ($rounds[0].Gdi - $beforeRounds.Gdi), ($rounds[0].User - $beforeRounds.User), `
        ($rounds[0].Windows - $beforeRounds.Windows)
    "  second half: private {0:+0.0;-0.0;0} MB, handles {1:+0;-0;0}, GDI {2:+0;-0;0}, USER {3:+0;-0;0}, windows {4:+0;-0;0}" -f `
        ($rounds[1].PrivMB - $rounds[0].PrivMB), ($rounds[1].Handles - $rounds[0].Handles), `
        ($rounds[1].Gdi - $rounds[0].Gdi), ($rounds[1].User - $rounds[0].User), `
        ($rounds[1].Windows - $rounds[0].Windows)
}

""
Stats $open "capture opened in"
Stats $close "capture closed in"
"one long capture opened in $drawOpen ms"
"the drawn-on capture was photographed to $shotPath"

""
"still answering: " + $(if ((WaitOverlay $ours 1) -ge 0) { "overlay is up, which it should not be" } else { "yes" })

$proc.Refresh()
"alive: " + (-not $proc.HasExited)

# What the application said about itself while all that was going on. The
# pointer only writes a line when it is behind, so silence there is the pass.
$log = Join-Path (Split-Path $Settings) "log.txt"

if (Test-Path $log) {
    ""
    "what the log says about the run:"
    $said = Get-Content $log -Tail 400 |
        Where-Object { $_ -match "trim|LMB|frames|error|exception|failed" }

    if ($said) { $said | Select-Object -Last 20 } else { "  nothing worth quoting" }

    # The pointer tags its own complaint with "(fps)" so this line can find it
    # without reading Cyrillic, which this console cannot.
    $slow = @(Get-Content $log -Tail 400 | Where-Object { $_ -match "\(fps\)" })
    "  pointer complained about its frame rate: " + $(if ($slow.Count) { "$($slow.Count) times" } else { "never" })
}
