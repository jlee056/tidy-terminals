# Background hotkey listener: registers Ctrl+Alt+T system-wide and runs Tidy Terminals when pressed.
# Started hidden at login by hotkey-listener.vbs (install.ps1 puts that in your Startup folder).
# Runs as a normal user; it only starts the elevated scheduled task, it doesn't tidy anything itself.

$ErrorActionPreference = 'Stop'

# One listener at a time
$created = $false
$mutex = New-Object System.Threading.Mutex($true, 'Local\TidyTerminalsHotkey', [ref]$created)
if (-not $created) { return }

Add-Type -AssemblyName System.Windows.Forms
Add-Type -ReferencedAssemblies System.Windows.Forms -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class HotkeyWindow : NativeWindow {
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);

    public event Action Pressed;

    public bool Register(uint mods, uint vk) {
        CreateHandle(new CreateParams());
        return RegisterHotKey(Handle, 1, mods | 0x4000, vk);   // 0x4000 = MOD_NOREPEAT
    }
    public void Unregister() { UnregisterHotKey(Handle, 1); }

    protected override void WndProc(ref Message m) {
        if (m.Msg == 0x0312 && Pressed != null) Pressed();   // WM_HOTKEY
        base.WndProc(ref m);
    }
}
'@

$sys = [Environment]::GetFolderPath('System')
$here = $PSScriptRoot

$win = New-Object HotkeyWindow
# MOD_ALT (1) + MOD_CONTROL (2), virtual key T (0x54)
if (-not $win.Register(3, 0x54)) {
    [System.Windows.Forms.MessageBox]::Show(
        'Tidy Terminals could not claim Ctrl+Alt+T. Another program is already using it.',
        'Tidy Terminals') | Out-Null
    return
}

$win.add_Pressed({
    # Prefer the elevated scheduled task (can move admin windows); fall back to a plain run
    & "$sys\schtasks.exe" /run /tn 'Tidy Terminals' *> $null
    if ($LASTEXITCODE -ne 0) {
        Start-Process "$sys\wscript.exe" -ArgumentList ('"{0}"' -f (Join-Path $here 'tidy-terminals.vbs'))
    }
})

[System.Windows.Forms.Application]::Run()
$win.Unregister()
