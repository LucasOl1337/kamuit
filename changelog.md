# Changelog — KamuiT

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).

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
