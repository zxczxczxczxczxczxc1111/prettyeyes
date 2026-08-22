# What the trim alone costs a screenshot.
#
# The 62.9 ms we measured earlier was NOT the price of the trim: the capture
# engine was released in the very same tick, so that number is "rebuild the D3D
# devices AND fault the pages back". This script separates the two.
#
# Method: two bursts. A fast one, where shots follow each other inside the trim
# delay, so no trim happens in between. A slow one, where every shot is preceded
# by a trim. Classification is read out of the log, not out of counting: for
# every capture we know whether a trim line came before it.
#
# ASCII only: this console mangles UTF-8 in scripts.
param(
    [int]$Shots = 10
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "src\PrettyEyes.App\bin\Release\net10.0-windows10.0.22621.0\win-x64\publish\PrettyEyes.App.exe"
$log = Join-Path $env:APPDATA "prettyeyes\log.txt"

if (-not (Test-Path $publish)) {
    Write-Host "Publish first: dotnet publish src/PrettyEyes.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true" -ForegroundColor Yellow
    exit 1
}

$others = Get-Process -Name "PrettyEyes.App" -ErrorAction SilentlyContinue
if ($others) {
    Write-Host "prettyeyes is already running:" -ForegroundColor Red
    foreach ($other in $others) { Write-Host ("  pid " + $other.Id + "  " + $other.Path) }
    Write-Host "Close it via the tray menu and run this again. I will not kill it for you."
    exit 1
}

$logStart = 0
if (Test-Path $log) { $logStart = (Get-Item $log).Length }

Write-Host "Starting the published build (NOT the installed one)..."
$app = Start-Process -FilePath $publish -PassThru
Start-Sleep -Seconds 20

Write-Host ""
Write-Host ("BURST ONE, fast. Press the hotkey and Escape " + $Shots + " times with NO pauses:") -ForegroundColor Cyan
Write-Host "less than four seconds between shots, otherwise a trim sneaks in."
Write-Host "Press Enter here when done."
[void](Read-Host)

Write-Host ""
Write-Host ("BURST TWO, slow. Press the hotkey and Escape " + $Shots + " times,") -ForegroundColor Cyan
Write-Host "waiting about EIGHT seconds between shots so a trim happens before each one."
Write-Host "Press Enter here when done."
[void](Read-Host)

if ($app.HasExited) {
    Write-Host "The app died. The numbers below would be about nothing." -ForegroundColor Red
    exit 1
}

$stream = [System.IO.File]::Open($log, "Open", "Read", "ReadWrite")
$stream.Position = $logStart
$reader = New-Object System.IO.StreamReader($stream)
$tail = $reader.ReadToEnd()
$reader.Close()

$warm = @()
$cold = @()
$trimPending = $false

foreach ($line in ($tail -split "`r?`n")) {
    if ($line -match "\(trim\)") { $trimPending = $true; continue }

    $hit = [regex]::Match($line, "capture: ([0-9]+)[.,]([0-9]+)")
    if (-not $hit.Success) { continue }

    $value = [double]($hit.Groups[1].Value + "." + $hit.Groups[2].Value)

    if ($trimPending) { $cold += $value; $trimPending = $false }
    else { $warm += $value }
}

function Report($label, $values) {
    if ($values.Count -eq 0) {
        Write-Host ($label + ": nothing recorded") -ForegroundColor Yellow
        return $null
    }
    $sorted = $values | Sort-Object
    $median = $sorted[[int]([math]::Floor($sorted.Count / 2))]
    $line = "{0,-22} n={1,3}  median={2,6:N1} ms  worst={3,6:N1} ms" -f $label, $values.Count, $median, ($sorted[-1])
    Write-Host $line
    return $median
}

Write-Host ""
Write-Host "--- capture cost ---"
# The first capture of the run is the warm-up and belongs to start-up, not here.
if ($warm.Count -gt 1) { $warm = $warm[1..($warm.Count - 1)] }
$warmMedian = Report "no trim before it" $warm
$coldMedian = Report "trimmed before it" $cold

if ($null -ne $warmMedian -and $null -ne $coldMedian) {
    $price = [math]::Round($coldMedian - $warmMedian, 1)
    Write-Host ""
    Write-Host ("The trim costs " + $price + " ms on the next screenshot.")
    if ($price -lt 25) {
        Write-Host "Cheap enough to do after every shot." -ForegroundColor Green
    } elseif ($price -lt 60) {
        Write-Host "Noticeable but survivable. Worth a second opinion." -ForegroundColor Yellow
    } else {
        Write-Host "Too expensive to do after every shot. Go back to trimming on idle only." -ForegroundColor Red
    }
}

Write-Host ""
Write-Host ("Stopping the copy I started (pid " + $app.Id + "), nothing else.")
Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue
