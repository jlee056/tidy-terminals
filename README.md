# Tidy Terminals

One click (or **Ctrl+Alt+T**) snaps every open terminal window into an even grid on your main monitor, however many are open.

Windows Snap only offers a few fixed layouts. If you run a pile of Claude Code, PowerShell or cmd windows, resizing them by hand gets old fast. This does it in one keypress, with no learning curve.

## What it does

- Finds every **Windows Terminal**, **Command Prompt** and **PowerShell** window.
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
2. Open PowerShell **as administrator**, then:

   ```powershell
   cd path\to\tidy-terminals
   powershell -ExecutionPolicy Bypass -File .\install.ps1
   ```

This registers a scheduled task named `Tidy Terminals` and puts a **Tidy Terminals** shortcut on your Desktop with the hotkey Ctrl+Alt+T. Admin approval is needed only this once.

Run it again after opening more terminals.

### Why a scheduled task?

Windows only lets an elevated program move elevated windows. The task runs with highest privileges, so terminals you opened "as administrator" join the grid too. Starting a task from the shortcut needs no UAC prompt.

### No admin / no install

Skip the installer and run the script directly. Admin-opened terminals will just be skipped:

```powershell
powershell -ExecutionPolicy Bypass -File .\tidy-terminals.ps1
```

## Files

| File | Purpose |
| --- | --- |
| `tidy-terminals.ps1` | The grid logic (Win32 calls via `Add-Type`) |
| `tidy-terminals.vbs` | Runs the script with no visible window |
| `run-tidy-task.vbs` | What the shortcut runs; starts the elevated scheduled task |
| `install.ps1` | Registers the task and creates the Desktop shortcut |

## Customizing

Edit the top of `tidy-terminals.ps1`:

- `$gap` sets the pixel gap between windows (default 4).
- The `agentview` check in the C# block excludes windows you want left alone. Change the title match to any window you don't want moved.

It tiles on the **primary** monitor's work area (screen minus taskbar).

## Uninstall

```powershell
Unregister-ScheduledTask -TaskName 'Tidy Terminals' -Confirm:$false
```

Then delete the Desktop shortcut and this folder.

## License

[MIT](LICENSE)
