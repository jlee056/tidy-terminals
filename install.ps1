# Installer. Run from a normal PowerShell window.
# Creates a Desktop shortcut and a Startup entry for the Ctrl+Alt+T listener.
# The listener starts at the next sign-in, or can be started manually after installation.

$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot

# Older versions used an elevated scheduled task. Remove it when this installer is
# elevated; otherwise tell the user how to remove it. The shortcut below never uses it.
$legacyTask = Get-ScheduledTask -TaskName 'Tidy Terminals' -ErrorAction SilentlyContinue
if ($legacyTask) {
    $principalCheck = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if ($principalCheck.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Unregister-ScheduledTask -TaskName 'Tidy Terminals' -Confirm:$false
    } else {
        Write-Warning 'An old elevated Tidy Terminals task remains. In an administrator PowerShell window, run: Unregister-ScheduledTask -TaskName "Tidy Terminals" -Confirm:$false'
    }
}

$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Tidy Terminals.lnk'))
$lnk.TargetPath = "$env:WINDIR\System32\wscript.exe"
$lnk.Arguments = '"{0}"' -f (Join-Path $dir 'run-tidy-task.vbs')
$lnk.WorkingDirectory = $dir
$lnk.Description = 'Snap all terminal windows into a grid'
$lnk.Save()

$startup = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Startup')) 'Tidy Terminals Hotkey.lnk'))
$startup.TargetPath = "$env:WINDIR\System32\wscript.exe"
$startup.Arguments = '"{0}"' -f (Join-Path $dir 'hotkey-listener.vbs')
$startup.WorkingDirectory = $dir
$startup.WindowStyle = 7
$startup.Save()

Write-Host 'Installed. Use the "Tidy Terminals" Desktop shortcut now.'
Write-Host 'The Ctrl+Alt+T hotkey starts at your next sign-in, or double-click hotkey-listener.vbs to start it now.'
