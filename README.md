# KamuiT

[![CI](https://github.com/LucasOl1337/kamuit/actions/workflows/ci.yml/badge.svg)](https://github.com/LucasOl1337/kamuit/actions/workflows/ci.yml)
![Windows](https://img.shields.io/badge/Windows-supported-0e7a0d)
![Linux](https://img.shields.io/badge/Linux-supported-0e7a0d)
![.NET 8](https://img.shields.io/badge/.NET-8.0-512bd4)

Workspace nativo de terminais para agentes de IA (Grok, Claude, Codex, Pi) — sucessor do TerminalDE.

| Sistema | UI | Motor de terminal | Shell padrão |
| --- | --- | --- | --- |
| **Windows** | WPF | Windows Terminal core (ConPTY + AtlasEngine) | PowerShell (`pwsh`) |
| **Linux** | GTK 4 | VTE (`libvte-2.91-gtk4`) | `$SHELL` (bash/zsh/…) |

O app no Windows continua o mesmo: WPF + `EasyWindowsTerminalControl`. No Linux o host é outro binário (`linux/KamuiT.Linux.csproj`) com o mesmo protocolo CLI/MCP (JSON-lines). Destino de runtime Linux: máquina OMART.

## Features

- **Agent-first** — abas podem nascer já no Grok / Claude / Codex / Pi
- **CLI + IPC** — `kamuit open grok -C <pasta> -n 2`
  - Windows: named pipe `\\.\pipe\kamuit`
  - Linux: socket `$XDG_RUNTIME_DIR/kamuit.sock`
- **MCP** — `scripts/kamuit-mcp.mjs` (Windows via pwsh; Linux fala direto no socket)
- **Abas** — `Ctrl+Shift+T` shell · `Ctrl+Shift+G/C/D/P` Grok/Claude/Codex/Pi · `Ctrl+Tab` ciclo · `Ctrl+Shift+W` fecha
- **Project pack** — `Ctrl+Shift+O` lista pastas do root de projetos e abre N abas já no folder
- **`Ctrl+1..9`** — salta para a aba N
- **Títulos vivos** — OSC no Windows; `window-title-changed` do VTE no Linux
- **Quick CD** — `Alt+1` Desktop, `Alt+2` projetos, `Alt+3..5` atalhos NexUnio se existirem
- **Limbo** — `Ctrl+Shift+X` esconde sem matar o PTY; `Ctrl+Shift+L` restaura
- **Sons de ready** — `SoundEffects/Terminal{N}.mp3` no slot visual (Windows: MediaPlayer; Linux: ffplay/mpg123/gst-play/paplay)
- **Single-instance** — segunda abertura encaminha args para a janela viva
- **Global `Ctrl+Space`** — só Windows (Win32 `RegisterHotKey`). No Linux o atalho in-app funciona; hotkey global de compositor (Wayland) não está implementado.

## Architecture

**Windows**

```
pwsh.exe ── ConPTY ── Windows Terminal core (AtlasEngine/DirectX) ── GPU
                ▲
                └── EasyWindowsTerminalControl (WPF wrapper, NuGet)
```

**Linux**

```
$SHELL ── PTY ── VTE (libvte-2.91-gtk4) ── GTK 4
                ▲
                └── linux/ (GirCore.Gtk-4.0 + P/Invoke VTE)
```

Protocolo compartilhado: `KamuiProtocol.cs`, `CommandServer.cs`, `AgentCatalog.cs`.

## CLI

Windows (PowerShell):

```powershell
pwsh -File scripts\install-cli.ps1
kamuit open grok
kamuit open claude -C C:\Projetos\riftbomb -n 2
kamuit list
```

Linux:

```bash
bash scripts/install-linux.sh
kamuit open grok
kamuit open claude -C "$HOME/projetos/riftbomb" -n 2
kamuit list
```

Ops: `open`, `list`, `focus`, `type`, `close`, `show`, `ping`, `agents`

### MCP

Windows (`~/.grok/config.toml`):

```toml
[mcp_servers.kamuit]
command = "node"
args = ["C:\\Projetos\\KamuiT\\scripts\\kamuit-mcp.mjs"]
```

Linux:

```toml
[mcp_servers.kamuit]
command = "node"
args = ["/caminho/para/KamuiT/scripts/kamuit-mcp.mjs"]
```

## Build

### Windows

Requer .NET 8 SDK (Windows).

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o publish --nologo
```

O atalho do Start Menu aponta para `publish\KamuiT.exe` — `dotnet build` sozinho não atualiza o app que o Search abre.

### Linux

Requer .NET 8 SDK, GTK 4 e VTE gtk4 (ou VTE 2.91).

```bash
# Debian/Ubuntu
sudo apt-get install -y libgtk-4-1 libvte-2.91-gtk4-0
# Fedora
# sudo dnf install -y gtk4 vte291-gtk4

dotnet publish linux/KamuiT.Linux.csproj -c Release -r linux-x64 --self-contained false -o publish-linux --nologo
./publish-linux/KamuiT
```

Ou o instalador: `bash scripts/install-linux.sh` (publica em `~/.local/share/kamuit` e liga `kamuit` em `~/.local/bin`).

`--help` e `--version` não abrem janela (úteis em CI sem display).

## Roadmap

- Verificar o host Linux na máquina OMART (ainda não ligada a este PC)
- Hotkey global no Linux (X11 / portal do compositor)
- Performance benchmark vs Windows Terminal (target: within ~5%)
- Codex ready-sound
- Learned Quick CD slots
- Reconfigurable hotkey panel
