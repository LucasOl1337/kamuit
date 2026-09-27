# Patch Notes — KamuiT

## KamuiT v0.3.0

27/09/2026 · Windows e Linux

O KamuiT agora roda também no Linux, e os agentes passam a abrir, controlar e organizar as próprias abas pela CLI e pelo MCP. Esta versão junta tudo que foi feito desde a v0.1.0, incluindo a 0.2.0 que nunca virou release.

### Novidades

- **KamuiT no Linux**: host nativo em GTK 4 + VTE com abas, limbo, project pack, títulos vivos e som de pronto, falando o mesmo protocolo do Windows.
- **Abas que já nascem no agente**: Grok, Claude, Codex ou Pi abrem direto na pasta do projeto, por `Ctrl+Shift+G/C/D/P` ou `kamuit open grok -C <pasta> -n 2`.
- **Controle pela linha de comando**: `kamuit open | list | focus | type | close | limbo | show | ping | agents` fala com a janela aberta pelo named pipe no Windows e pelo socket Unix no Linux.
- **Servidor MCP**: com `scripts/kamuit-mcp.mjs`, um agente usa o KamuiT como ferramenta pra abrir, listar, focar e digitar em abas.
- **Project pack**: `Ctrl+Shift+O` lista as pastas de projetos e abre N abas já dentro da escolhida.
- **Reordenar abas**: arraste o cabeçalho ou use `Ctrl+Shift+←/→`; a navegação segue a ordem visual (Windows).
- **Colar no terminal**: `Ctrl+V` cola texto com bracketed paste, e colar só imagem continua chegando na TUI (Windows).

### Melhorias

- **Uma janela só**: abrir o KamuiT de novo foca a janela que já existe e repassa o pedido, sem abrir uma segunda.
- **Som de pronto na aba certa**: cada aba tem identidade fixa, e o som acompanha a posição visual depois de arrastar, fechar ou mandar pro limbo.
- **`Ctrl+Space` mais previsível**: com o KamuiT aberto e sem foco, o atalho traz a janela pra frente em vez de esconder (Windows).
- **TUIs com cor de verdade**: as abas anunciam `TERM`, `COLORTERM` e `WT_SESSION`, então ferramentas de terminal detectam truecolor (Windows).
- **Visual escuro no Linux**: abas, diálogos e títulos no formato `agente · pasta`, fonte JetBrains Mono quando instalada.

### Correções

- **Tab volta pro terminal**: a tecla Tab chega ao PSReadLine e às TUIs em vez de escapar pra interface (Windows).
- **Atalhos com o terminal focado**: no Linux, `Ctrl+Shift+T`, `W`, `X` e os outros funcionam mesmo com o cursor dentro do VTE.
- **Fechar e esconder a aba certa**: no Linux a aba ativa ficava presa na anterior, e `Ctrl+Shift+W`, `Ctrl+Shift+X` ou `kamuit type` sem slot agiam na aba errada.

### Sistemas

- **Downloads prontos**: a release traz `KamuiT-v0.3.0-win-x64.zip` e `KamuiT-v0.3.0-linux-x64.tar.gz`, gerados pelo GitHub Actions a cada versão publicada.
- **CI nos dois sistemas**: cada push compila Windows e Linux e roda o smoke do binário Linux.
- **Instância isolada**: `KAMUIT_PROJECTS_ROOT`, `KAMUIT_AGENT_BIN_DIR` e `KAMUIT_SHELL_INIT` permitem subir um KamuiT de teste sem encostar nos agentes e no shell de verdade.

### Como instalar

- **Windows**: descompacte o zip e abra `KamuiT.exe`. Precisa do .NET 8 Desktop Runtime. `scripts\install-shortcut.ps1` cria o atalho no Menu Iniciar e `scripts\install-cli.ps1` instala o comando `kamuit`.
- **Linux**: extraia o tar.gz e rode `./KamuiT`. Precisa do .NET 8 Runtime, GTK 4 e `libvte-2.91-gtk4`. A partir do código, `bash scripts/install-linux.sh` instala dependências, atalho e CLI.

### Limitações conhecidas

- `Ctrl+Space` global existe só no Windows; no Linux os atalhos valem dentro da janela.
- Som de pronto só nas abas 1 a 5 e ainda sem suporte ao Codex.
- Abas abertas antes de atualizar continuam no formato antigo de sinal até serem reabertas.
- O binário Windows desta versão foi compilado no CI; o teste interativo foi feito no host Linux.

**Comparação completa**: https://github.com/LucasOl1337/kamuit/compare/v0.1.0...v0.3.0

---

## 2026-07-27 — v0.2.0 agent-first, CLI e controle local

**Comparação:** PC e `origin/main` partiam de `e90c367`; o checkout acrescenta 10 arquivos rastreados alterados (873 inserções, 56 remoções) e novos módulos de protocolo, catálogo de agentes, janela de projetos e scripts.  
**Branch de publicação:** `agent/2026-07-27-safe-commit`.  
**Conflitos:** nenhum após `fetch`.

### O que muda para o usuário

- Abas podem nascer diretamente em Grok, Claude, Codex, Pi ou shell, já no diretório do projeto.
- Uma CLI local e um servidor named-pipe `\\.\pipe\kamuit` permitem abrir, listar, focar, digitar e fechar abas sem automação frágil de mouse.
- A segunda abertura do executável encaminha a solicitação para a instância existente, em vez de criar duas janelas.
- Abas ganham identidade estável por GUID; o som de conclusão acompanha a posição visual após reordenação, fechamento ou limbo.
- Cabeçalhos podem ser arrastados para reordenar e o seletor de projetos abre packs de abas em `C:\projetos`.
- O ambiente anuncia compatibilidade de terminal moderno (`TERM`, `COLORTERM`, `WT_SESSION`) e instala o init opcional em `~/.kamuit`.

