' Starts the "Tidy Terminals" scheduled task (runs as admin, no prompt) with no visible window.
' Used by the desktop shortcut / Ctrl+Alt+T.
Set sh = CreateObject("WScript.Shell")
sh.Run """" & sh.ExpandEnvironmentStrings("%SystemRoot%") & "\System32\schtasks.exe"" /run /tn ""Tidy Terminals""", 0, False
