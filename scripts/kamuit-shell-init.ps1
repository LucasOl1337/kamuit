# KamuiT shell init — sourced by every new tab.
# Tab = autofill: aceita previsão inline se houver; senão completa o próximo match.
# NÃO usa MenuComplete (sem popup de lista).

# Terminal identity for TUI apps (Devin, etc.) that treat unknown hosts as conhost.
# KamuiT is ConPTY + Windows Terminal core; these vars match what WT/modern hosts expose.
if (-not $env:TERM -or $env:TERM -eq 'dumb') { $env:TERM = 'xterm-256color' }
if (-not $env:COLORTERM) { $env:COLORTERM = 'truecolor' }
if (-not $env:TERM_PROGRAM) { $env:TERM_PROGRAM = 'KamuiT' }
if (-not $env:TERM_PROGRAM_VERSION) { $env:TERM_PROGRAM_VERSION = '0.1.0' }
if (-not $env:WT_SESSION) { $env:WT_SESSION = [guid]::NewGuid().ToString() }
if (-not $env:KAMUIT) { $env:KAMUIT = '1' }

try {
    Set-PSReadLineOption -PredictionSource History -ErrorAction Stop
    Set-PSReadLineOption -PredictionViewStyle InlineView -ErrorAction SilentlyContinue
} catch {
    # PSReadLine antigo / Prediction indisponível — segue só com Complete
}

try {
    Set-PSReadLineKeyHandler -Key Tab -BriefDescription 'KamuiT autofill' -LongDescription 'Accept inline suggestion if any, else TabCompleteNext' -ScriptBlock {
        param($key, $arg)

        $line = $null
        $cursor = $null
        [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$line, [ref]$cursor)

        # No fim da linha: tenta aceitar o texto cinza (previsão do histórico)
        if ($null -ne $line -and $cursor -eq $line.Length -and $line.Length -gt 0) {
            $before = $line
            try {
                [Microsoft.PowerShell.PSConsoleReadLine]::AcceptSuggestion($key, $arg)
            } catch {
                try { [Microsoft.PowerShell.PSConsoleReadLine]::AcceptSuggestion() } catch { }
            }
            $after = $null
            $c2 = $null
            [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$after, [ref]$c2)
            if ($null -ne $after -and $after -ne $before) {
                return
            }
        }

        # Sem previsão → completa caminho/comando (troca o texto, sem menu)
        [Microsoft.PowerShell.PSConsoleReadLine]::TabCompleteNext()
    }

    Set-PSReadLineKeyHandler -Key Shift+Tab -Function TabCompletePrevious
} catch {
    # fallback: binding padrão do PSReadLine
    try { Set-PSReadLineKeyHandler -Key Tab -Function TabCompleteNext } catch { }
}
