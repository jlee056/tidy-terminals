# Tidy Terminals

One click (or **Ctrl+Alt+T**) snaps your regular terminal windows into an even grid on your main monitor, however many are open.

Windows Snap only offers a few fixed layouts. If you run a pile of Claude Code, PowerShell or cmd windows, resizing them by hand gets old fast. This does it in one keypress, with no learning curve.

## What it does

- Finds every **Windows Terminal**, **Command Prompt** and **PowerShell** window that the current user can move.
- Tiles them into a grid sized to the count: 2 side by side, 4 in 2x2, 6 in 2x3, 9 in 3x3.
- A partly filled last row stretches to the full width.
- Restores minimized or maximized windows first.
- Accounts for Windows' invisible window borders so edges line up cleanly.
- Skips any window titled `agentview` (see [Customizing](#customizing)).
- Runs hidden: no console flash.

## Requirements

- Windows 10 or 11
- Windows PowerShell 5.1 (built in)

## Install

1. Clone or download this repo somewhere permanent (the shortcut points at this folder).
2. Open a normal PowerShell window, then:

   ```powershell
   cd path\to\tidy-terminals
   powershell -ExecutionPolicy Bypass -File .\install.ps1
   ```

This puts a **Tidy Terminals** shortcut on your Desktop and adds a background listener at login for the **Ctrl+Alt+T** hotkey. No administrator approval is needed for a new installation. Upgrading from an older version may require removing its elevated scheduled task; the installer will warn you if it cannot do that.

The hotkey listener starts at your next sign-in. To start it right away, double-click `hotkey-listener.vbs` from Explorer.

Run it again after opening more terminals.

### Administrator windows

Windows only lets an elevated program move elevated windows. The regular shortcut and hotkey run without elevation, so they skip terminals opened "as administrator." If you need to tile those too, run the script from an elevated PowerShell window:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tidy-terminals.ps1
```

## Files

| File | Purpose |
| --- | --- |
| `tidy-terminals.ps1` | The grid logic (Win32 calls via `Add-Type`) |
| `tidy-terminals.vbs` | Runs the script with no visible window |
| `run-tidy-task.vbs` | Legacy-named launcher used by existing Desktop shortcuts; starts the tiler directly |
| `hotkey-listener.ps1` / `.vbs` | Hidden background listener that registers Ctrl+Alt+T via the Windows hotkey API |
| `install.ps1` | Creates the Desktop shortcut and adds the listener to Startup |

## Customizing

Edit the top of `tidy-terminals.ps1`:

- `$gap` sets the pixel gap between windows (default 4).
- The `agentview` check in the C# block excludes windows you want left alone. Change the title match to any window you don't want moved.

It tiles on the **primary** monitor's work area (screen minus taskbar).

## Troubleshooting

If another program already owns Ctrl+Alt+T, the listener shows a message saying so. Change the key in `hotkey-listener.ps1` (the `0x54` is the virtual-key code for T), then restart the listener or sign out and back in.

## Uninstall

```powershell
Remove-Item ([IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'Tidy Terminals.lnk'))
```

Then delete the `Tidy Terminals Hotkey` shortcut in your Startup folder (`shell:startup`), end any `powershell` process running `hotkey-listener.ps1`, and delete this folder. Older installations may also have a scheduled task named `Tidy Terminals`; remove it from an elevated PowerShell window if present.

## License

[MIT](LICENSE)
