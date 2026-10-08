# Installer. Run from an elevated PowerShell (right-click > Run as administrator).
#   1. Registers a "Tidy Terminals" scheduled task that runs with highest privileges, so the
#      tidy script can also move terminals that were opened as administrator.
#   2. Creates a Desktop shortcut that starts the task.
#   3. Adds a Startup entry for a tiny background listener that owns the Ctrl+Alt+T hotkey
#      (more reliable than a shortcut-file hotkey) and starts it now.
# The elevated scripts are copied to Program Files; re-run this after editing tidy-terminals.ps1.
# After this, using the shortcut or hotkey shows no admin prompt.

$ErrorActionPreference = 'Stop'

$principalCheck = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principalCheck.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this from an elevated (administrator) PowerShell.'
}

$dir = $PSScriptRoot

# The task runs elevated, so the scripts it runs must not be writable by a normal user
# (otherwise any program running as you could edit them and gain admin with no prompt).
# Copy them under Program Files, where only administrators can write.
$secure = Join-Path $env:ProgramFiles 'TidyTerminals'
New-Item -ItemType Directory -Force -Path $secure | Out-Null
Copy-Item (Join-Path $dir 'tidy-terminals.ps1'), (Join-Path $dir 'tidy-terminals.vbs') -Destination $secure -Force

$user = "$env:USERDOMAIN\$env:USERNAME"

$action = New-ScheduledTaskAction -Execute "$env:WINDIR\System32\wscript.exe" `
    -Argument ('"{0}"' -f (Join-Path $secure 'tidy-terminals.vbs'))
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
$lnk.Description = 'Snap all terminal windows into a grid'
$lnk.Save()

$startup = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Startup')) 'Tidy Terminals Hotkey.lnk'))
$startup.TargetPath = "$env:WINDIR\System32\wscript.exe"
$startup.Arguments = '"{0}"' -f (Join-Path $dir 'hotkey-listener.vbs')
$startup.WorkingDirectory = $dir
$startup.WindowStyle = 7
$startup.Save()
Start-Process "$env:WINDIR\System32\wscript.exe" -ArgumentList ('"{0}"' -f (Join-Path $dir 'hotkey-listener.vbs'))

Write-Host 'Installed. Use the "Tidy Terminals" Desktop shortcut or press Ctrl+Alt+T.'
