# KamuiT v0.1.0 — first release

The first working build of KamuiT, the native successor to TerminalDE.

## What's inside

- **Native terminal engine** — embeds the official Windows Terminal core (ConPTY + AtlasEngine) via `EasyWindowsTerminalControl` 1.0.38. No Electron, no xterm.js.
- **Tabs** — `Ctrl+Shift+T` new · `Ctrl+Tab`/`Ctrl+Shift+Tab` cycle · `Ctrl+Shift+W` close (kills the shell, no orphans) · `Ctrl+1..9` jump-or-create
- **Live tab titles** — OSC title sequences from the session drive tab labels (Grok's "Waiting for response…" / "Thinking…" show up)
- **Quick CD** — `Alt+1` Desktop · `Alt+2` `C:\projetos`
- **Limbo** — `Ctrl+Shift+X` hides a tab without killing the shell · `Ctrl+Shift+L` popup to restore
- **Agent-ready sounds** — Grok + Claude `Stop` hooks, Pi extension; plays `SoundEffects/Terminal{N}.mp3` per tab. Passive design: no polling, no scraping, nothing inside the shell
- **Global `Ctrl+Space`** — summon/hide from anywhere, with retry while another app holds the hotkey
- **App icon** + Start Menu shortcut script

## Notable bugs slain on the way

- Black screen on startup: the wrapper doesn't initialize its palette without an explicit `TerminalTheme`
- Quick CD dead keys: WPF reports `Alt+digit` as `Key.System`, real key in `e.SystemKey`
- Ready sounds silently dropped: atomic write+rename fires `Renamed`, not `Created`, on `FileSystemWatcher` — and pre-existing signals needed a startup sweep
- "Terminal 1 ready" playing for every tab: KamuiT was launched from inside a TerminalDE tab and inherited `TERMINALDE=1` + its PTY id — sessions now sanitize foreign env vars at birth
- Crash restoring from Limbo: reparenting a `HwndHost` destroys the native HWND; all terminals now stay in the visual tree, toggling `Visibility` only
- Limbo popup self-closing instantly: WPF `Deactivated` fires because focus lives in the native terminal HWND

## Known limitations

- Sounds only exist for tabs 1–5 (`SoundEffects/Terminal1..5.mp3`)
- Tab slot numbers are fixed at creation (closing tab 2 doesn't renumber tab 3)
- Codex has no ready-sound yet (its `notify` slot is taken by computer-use)
- `Ctrl+Space` can't be held while TerminalDE (or another app) owns it — retry grabs it once free
