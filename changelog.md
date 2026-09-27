# Changelog — KamuiT

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).

## [0.3.0] — 2026-09-27 — porte Linux

Primeira versão publicada desde a v0.1.0: reúne o trabalho da 0.2.0 (nunca lançada como release) e o porte Linux.

### Added

- Op `limbo` no protocolo e na CLI Linux (esconde a aba sem matar o PTY).
- `KAMUIT_PROJECTS_ROOT`, `KAMUIT_AGENT_BIN_DIR`, `KAMUIT_SHELL_INIT` pra rodar uma instância isolada (demo, teste, bancada).
- Workflow `release.yml`: anexa `KamuiT-vX.Y.Z-win-x64.zip` e `KamuiT-vX.Y.Z-linux-x64.tar.gz` na GitHub Release.
- Host Linux (`linux/KamuiT.Linux.csproj`): GTK 4 + VTE, abas, limbo, project pack, CLI/MCP, sons de ready.
- Socket POSIX `$XDG_RUNTIME_DIR/kamuit.sock` (mesmo JSON-lines do named pipe Windows).
- `scripts/kamuit.sh`, `scripts/kamuit-shell-init.sh`, `scripts/install-linux.sh`.
- CI GitHub Actions em `windows-latest` e `ubuntu-latest`.
- README e tópicos do repositório passam a declarar Windows e Linux.

### Changed

- `CommandServer` deixa de depender do WPF Dispatcher; o marshal vai para a UI thread via delegate.
- `AgentCatalog` resolve executáveis também em `~/.local/bin` / PATH no Linux.
- `KamuiRequest.ParseCli` é a CLI compartilhada.
- `CommandServer` no Linux usa Unix domain socket de verdade (não NamedPipe).
- Tema escuro GTK nas abas, diálogos e títulos (`agente · pasta`); atalhos em fase Capture pra valer com o VTE focado.
- Versão anunciada (`TERM_PROGRAM_VERSION`, MCP `serverInfo`) alinhada em 0.3.0 nos dois hosts.

### Fixed

- Linux: a aba ativa ficava presa na anterior (o `switch-page` do GTK chega antes da troca), então `Ctrl+Shift+W`/`Ctrl+Shift+X` e `type` sem slot agiam na aba errada.

### Removed

- `package.json` do Maestri, commitado por engano.

### Validation

- 04/09: compilação Windows e Linux no PC Windows.
- 27/09: host Linux aberto numa bancada do Omarchy (GTK 4 + VTE): socket, `open`, `type`, `focus`, `limbo`, `Ctrl+Shift+T` e `Ctrl+Shift+X` com o terminal focado.

## [0.2.0] — 2026-07-27 — agent-first safe commit

### Added

- `AgentCatalog.cs`: aliases, descoberta de executáveis e lançamento seguro de Grok, Claude, Codex e Pi.
- `CommandServer.cs` e `KamuiProtocol.cs`: protocolo JSON-lines por named pipe, cliente local e respostas estruturadas.
- `ProjectPackWindow.cs`: seleção de projeto e criação de múltiplas abas com diretório/agente definidos.
- CLI PowerShell/CMD, servidor MCP e scripts de inicialização, instalação e sinal de prontidão.
- Single-instance com encaminhamento de argumentos para a janela viva.
- Reordenação de abas por arraste, identidade estável e recálculo de slots visuais.

### Changed

- `MainWindow` passa a criar abas agent-first, aceitar comandos externos e expor metadados de cwd, agente, slot e limbo.
- `AgentReadyService` resolve `tabId` na UI e mantém o índice antigo apenas como fallback.
- `KamuiT.csproj` sobe para `0.2.0` e copia scripts de runtime para o output.
- Atalhos e documentação foram ampliados para CLI, MCP e hotkeys de agentes.

### Fixed

- A tecla Tab volta a chegar ao PSReadLine/TUI, sem escapar para a interface WPF.
- Uma segunda instância deixa de competir pela janela e pelo terminal.
- O som de conclusão não usa mais um slot obsoleto depois de drag/close/limbo.

### Excluded from version control

- `publish-new/` e `output/`: builds, símbolos, DLLs e artefatos de inspeção gerados localmente.

### Repository state

- Base auditada: `origin/main@e90c367`; zero conflito e zero commit remoto divergente no início do snapshot.

### Validation

- `dotnet publish -c Release -r win-x64 --self-contained false -o publish --nologo`: aprovado e aplicado ao executável usado pelo atalho do Windows.
