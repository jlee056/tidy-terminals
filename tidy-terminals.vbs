' Runs tidy-terminals.ps1 with no visible window. The script sits next to this file.
Set fso = CreateObject("Scripting.FileSystemObject")
ps1 = fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName), "tidy-terminals.ps1")
CreateObject("WScript.Shell").Run "powershell -NoProfile -ExecutionPolicy Bypass -File """ & ps1 & """", 0, False
