' Starts the "Tidy Terminals" scheduled task (runs as admin, no prompt) with no visible window.
' Used by the desktop shortcut / Ctrl+Alt+T.
CreateObject("WScript.Shell").Run "schtasks /run /tn ""Tidy Terminals""", 0, False
