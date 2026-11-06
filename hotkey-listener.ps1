# Background hotkey listener: registers Ctrl+Alt+T (Tidy Terminals) and Ctrl+Alt+A (Aqua Tidy)
# system-wide and runs the matching script when pressed.
# Started hidden at login by hotkey-listener.vbs (install.ps1 puts that in your Startup folder).
# Runs as a normal user and launches the tiler when a hotkey is pressed.

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

    public event Action<int> Pressed;

    public HotkeyWindow() { CreateHandle(new CreateParams()); }

    public bool Register(int id, uint mods, uint vk) {
        return RegisterHotKey(Handle, id, mods | 0x4000, vk);   // 0x4000 = MOD_NOREPEAT
    }
    public void Unregister(int id) { UnregisterHotKey(Handle, id); }

    protected override void WndProc(ref Message m) {
        if (m.Msg == 0x0312 && Pressed != null) Pressed(m.WParam.ToInt32());   // WM_HOTKEY
        base.WndProc(ref m);
    }
}
'@

$sys = [Environment]::GetFolderPath('System')
$here = $PSScriptRoot

$win = New-Object HotkeyWindow
# MOD_ALT (1) + MOD_CONTROL (2); id 1 = T (0x54), id 2 = A (0x41)
if (-not $win.Register(1, 3, 0x54)) {
    [System.Windows.Forms.MessageBox]::Show(
        'Tidy Terminals could not claim Ctrl+Alt+T. Another program is already using it.',
        'Tidy Terminals') | Out-Null
    return
}
if (-not $win.Register(2, 3, 0x41)) {
    [System.Windows.Forms.MessageBox]::Show(
        'Aqua Tidy could not claim Ctrl+Alt+A. Another program is already using it. Ctrl+Alt+T still works.',
        'Tidy Terminals') | Out-Null
}

$win.add_Pressed({
    param($id)
    $script = if ($id -eq 2) { 'aqua-tidy.vbs' } else { 'tidy-terminals.vbs' }
    Start-Process "$sys\wscript.exe" -ArgumentList ('"{0}"' -f (Join-Path $here $script)) -WindowStyle Hidden
})

[System.Windows.Forms.Application]::Run()
$win.Unregister(1)
$win.Unregister(2)