### Empacotamento seguro

O projeto sobe para a versão `0.2.0` e inclui scripts necessários no output. A pasta experimental `publish-new/` e mapas de jornada em `output/` foram preservados localmente, porém ignorados: contêm binários e PDB gerados (incluindo um PDB de aproximadamente 81 MB), não fonte. A aplicação oficial continua sendo publicada em `publish/`, conforme as regras do projeto.

### Compatibilidade

O protocolo antigo por número de aba continua aceito como fallback. Chamadas novas devem preferir `tabId`, evitando tocar o som na aba errada depois de reordenação.

---

## KamuiT v0.2.0 — agent-first + CLI/MCP

- **Agent-first tabs** — open a tab already running Grok / Claude / Codex / Pi (shell still available)
- **Named pipe IPC** — `\\.\pipe\kamuit` JSON-line protocol (`open`, `list`, `focus`, `type`, `close`, `show`, `ping`, `agents`)
- **CLI** — `scripts/kamuit.ps1` + `install-cli.ps1` → `kamuit open grok -C <path> -n 2`
- **MCP** — `scripts/kamuit-mcp.mjs` for agent tool use (Grok config.toml snippet in README)
- **Hotkeys** — `Ctrl+Shift+G/C/D/P` → new Grok / Claude / Codex / Pi tab
- **Single-instance** — second `KamuiT.exe` focuses the live window / forwards args
- Env on agent tabs: `KAMUIT_AGENT=grok|claude|…`

# KamuiT v0.1.0 — first release

The first working build of KamuiT, the native successor to TerminalDE.

## What's inside

- **Native terminal engine** — embeds the official Windows Terminal core (ConPTY + AtlasEngine) via `EasyWindowsTerminalControl` 1.0.38. No Electron, no xterm.js.
- **Tabs** — `Ctrl+Shift+T` new · `Ctrl+Tab`/`Ctrl+Shift+Tab` cycle · `Ctrl+Shift+W` close (kills the shell, no orphans) · `Ctrl+1..9` jump-or-create
- **Live tab titles** — OSC title sequences from the session drive tab labels (Grok's "Waiting for response…" / "Thinking…" show up)
- **Quick CD** — `Alt+1` Desktop · `Alt+2` `C:\projetos` · `Alt+3` `C:\NexUnio` · `Alt+4` NexSales · `Alt+5` sfr-resgate-digital
- **Limbo** — `Ctrl+Shift+X` hides a tab without killing the shell · `Ctrl+Shift+L` popup to restore
- **Agent-ready sounds** — Grok + Claude `Stop` hooks, Pi extension; plays `SoundEffects/Terminal{N}.mp3` per tab. Passive design: no polling, no scraping, nothing inside the shell
- **Global `Ctrl+Space`** — summon/hide from anywhere, with retry while another app holds the hotkey
  - If KamuiT is open but not focused → bring to front (don't hide)
  - If already focused → hide; if closed/minimized → show
- **Terminal paste** — `Ctrl+V` pastes clipboard text with bracketed-paste markers; image-only clipboards still reach the foreground TUI
- **App icon** + Start Menu shortcut script

## Notable bugs slain on the way

- Black screen on startup: the wrapper doesn't initialize its palette without an explicit `TerminalTheme`
- Quick CD dead keys: WPF reports `Alt+digit` as `Key.System`, real key in `e.SystemKey`
- Ready sounds silently dropped: atomic write+rename fires `Renamed`, not `Created`, on `FileSystemWatcher` — and pre-existing signals needed a startup sweep
- "Terminal 1 ready" playing for every tab: KamuiT was launched from inside a TerminalDE tab and inherited `TERMINALDE=1` + its PTY id — sessions now sanitize foreign env vars at birth
- Crash restoring from Limbo: reparenting a `HwndHost` destroys the native HWND; all terminals now stay in the visual tree, toggling `Visibility` only
- Limbo popup self-closing instantly: WPF `Deactivated` fires because focus lives in the native terminal HWND

## v0.1.2 — project pack

- **Ctrl+Shift+O** — “Abrir projeto”: escolhe pasta em `C:\projetos` + quantas abas (1–9, default 3). Cada aba já nasce no folder (sem `cd` 3x)

## v0.1.1 — tab drag + ready-sound + Tab completion

- **Tab autofill** — `Tab` preenche o texto (previsão do histórico se houver; senão próximo match). Sem menu. WPF não engole a tecla; ConPTY recebe `\t`
- **Drag reorder** — arraste o header da aba; `Ctrl+Shift+←/→` move a aba ativa; nav segue ordem visual
- **Ready-sound fix** — `KAMUIT_TAB_ID` estável; som = posição visual atual (não o slot de criação)
- Signal schema v2: `{ tabId, tab? }`

## Known limitations

- Sounds only exist for tabs 1–5 (`SoundEffects/Terminal1..5.mp3`)
- Codex has no ready-sound yet (its `notify` slot is taken by computer-use)
- `Ctrl+Space` can't be held while TerminalDE (or another app) owns it — retry grabs it once free
- Sessões abertas **antes** do update ainda usam só `KAMUIT_TAB` legado até reabrir a aba
