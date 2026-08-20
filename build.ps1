# Publishes the app and builds the installer. One command, no memory required.
$ErrorActionPreference = "Stop"

$publish = "src\PrettyEyes.App\bin\Release\net10.0-windows10.0.22621.0\win-x64\publish"
# Inno Setup installs per-user or per-machine depending on how it was set up.
$isccCandidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "ISCC.exe не найден. Поставь Inno Setup 6: winget install JRSoftware.InnoSetup"
}

dotnet publish src/PrettyEyes.App -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if (-not (Test-Path "$publish\PrettyEyes.App.exe")) {
    throw "Публикация не дала исполняемый файл: $publish"
}

& $iscc "installer\prettyeyes.iss"
Write-Host "Установщик собран в dist\"

# The hash goes into the release notes as "sha256: <хэш>". The updater refuses
# a release without one, so printing it here is not a nicety: forget the line
# and nobody updates.
$setup = Get-ChildItem "dist\prettyeyes-setup-*.exe" | Sort-Object LastWriteTime | Select-Object -Last 1

if ($setup) {
    $hash = (Get-FileHash $setup.FullName -Algorithm SHA256).Hash.ToLower()
    Write-Host ""
    Write-Host "В описание релиза строкой:"
    Write-Host "sha256: $hash"
}
