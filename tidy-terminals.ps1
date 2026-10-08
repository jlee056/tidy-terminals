# Tidy Terminals: snaps every open terminal window into an even grid on the main monitor.
# Picks up Windows Terminal, Command Prompt and PowerShell windows. Leaves agentview alone.
# Windows opened "as administrator" can only be moved when this runs elevated (install.ps1 sets
# that up); otherwise they are skipped.
# Run it from the desktop shortcut or the Ctrl+Alt+T hotkey.

$ErrorActionPreference = 'Stop'
$gap = 4   # pixels between windows

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class Tidy {
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int ht, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern IntPtr SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] public static extern bool SystemParametersInfo(int a, int b, ref RECT r, int c);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();

    public static List<IntPtr> Terminals() {
        var found = new List<IntPtr>();
        IntPtr self = GetConsoleWindow();
        EnumWindows((h, p) => {
            if (h == self || !IsWindowVisible(h)) return true;
            var cls = new StringBuilder(256); GetClassName(h, cls, 256);
            var title = new StringBuilder(512); GetWindowText(h, title, 512);
            string c = cls.ToString(), t = title.ToString();
            bool term = c == "CASCADIA_HOSTING_WINDOW_CLASS" || c == "ConsoleWindowClass";
            // agentview windows: titled "agentview" by its launcher, or plain "python" if launched the old way
            bool agentview = t.IndexOf("agentview", StringComparison.OrdinalIgnoreCase) >= 0 || t == "python";
            // no-op move as a probe: fails on admin windows, so they don't leave a hole in the grid
            if (term && t.Length > 0 && !agentview && SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, 0x0017))
                found.Add(h);
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static RECT MainWorkArea() {
        var r = new RECT(); SystemParametersInfo(0x0030, 0, ref r, 0);  // SPI_GETWORKAREA: primary monitor minus taskbar
        return r;
    }
}
'@

[void][Tidy]::SetProcessDpiAwarenessContext([IntPtr]-4)   # per-monitor DPI aware, so pixel math is real pixels

$wins = [Tidy]::Terminals()
$n = $wins.Count
if ($n -eq 0) { return }

# Wide screen: fewer rows than columns. 2 -> 1x2, 4 -> 2x2, 6 -> 2x3, 9 -> 3x3
$rows = [Math]::Max(1, [Math]::Floor([Math]::Sqrt($n)))
$cols = [Math]::Ceiling($n / $rows)

$wa = [Tidy]::MainWorkArea()
$cellW = [Math]::Floor(($wa.R - $wa.L) / $cols)
$cellH = [Math]::Floor(($wa.B - $wa.T) / $rows)

# Oldest window first (EnumWindows gives top-most first, so reverse)
$wins.Reverse()
for ($i = 0; $i -lt $n; $i++) {
    $h = $wins[$i]
    $row = [Math]::Floor($i / $cols)
    $col = $i % $cols
    # Last row stretches its windows to fill the width when it isn't full
    $inRow = if ($row -eq $rows - 1) { $n - $row * $cols } else { $cols }
    $w = [Math]::Floor(($wa.R - $wa.L) / $inRow)

    [void][Tidy]::ShowWindow($h, 9)   # restore if minimized or maximized

    # Windows 10/11 frames have invisible borders; pad so the visible edges line up
    $outer = New-Object Tidy+RECT; $vis = New-Object Tidy+RECT
    [void][Tidy]::GetWindowRect($h, [ref]$outer)
    $padL = 0; $padT = 0; $padR = 0; $padB = 0
    if ([Tidy]::DwmGetWindowAttribute($h, 9, [ref]$vis, 16) -eq 0) {
        $padL = $vis.L - $outer.L; $padT = $vis.T - $outer.T
        $padR = $outer.R - $vis.R; $padB = $outer.B - $vis.B
    }

    $x = $wa.L + $col * $w + $gap
    $y = $wa.T + $row * $cellH + $gap
    [void][Tidy]::SetWindowPos($h, [IntPtr]::Zero,
        $x - $padL, $y - $padT,
        $w - 2 * $gap + $padL + $padR, $cellH - 2 * $gap + $padT + $padB,
        0x0044)   # SWP_NOZORDER | SWP_SHOWWINDOW
}
