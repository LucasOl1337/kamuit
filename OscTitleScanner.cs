using System;

namespace KamuiT;

/// <summary>
/// Detecta sequências OSC de título (ESC]0;titulo BEL ou ESC]2;titulo ESC\)
/// no stream de saída do terminal. É assim que apps como Grok/pwsh anunciam
/// "Waiting for response...", "Thinking...", etc. pro emulador mostrar na aba.
/// Tolera sequências quebradas entre chunks de leitura do ConPTY.
/// </summary>
public sealed class OscTitleScanner
{
    private const int MaxCarry = 512;
    private string _leftover = "";

    /// <summary>Retorna o último título visto no chunk (ou null se nenhum).</summary>
    public string? Feed(ReadOnlySpan<char> data)
    {
        // Fast path sem alocação: a esmagadora maioria dos chunks não tem ESC
        // (e OSC de título é raríssimo) — não vale o ToString() nesses casos.
        if (_leftover.Length == 0 && data.IndexOf('\x1b') < 0)
            return null;

        var s = _leftover + data.ToString();
        _leftover = "";

        string? title = null;
        var i = 0;
        while (i < s.Length)
        {
            var start = s.IndexOf("\x1b]", i, StringComparison.Ordinal);
            if (start < 0)
                break;

            var bel = s.IndexOf('\a', start + 2);
            var st = s.IndexOf("\x1b\\", start + 2, StringComparison.Ordinal);
            var end = bel < 0 ? st : (st < 0 ? bel : Math.Min(bel, st));

            if (end < 0)
            {
                // sequência incompleta: guarda do ESC em diante pra juntar com o próximo chunk
                if (s.Length - start <= MaxCarry)
                    _leftover = s[start..];
                break;
            }

            var content = s[(start + 2)..end];
            var semi = content.IndexOf(';');
            if (semi > 0 && content[..semi] is "0" or "2")
            {
                var candidate = content[(semi + 1)..].Trim();
                if (candidate.Length > 0)
                    title = candidate;
            }

            i = end + (end == bel ? 1 : 2);
        }
        return title;
    }
}
