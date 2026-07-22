# KamuiT

A lean, native terminal workspace for running AI agents (Grok, Claude, Codex, Pi) on Windows — the successor to TerminalDE.

TerminalDE proved the workflow (tabs of agent TUIs, global summon, quick navigation, ready sounds) but hit a wall: Electron + xterm.js can't render heavy TUI redraws as fast as a native terminal. KamuiT keeps the features and swaps the engine: it embeds the **official Windows Terminal core** (ConPTY + the GPU-accelerated AtlasEngine) inside a small WPF app, so rendering performance is identical to running agents directly in Windows Terminal.

## Features

- **Native rendering** — same engine as Windows Terminal, no Electron, no xterm.js, no IPC
- **Tabs** — `Ctrl+Shift+T` new, `Ctrl+Tab` / `Ctrl+Shift+Tab` cycle, `Ctrl+Shift+W` close (kills the shell, no leaked processes)
- **`Ctrl+1..9`** — jump to tab N, creating the missing tabs on the way
- **Live tab titles** — tracks what the session announces via OSC escape sequences (e.g. Grok's "Waiting for response…", "Thinking…")
- **Quick CD** — `Alt+1` Desktop, `Alt+2` `C:\projetos`
- **Limbo** — `Ctrl+Shift+X` hides a tab without killing its shell; `Ctrl+Shift+L` restores. Hidden tabs cost zero rendering
- **Agent-ready sounds** — plays `SoundEffects/Terminal{N}.mp3` when an agent finishes its turn in tab N
  - Grok: official `Stop` hook
  - Claude: `Stop` hook in `~/.claude/settings.json`
  - Pi: extension (`~/.pi/agent/extensions/kamuit-ready.ts`)
  - All passive: no polling, no screen scraping, nothing running inside the shell
- **Global `Ctrl+Space`** — summon/hide the window from any app (auto-retries if another app holds the hotkey)

## Architecture

```
pwsh.exe ── ConPTY ── Windows Terminal core (AtlasEngine/DirectX) ── GPU
                ▲
                └── EasyWindowsTerminalControl (WPF wrapper, NuGet)
```

- WPF app, .NET 8, single `MainWindow` + a few services
- All tab terminals live permanently in the visual tree; switching/limbo toggles `Visibility` only (reparenting a `HwndHost` destroys the native HWND — the source of our first crash)
- Each pwsh session is born with `KAMUIT=1` / `KAMUIT_TAB=N` (and TerminalDE env vars sanitized) so agent hooks know which tab sound to play
- Overlay UIs (Limbo popup) are separate windows, because WPF content can't render over the terminal's `HwndHost` (airspace)

## Build & run

Requires .NET 8 SDK (Windows).

```powershell
dotnet build
.\bin\Debug\net8.0-windows\KamuiT.exe
```

Optional: `scripts\install-shortcut.ps1` creates a Start Menu shortcut.

## Roadmap

- Performance benchmark vs Windows Terminal (target: within ~5%)
- Codex ready-sound (needs a notify wrapper — `notify` slot is occupied by computer-use)
- Learned Quick CD slots (ranking by usage, like TerminalDE)
- Reconfigurable hotkey panel
- Auto tab labels (project folder + foreground process)
