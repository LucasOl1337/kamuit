# KamuiT — agent rules

## Ship to the Windows Start Menu app (mandatory)

The app the user opens via **Windows Search / Start Menu** is:

- Shortcut: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\KamuiT.lnk`
- Target: `C:\Projetos\KamuiT\publish\KamuiT.exe`

**A plain `dotnet build` does NOT update that app.** Debug/Release under `bin\` are not what Search launches.

### After any code or asset change that should be usable

1. If KamuiT is running, stop it so files are not locked:
   ```powershell
   Stop-Process -Name KamuiT -Force -ErrorAction SilentlyContinue
   ```
2. Publish into `publish\`:
   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained false -o publish --nologo
   ```
3. Optional — if the Start Menu entry is missing or points elsewhere:
   ```powershell
   pwsh -File scripts\install-shortcut.ps1
   ```

### Do not stop at Debug-only builds

- Building `bin\Debug\...` is fine for compile checks mid-task.
- Before telling the user the app is ready / “abre o KamuiT”, **publish to `publish\`** so Search opens the new bits.
- Never claim “já está no app do Windows” after only a Debug build.

### What “done” means here

Done = `publish\KamuiT.exe` (and sibling DLLs/scripts/sounds) updated with the change, not only sources or `bin\Debug`.

## Linux host (GTK 4 + VTE)

Publish: `dotnet publish linux/KamuiT.Linux.csproj -c Release -r linux-x64 --self-contained false -o publish-linux --nologo`

Socket: `$XDG_RUNTIME_DIR/kamuit.sock` (Unix domain, JSON-lines). CLI: `scripts/kamuit.sh`.

To run a bench-local instance without touching the human session or `~/.grok` agents:

- `KAMUIT_PROJECTS_ROOT` — project pack / initial cwd (need not exist beforehand)
- `KAMUIT_AGENT_BIN_DIR` — overrides grok/claude/codex/pi/jcode resolution
- `KAMUIT_SHELL_INIT` — bash rcfile instead of copying `~/.kamuit/kamuit-shell-init.sh`

## Maintaining this file

Keep this file for knowledge useful to almost every future agent session in this project.
Do not repeat what the codebase already shows; point to the authoritative file or command instead.
Prefer rewriting or pruning existing entries over appending new ones.
When updating this file, preserve this bar for all agents and keep entries concise.