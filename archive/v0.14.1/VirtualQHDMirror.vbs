Option Explicit

Dim shell, fso, root, command
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

root = fso.GetParentFolderName(WScript.ScriptFullName)
command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File " & Chr(34) & _
          root & "\VirtualQHDMirror.ps1" & Chr(34) & _
          " -Mode adaptive -InputMode portal -CursorHz 120"

shell.Run command, 0, False
