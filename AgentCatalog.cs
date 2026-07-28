using System.IO;

namespace KamuiT;

/// <summary>
/// Resolve nomes de agentes (grok, claude, codex, pi, shell) para o comando
/// que a aba deve rodar depois do init do PowerShell.
/// </summary>
public static class AgentCatalog
{
    public static readonly string[] KnownAgents = ["grok", "claude", "codex", "pi", "shell", "pwsh", "none"];

    /// <summary>
    /// Normaliza alias → id canônico. null/vazio/"shell"/"pwsh"/"none" → só shell.
    /// </summary>
    public static string? Normalize(string? agent)
    {
        if (string.IsNullOrWhiteSpace(agent))
            return null;

        var a = agent.Trim().ToLowerInvariant();
        return a switch
        {
            "shell" or "pwsh" or "powershell" or "none" or "empty" => null,
            "g" or "grok" or "grok-build" or "xai" => "grok",
            "c" or "claude" or "claude-code" or "anthropic" => "claude",
            "x" or "codex" or "openai" => "codex",
            "p" or "pi" => "pi",
            _ => a, // permite comando custom no PATH
        };
    }

    /// <summary>
    /// Caminho do executável/script, se conhecido. null se for só nome no PATH.
    /// </summary>
    public static string? ResolvePath(string agentId)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return agentId switch
        {
            "grok" => FirstExisting(
                Path.Combine(home, ".grok", "bin", "grok.exe"),
                Path.Combine(home, ".local", "bin", "grok.exe")),
            "claude" => FirstExisting(
                Path.Combine(roaming, "npm", "claude.cmd"),
                Path.Combine(roaming, "npm", "claude.ps1"),
                Path.Combine(local, "AnthropicClaude", "claude.exe")),
            "codex" => FirstExisting(
                Path.Combine(roaming, "npm", "codex.cmd"),
                Path.Combine(roaming, "npm", "codex.ps1"),
                Path.Combine(home, ".local", "bin", "codex.exe")),
            "pi" => FirstExisting(
                Path.Combine(roaming, "npm", "pi.cmd"),
                Path.Combine(roaming, "npm", "pi.ps1")),
            _ => null,
        };
    }

    /// <summary>
    /// Snippet PowerShell seguro pra colar no -Command da aba (após init).
    /// Ex.: <c>&amp; 'C:\Users\…\grok.exe'</c> ou <c>grok</c>.
    /// </summary>
    public static string? BuildLaunchSnippet(string? agent)
    {
        var id = Normalize(agent);
        if (id is null)
            return null;

        var path = ResolvePath(id);
        if (path is not null)
            return "& '" + path.Replace("'", "''") + "'";

        // fallback: nome no PATH (sem metachar)
        if (id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.'))
            return id;

        return null;
    }

    public static bool IsKnown(string? agent)
    {
        var id = Normalize(agent);
        return id is null || KnownAgents.Contains(id) || ResolvePath(id) is not null
               || (id is not null && id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.'));
    }

    private static string? FirstExisting(params string[] paths)
    {
        foreach (var p in paths)
        {
            if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
                return p;
        }
        return null;
    }
}
