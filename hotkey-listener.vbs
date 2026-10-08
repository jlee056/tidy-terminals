' Starts hotkey-listener.ps1 hidden (used by the Startup shortcut). Full paths, no PATH lookup.
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
ps1 = fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName), "hotkey-listener.ps1")
pwsh = sh.ExpandEnvironmentStrings("%SystemRoot%") & "\System32\WindowsPowerShell\v1.0\powershell.exe"
sh.Run """" & pwsh & """ -STA -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File """ & ps1 & """", 0, False
