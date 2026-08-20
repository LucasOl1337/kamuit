# KamuiT

A lean, native terminal workspace for running AI agents (Grok, Claude, Codex, Pi) on Windows — the successor to TerminalDE.

TerminalDE proved the workflow (tabs of agent TUIs, global summon, quick navigation, ready sounds) but hit a wall: Electron + xterm.js can't render heavy TUI redraws as fast as a native terminal. KamuiT keeps the features and swaps the engine: it embeds the **official Windows Terminal core** (ConPTY + the GPU-accelerated AtlasEngine) inside a small WPF app, so rendering performance is identical to running agents directly in Windows Terminal.

## Features

- **Agent-first** — tabs can boot straight into Grok / Claude / Codex / Pi (not only empty shells)
- **CLI + named pipe** — any agent can open tabs: `kamuit open grok -C C:\Projetos\foo -n 2`
- **MCP** — `scripts/kamuit-mcp.mjs` exposes the same ops to Grok/Claude/Codex tool use
- **Native rendering** — same engine as Windows Terminal, no Electron, no xterm.js
- **Tabs** — `Ctrl+Shift+T` new shell · **`Ctrl+Shift+G/C/D/P`** new Grok/Claude/Codex/Pi · `Ctrl+Tab` cycle · `Ctrl+Shift+W` close
- **Drag reorder** — drag a tab header to change order (`Ctrl+Shift+←/→` also moves the active tab); keyboard nav follows the visual order
- **Project pack** — `Ctrl+Shift+O` lists folders under `C:\projetos`, pick one + N tabs (1–9); each shell starts already in that folder (no triple `cd`)
- **`Ctrl+1..9`** — jump to tab N (left-to-right), creating the missing tabs on the way
- **Live tab titles** — tracks what the session announces via OSC escape sequences (e.g. Grok's "Waiting for response…", "Thinking…")
- **Quick CD** — `Alt+1` Desktop, `Alt+2` `C:\projetos`, `Alt+3` `C:\NexUnio`, `Alt+4` `C:\NexUnio\NexSales`, `Alt+5` `C:\NexUnio\sfr-resgate-digital`
- **Limbo** — `Ctrl+Shift+X` hides a tab without killing its shell; `Ctrl+Shift+L` restores. Hidden tabs cost zero rendering
- **Agent-ready sounds** — plays `SoundEffects/Terminal{N}.mp3` for the **current visual slot** of the tab that finished
  - Each session gets a stable `KAMUIT_TAB_ID`; close/reorder never mis-label the sound
  - Grok: official `Stop` hook
  - Claude: `Stop` hook in `~/.claude/settings.json`
  - Pi: extension (`~/.pi/agent/extensions/kamuit-ready.ts`)
  - All passive: no polling, no screen scraping, nothing running inside the shell
- **Global `Ctrl+Space`** — summon/hide the window from any app (auto-retries if another app holds the hotkey)
- **Single-instance** — second launch focuses the existing window (or forwards CLI args via pipe)

## Architecture

```
pwsh.exe ── ConPTY ── Windows Terminal core (AtlasEngine/DirectX) ── GPU
                ▲
                └── EasyWindowsTerminalControl (WPF wrapper, NuGet)
```

- WPF app, .NET 8, single `MainWindow` + a few services
- All tab terminals live permanently in the visual tree; switching/limbo toggles `Visibility` only (reparenting a `HwndHost` destroys the native HWND — the source of our first crash)
- Each pwsh session is born with `KAMUIT=1`, stable `KAMUIT_TAB_ID`, and initial `KAMUIT_TAB=N` (TerminalDE env sanitized). Ready hooks report `tabId`; KamuiT maps it to the current visual slot before playing the sound
- Overlay UIs (Limbo popup) are separate windows, because WPF content can't render over the terminal's `HwndHost` (airspace)

## CLI (agent control plane)

```powershell
# install once → %USERPROFILE%\.local\bin on PATH
pwsh -File scripts\install-cli.ps1

kamuit open grok
kamuit open claude -C C:\Projetos\riftbomb -n 2
kamuit open grok -C C:\Projetos -n 4
kamuit list
kamuit focus 2
kamuit type 1 "continue" -Enter
kamuit show
```

Protocol: JSON lines on named pipe `\\.\pipe\kamuit`  
Ops: `open`, `list`, `focus`, `type`, `close`, `show`, `ping`, `agents`

### MCP (for Grok / other agents)

```toml
# ~/.grok/config.toml
[mcp_servers.kamuit]
command = "node"
args = ["C:\\Projetos\\KamuiT\\scripts\\kamuit-mcp.mjs"]
```

Tools: `kamuit_open`, `kamuit_list`, `kamuit_focus`, `kamuit_type`, `kamuit_show`

## Build & run

Requires .NET 8 SDK (Windows).

```powershell
dotnet build
.\bin\Debug\net8.0-windows\KamuiT.exe
```

Ship to the Start Menu app (what Search launches):

```powershell
Stop-Process -Name KamuiT -Force -ErrorAction SilentlyContinue
dotnet publish -c Release -r win-x64 --self-contained false -o publish --nologo
pwsh -File scripts\install-shortcut.ps1   # optional
pwsh -File scripts\install-cli.ps1
```

Optional: `scripts\install-shortcut.ps1` creates a Start Menu shortcut.

## Roadmap

- Performance benchmark vs Windows Terminal (target: within ~5%)
- Codex ready-sound (needs a notify wrapper — `notify` slot is occupied by computer-use)
- Learned Quick CD slots (ranking by usage, like TerminalDE)
- Reconfigurable hotkey panel
- Auto tab labels (project folder + foreground process)
