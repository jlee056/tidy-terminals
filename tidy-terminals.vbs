' Runs tidy-terminals.ps1 with no visible window. The script sits next to this file.
' Uses full paths for the interpreter so a planted powershell.exe on PATH can't be picked up
' when this runs elevated.
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
ps1 = fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName), "tidy-terminals.ps1")
pwsh = sh.ExpandEnvironmentStrings("%SystemRoot%") & "\System32\WindowsPowerShell\v1.0\powershell.exe"
sh.Run """" & pwsh & """ -NoProfile -ExecutionPolicy Bypass -File """ & ps1 & """", 0, False
