# Task 0: what the working-set trim actually buys us.
#
# Three numbers per sample, and the difference between them is the whole point:
#   PrivateWS  - the "Memory" column in Task Manager. The one people quote.
#   RSS        - everything resident, shared pages included.
#   Commit     - what the process actually asked the system for. The trim does
#                NOT change this one, and if it does, something else happened.
#
# ASCII only on purpose: this console mangles UTF-8 in scripts, and a mangled
# script fails after the line that prints the count, which reads as success.
param(
    [int]$Shots = 20,
    [int]$IdleMinutes = 6
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "src\PrettyEyes.App\bin\Release\net10.0-windows10.0.22621.0\win-x64\publish\PrettyEyes.App.exe"
$log = Join-Path $env:APPDATA "prettyeyes\log.txt"

if (-not (Test-Path $publish)) {
    Write-Host "Publish first:" -ForegroundColor Yellow
    Write-Host "  dotnet publish src/PrettyEyes.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true"
    exit 1
}

# The trap that already cost one measurement run: a copy is already running, the
# single-instance mutex makes ours exit without a word, and every number below
# would describe a process that is not ours. Never kill it - it may be the
# installed copy the user is actually using.
$others = Get-Process -Name "PrettyEyes.App" -ErrorAction SilentlyContinue
if ($others) {
    Write-Host "prettyeyes is already running:" -ForegroundColor Red
    foreach ($other in $others) { Write-Host ("  pid " + $other.Id + "  " + $other.Path) }
    Write-Host "Close it (tray menu, Exit) and run this again. I will not kill it for you."
    exit 1
}

function Sample($label, $process) {
    $process.Refresh()
    $raw = Get-CimInstance Win32_PerfRawData_PerfProc_Process -Filter ("IDProcess=" + $process.Id)
    $privateWs = [math]::Round($raw.WorkingSetPrivate / 1MB)
    $rss = [math]::Round($process.WorkingSet64 / 1MB)
    $commit = [math]::Round($process.PrivateMemorySize64 / 1MB)
    $line = "{0,-26} privateWS={1,4} MB  rss={2,4} MB  commit={3,4} MB  handles={4,5}  threads={5,3}" -f `
        $label, $privateWs, $rss, $commit, $process.HandleCount, $process.Threads.Count
    Write-Host $line
    Add-Content -Path $report -Value $line -Encoding UTF8
    return $privateWs
}

$report = Join-Path $env:TEMP ("prettyeyes-trim-" + (Get-Date -Format "HHmmss") + ".txt")
$logStart = 0
if (Test-Path $log) { $logStart = (Get-Item $log).Length }

Write-Host ("Report: " + $report)
Write-Host "Starting the published build (NOT the installed one)..."
$app = Start-Process -FilePath $publish -PassThru
Start-Sleep -Seconds 20   # warm capture and the emoji atlas belong to start-up

$before = Sample "at start" $app

Write-Host ""
Write-Host ("Now press the capture hotkey and Escape " + $Shots + " times.") -ForegroundColor Cyan
Write-Host "Take your time. Press Enter here when done."
[void](Read-Host)

if ($app.HasExited) {
    Write-Host "The app died during the shots. Look at the log, the numbers below are worthless." -ForegroundColor Red
    exit 1
}

$loaded = Sample "after shots" $app

Write-Host ""
Write-Host ("DO NOT touch the keyboard for " + $IdleMinutes + " minutes: any key restarts the idle countdown.") -ForegroundColor Yellow

$trimmed = $null
$deadline = (Get-Date).AddMinutes($IdleMinutes)
while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 30
    if ($app.HasExited) {
        Write-Host "The app died while idling. Numbers below are worthless." -ForegroundColor Red
        exit 1
    }
    $elapsed = [math]::Round(((Get-Date) - $deadline.AddMinutes(-$IdleMinutes)).TotalMinutes, 1)
    $now = Sample ("idle " + $elapsed + " min") $app

    if ($null -eq $trimmed) {
        $tail = ""
        if (Test-Path $log) {
            $stream = [System.IO.File]::Open($log, "Open", "Read", "ReadWrite")
            $stream.Position = $logStart
            $reader = New-Object System.IO.StreamReader($stream)
            $tail = $reader.ReadToEnd()
            $reader.Close()
        }
        # The log is UTF-8 and this script is read as ANSI, so the line carries
        # an ASCII tail to match on. Nothing non-ASCII may appear in this file.
        if ($tail -match "\(trim\)") {
            $trimmed = $now
            Write-Host "  ^ this is the sample right after the trim fired" -ForegroundColor Green
        }
    }
}

Write-Host ""
Write-Host "Now press the capture hotkey ONCE and Escape, then press Enter here." -ForegroundColor Cyan
Write-Host "This measures what the first shot after a trim costs."
[void](Read-Host)

$after = Sample "after waking it up" $app

$costs = @()
if (Test-Path $log) {
    $stream = [System.IO.File]::Open($log, "Open", "Read", "ReadWrite")
    $stream.Position = $logStart
    $reader = New-Object System.IO.StreamReader($stream)
    $tail = $reader.ReadToEnd()
    $reader.Close()
    $costs = [regex]::Matches($tail, "capture: ([0-9]+[.,][0-9]+)") | ForEach-Object { $_.Groups[1].Value }
}

Write-Host ""
Write-Host "--- capture times from the log, in order ---"
Write-Host ($costs -join "  ")
Add-Content -Path $report -Value ("captures: " + ($costs -join "  ")) -Encoding UTF8

Write-Host ""
Write-Host "--- verdict ---"
if ($null -eq $trimmed) {
    Write-Host "FAIL: the trim never fired. Either the idle wait was too short, or a key was pressed." -ForegroundColor Red
} else {
    Write-Host ("Private working set: " + $loaded + " MB loaded -> " + $trimmed + " MB after the trim")
    if ($trimmed -le 40) {
        Write-Host "PASS: Task Manager will show a two-digit number." -ForegroundColor Green
    } else {
        Write-Host "The number moved, but not into the range this was done for." -ForegroundColor Yellow
    }
    Write-Host "Compare the LAST capture time above with the ones before it: that is the price."
}

Write-Host ""
Write-Host "Stopping the copy I started (pid " -NoNewline
Write-Host ($app.Id.ToString() + "), nothing else.")
Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
Write-Host ("Report saved: " + $report)
