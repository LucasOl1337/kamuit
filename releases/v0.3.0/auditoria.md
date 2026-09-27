# Auditoria da release v0.3.0

## Base e candidato

- Base: `v0.1.0` = `53ccb58` (última GitHub Release, 22/07/2026, asset `KamuiT-v0.1.0-win-x64.zip`).
- `main` antes da release: `e2b50fc`, em dia com `origin/main`, árvore limpa.
- A 0.2.0 (27/07) e a 0.3.0 de 04/09 existiam no changelog e no csproj, mas nunca viraram tag nem release. Esta release cobre tudo desde a v0.1.0.
- Integrado de `origin/fm/kamuit-vitrine-x1` (PR #2, `1772408`): só o código do host (socket Unix, op `limbo`, atalhos em Capture, tema, variáveis de isolamento, AGENTS.md/CLAUDE.md). A landing em `docs/` e o vídeo ficaram no PR #2, que pede merge explícito pra Pages.

## Commits no candidato

| SHA | O quê |
| --- | --- |
| e90c367 | ignora publish/ e zips |
| 7dab85c | 0.2.0 agent-first: catálogo, pipe, CLI, MCP, pack, single-instance, drag, fix do Tab (PR #1) |
| e63abd6 | catálogo, MCP, CLI e MainWindow (inclui `package.json` do Maestri por engano) |
| 1c2a0e2 | host Linux GTK 4 + VTE, CI dupla |
| e2b50fc | relatório do porte |
| ee4c3ca | código do host Linux vindo do PR #2 |
| f678cdd | fix: aba ativa no Linux (switch-page chega antes da troca) |
| release | versão alinhada, workflow de assets, notas, arte, remove `package.json` |

## Matriz de mudanças

| Item | Evidência | Destino |
| --- | --- | --- |
| Host Linux GTK 4 + VTE | 1c2a0e2, ee4c3ca | Notas: Novidades |
| Abas agent-first, hotkeys G/C/D/P | 7dab85c, e63abd6 | Notas |
| CLI + IPC (pipe/socket), op limbo | 7dab85c, 1c2a0e2, ee4c3ca | Notas |
| MCP | 7dab85c, e63abd6 | Notas |
| Project pack | 7dab85c | Notas |
| Drag reorder (só Windows) | 7dab85c | Notas |
| Ctrl+V bracketed paste (Windows) | diff v0.1.0..HEAD MainWindow | Notas |
| Single-instance, som por tabId, Ctrl+Space, TERM/COLORTERM | 7dab85c, e63abd6 | Notas: Melhorias |
| Tema escuro Linux | ee4c3ca | Notas |
| Tab no PSReadLine | 7dab85c | Notas: Correções |
| Atalhos em Capture (Linux) | ee4c3ca | Notas |
| Aba ativa presa na anterior (Linux) | f678cdd, achado nesta release | Notas |
| Workflow `release.yml` + CI dupla | release, 1c2a0e2 | Notas: Sistemas |
| Variáveis de isolamento | ee4c3ca | Notas: Sistemas |
| Refactor CommandServer (HandleStream) | ee4c3ca | Só auditoria |
| `package.json` do Maestri removido | e63abd6 → release | Só changelog/auditoria |
| Landing `docs/` + vídeo | PR #2 | Excluído (hold do PR) |

## Cobertura de sessões

- Claude: encontrado. `79815f0f` (24/09) cita o SHA `1772408` na triagem de worktrees; a sessão que gravou a vitrine (15/09, bancada `kamuit-promo`) não foi isolada com certeza. Esta release: sessão atual.
- Codex: sessões de 15 a 25/09 citam `kamuit-vitrine-x1`/`kamuit-promo`; correlação com commits não confirmada (confiança baixa).
- Grok: sessões citam o KamuiT e o PR, sem prova de autoria dos commits.
- Hermes: nenhuma sessão relacionada nas fontes consultadas (`~/.hermes`).
- Pi: nenhuma sessão relacionada em `~/.pi`.
- Orca: fonte indisponível (sem diretório de histórico nesta máquina).
- Commits de 22/07 a 04/09 foram feitos no PC Windows (relatório do porte); histórico daquela máquina não está acessível daqui. Atribuição: agente não identificado.

## Validações

- `dotnet publish linux/KamuiT.Linux.csproj -r linux-x64` (Omarchy, SDK 8.0.130): ok. `KamuiT --version` → `KamuiT 0.3.0 (linux)`.
- `dotnet publish KamuiT.csproj -r win-x64` local: indisponível (SDK do Arch sem WindowsDesktop). Coberto pelo CI `windows-latest` e pelo workflow de release.
- Teste interativo na bancada `release-kamuit` com HOME, XDG_RUNTIME_DIR e projetos isolados: `ping`, `open -n 2`, `list`, `type --enter`, `focus`, `limbo`, `Ctrl+Shift+T` e `Ctrl+Shift+X` com o VTE focado. O bug da aba ativa apareceu aqui (lista marcava a aba anterior) e foi corrigido em f678cdd; retestado ok.
- `node --check scripts/kamuit-mcp.mjs`, `bash -n scripts/kamuit.sh`: ok.
- CI do PR #2 (Windows + Linux): verde em 15/09.

## Pendências e riscos

- PR #2 continua aberto com a landing (`docs/`) e o vídeo; depois desta release o diff dele fica só com a vitrine.
- Binário Windows não foi aberto interativamente nesta rodada.
- Instalação local do Lucas em `~/.local/share/kamuit` (04/09) não foi atualizada: `bash scripts/install-linux.sh` atualiza quando ele quiser.
- Sem migrations nem deploy de serviço: não aplicável (app desktop).
