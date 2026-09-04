# Relatório — porte Linux do KamuiT

Data: 2026-09-04  
Ramo: `agent/2026-07-27-safe-commit`  
Repo: https://github.com/LucasOl1337/kamuit

## Aceite

| Critério | Estado |
| --- | --- |
| (1) GitHub mostra suporte Windows e Linux | Feito no repositório: descrição, tópicos `windows`/`linux`, README com tabela e badges, CI dual. O README da home só aparece em `main` depois do push desse conteúdo. |
| (2) Código do suporte Linux implementado | Feito: host GTK4 + VTE em `linux/`. |
| (3) Relatório honesto do que não deu para verificar | Este arquivo. |

## O que foi feito

Windows continua WPF + Windows Terminal core. Linux é um host separado (não é WPF no Linux — WPF não roda lá).

- `linux/KamuiT.Linux.csproj` — GTK 4 (GirCore) + P/Invoke de `libvte-2.91-gtk4` (fallback `libvte-2.91`)
- Protocolo compartilhado: `KamuiProtocol.cs`, `CommandServer.cs`, `AgentCatalog.cs`
- IPC Linux: socket `$XDG_RUNTIME_DIR/kamuit.sock`
- CLI: `scripts/kamuit.sh` · instalador: `scripts/install-linux.sh`
- MCP: `scripts/kamuit-mcp.mjs` fala no socket quando não é Windows
- CI: `.github/workflows/ci.yml` (windows-latest + ubuntu-latest)

Descrição GitHub: `Terminal workspace for AI agents on Windows and Linux — WT core on Windows, VTE on Linux`  
Tópicos: `windows`, `linux`, `terminal`, `dotnet`, `gtk`, `wpf`

## Verificado neste PC (Windows)

- `dotnet build` KamuiT.csproj (WPF) — ok
- `dotnet build` linux/KamuiT.Linux.csproj — ok (código gerenciado)
- `dotnet publish -r win-x64 -o publish` — ok
- `dotnet publish -r linux-x64 -o publish-linux` — ok (cross-compile a partir do Windows)
- `KamuiT.exe --help` e `--version` do host Linux (binário win do TFM net8.0) — ok, sem abrir GTK

## Não verificado — não inventar resultado

- **Máquina Linux do OMART:** não está neste PC. Nenhum teste gráfico, VTE, socket, agente ou som foi rodado lá.
- **Runtime GTK/VTE:** este Windows não tem `libgtk-4` / `libvte`. O host Linux não foi aberto como GUI aqui.
- **GitHub Actions ubuntu-latest:** o workflow foi commitado; o resultado do job só existe depois do push. Não afirmar verde sem o log.
- **Hotkey global Ctrl+Space:** só Windows (Win32). No Linux não está implementado (Wayland não tem equivalente simples).
- **Distro do OMART:** o instalador tenta apt/dnf/pacman. Se o VTE gtk4 não existir, cai no VTE 2.91; se o soname for outro, o load falha com mensagem pedindo `scripts/install-linux.sh`.
- **Sons no Linux:** tentam ffplay/mpg123/gst-play/paplay/pw-play. Sem um desses, o ready-sound fica mudo.
- **Agentes no PATH do OMART:** catálogo procura `~/.local/bin` e PATH. Não confirmei se grok/claude/codex/pi estão instalados lá.

## Como rodar no OMART (quando a máquina existir)

```bash
git clone https://github.com/LucasOl1337/kamuit.git
cd kamuit
git checkout agent/2026-07-27-safe-commit
# .NET 8 SDK + GTK4 + VTE
bash scripts/install-linux.sh
KamuiT
# ou
kamuit open grok
```

`--help` / `--version` não precisam de display. A janela precisa de sessão gráfica (X11 ou Wayland).
