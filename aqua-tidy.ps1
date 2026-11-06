# Aqua Tidy: the fun version of Tidy Terminals. Little pixel Aquas carry every terminal
# into the same grid. Run from the "Aqua Tidy" Desktop shortcut or Ctrl+Alt+A.
# Esc (or 20 seconds) snaps any window still in flight straight into place.
#
#   aqua-tidy.ps1                  run it
#   aqua-tidy.ps1 -Preview x.png   render every sprite frame to a sheet (for editing the art)
#   aqua-tidy.ps1 -MakeIcon        rebuild aqua.ico from the sprite art

param([string]$Preview, [switch]$MakeIcon)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$src = Join-Path $here 'AquaTidy.cs'
$sprites = Join-Path $here 'aqua-sprites.txt'
$cache = Join-Path $env:LOCALAPPDATA 'AquaTidy'
$logFile = Join-Path $cache 'aqua-tidy.log'

function Import-Engine {
    # Compiling takes a second or two, so keep the built DLL and reuse it until the source changes
    New-Item -ItemType Directory -Force $cache | Out-Null
    $hash = (Get-FileHash $src -Algorithm SHA256).Hash.Substring(0, 16)
    $dll = Join-Path $cache "AquaTidy-$hash.dll"
    if (-not (Test-Path $dll)) {
        Get-ChildItem $cache -Filter 'AquaTidy-*.dll' -ErrorAction SilentlyContinue | Remove-Item -ErrorAction SilentlyContinue
        Add-Type -Path $src -ReferencedAssemblies System.Windows.Forms, System.Drawing -OutputAssembly $dll -OutputType Library
    }
    Add-Type -Path $dll
}

if ($Preview) { Import-Engine; [AquaTidy.Engine]::SaveSheet($sprites, $Preview); return }
if ($MakeIcon) { Import-Engine; [AquaTidy.Engine]::SaveIcon($sprites, (Join-Path $here 'aqua.ico')); return }

# One Aqua run at a time
$created = $false
$mutex = New-Object System.Threading.Mutex($true, 'Local\AquaTidyRun', [ref]$created)
if (-not $created) { return }

try {
    Import-Engine
    [AquaTidy.Engine]::Run($sprites)
} catch {
    # Something broke: log it and tidy the boring way so the windows still end up in place
    try { "$(Get-Date -Format s)  $($_.Exception.Message)" | Out-File -Append -Encoding utf8 $logFile } catch {}
    & (Join-Path $here 'tidy-terminals.ps1')
} finally {
    $mutex.ReleaseMutex()
}
