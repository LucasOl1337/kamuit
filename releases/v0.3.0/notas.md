# KamuiT v0.3.0

27/09/2026 · Windows e Linux

O KamuiT agora roda também no Linux, e os agentes passam a abrir, controlar e organizar as próprias abas pela CLI e pelo MCP. Esta versão junta tudo que foi feito desde a v0.1.0, incluindo a 0.2.0 que nunca virou release.

## Novidades

- **KamuiT no Linux**: host nativo em GTK 4 + VTE com abas, limbo, project pack, títulos vivos e som de pronto, falando o mesmo protocolo do Windows.
- **Abas que já nascem no agente**: Grok, Claude, Codex ou Pi abrem direto na pasta do projeto, por `Ctrl+Shift+G/C/D/P` ou `kamuit open grok -C <pasta> -n 2`.
- **Controle pela linha de comando**: `kamuit open | list | focus | type | close | limbo | show | ping | agents` fala com a janela aberta pelo named pipe no Windows e pelo socket Unix no Linux.
- **Servidor MCP**: com `scripts/kamuit-mcp.mjs`, um agente usa o KamuiT como ferramenta pra abrir, listar, focar e digitar em abas.
- **Project pack**: `Ctrl+Shift+O` lista as pastas de projetos e abre N abas já dentro da escolhida.
- **Reordenar abas**: arraste o cabeçalho ou use `Ctrl+Shift+←/→`; a navegação segue a ordem visual (Windows).
- **Colar no terminal**: `Ctrl+V` cola texto com bracketed paste, e colar só imagem continua chegando na TUI (Windows).

## Melhorias

- **Uma janela só**: abrir o KamuiT de novo foca a janela que já existe e repassa o pedido, sem abrir uma segunda.
- **Som de pronto na aba certa**: cada aba tem identidade fixa, e o som acompanha a posição visual depois de arrastar, fechar ou mandar pro limbo.
- **`Ctrl+Space` mais previsível**: com o KamuiT aberto e sem foco, o atalho traz a janela pra frente em vez de esconder (Windows).
- **TUIs com cor de verdade**: as abas anunciam `TERM`, `COLORTERM` e `WT_SESSION`, então ferramentas de terminal detectam truecolor (Windows).
- **Visual escuro no Linux**: abas, diálogos e títulos no formato `agente · pasta`, fonte JetBrains Mono quando instalada.

## Correções

- **Tab volta pro terminal**: a tecla Tab chega ao PSReadLine e às TUIs em vez de escapar pra interface (Windows).
- **Atalhos com o terminal focado**: no Linux, `Ctrl+Shift+T`, `W`, `X` e os outros funcionam mesmo com o cursor dentro do VTE.
- **Fechar e esconder a aba certa**: no Linux a aba ativa ficava presa na anterior, e `Ctrl+Shift+W`, `Ctrl+Shift+X` ou `kamuit type` sem slot agiam na aba errada.

## Sistemas

- **Downloads prontos**: a release traz `KamuiT-v0.3.0-win-x64.zip` e `KamuiT-v0.3.0-linux-x64.tar.gz`, gerados pelo GitHub Actions a cada versão publicada.
- **CI nos dois sistemas**: cada push compila Windows e Linux e roda o smoke do binário Linux.
- **Instância isolada**: `KAMUIT_PROJECTS_ROOT`, `KAMUIT_AGENT_BIN_DIR` e `KAMUIT_SHELL_INIT` permitem subir um KamuiT de teste sem encostar nos agentes e no shell de verdade.

## Como instalar

- **Windows**: descompacte o zip e abra `KamuiT.exe`. Precisa do .NET 8 Desktop Runtime. `scripts\install-shortcut.ps1` cria o atalho no Menu Iniciar e `scripts\install-cli.ps1` instala o comando `kamuit`.
- **Linux**: extraia o tar.gz e rode `./KamuiT`. Precisa do .NET 8 Runtime, GTK 4 e `libvte-2.91-gtk4`. A partir do código, `bash scripts/install-linux.sh` instala dependências, atalho e CLI.

## Limitações conhecidas

- `Ctrl+Space` global existe só no Windows; no Linux os atalhos valem dentro da janela.
- Som de pronto só nas abas 1 a 5 e ainda sem suporte ao Codex.
- Abas abertas antes de atualizar continuam no formato antigo de sinal até serem reabertas.
- O binário Windows desta versão foi compilado no CI; o teste interativo foi feito no host Linux.

**Comparação completa**: https://github.com/LucasOl1337/kamuit/compare/v0.1.0...v0.3.0
