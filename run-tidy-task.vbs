' Legacy launcher name kept so existing Desktop shortcuts still work.
' Run the tiler directly; the elevated scheduled task can refuse to start silently.
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
tiler = fso.BuildPath(fso.GetParentFolderName(WScript.ScriptFullName), "tidy-terminals.vbs")
sh.Run """" & sh.ExpandEnvironmentStrings("%SystemRoot%") & "\System32\wscript.exe"" """ & tiler & """", 0, False
