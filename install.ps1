# Installer. Run from an elevated PowerShell (right-click > Run as administrator).
#   1. Registers a "Tidy Terminals" scheduled task that runs with highest privileges, so the
#      tidy script can also move terminals that were opened as administrator.
#   2. Creates a Desktop shortcut that starts the task, with hotkey Ctrl+Alt+T.
# After this, using the shortcut or hotkey shows no admin prompt.

$ErrorActionPreference = 'Stop'

$principalCheck = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principalCheck.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this from an elevated (administrator) PowerShell.'
}

$dir = $PSScriptRoot
$user = "$env:USERDOMAIN\$env:USERNAME"

$action = New-ScheduledTaskAction -Execute "$env:WINDIR\System32\wscript.exe" `
    -Argument ('"{0}"' -f (Join-Path $dir 'tidy-terminals.vbs'))
$principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName 'Tidy Terminals' -Action $action -Principal $principal `
    -Settings $settings -Description 'Snap all terminal windows into a grid (admin, so it can move admin windows)' -Force |
    Out-Null

$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Tidy Terminals.lnk'))
$lnk.TargetPath = "$env:WINDIR\System32\wscript.exe"
$lnk.Arguments = '"{0}"' -f (Join-Path $dir 'run-tidy-task.vbs')
$lnk.WorkingDirectory = $dir
$lnk.Hotkey = 'Ctrl+Alt+T'
$lnk.Description = 'Snap all terminal windows into a grid'
$lnk.Save()

Write-Host 'Installed. Use the "Tidy Terminals" Desktop shortcut or press Ctrl+Alt+T.'
