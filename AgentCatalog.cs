using System.IO;

namespace KamuiT;

/// <summary>
/// Resolve nomes de agentes (grok, claude, codex, pi, jcode, shell) para o comando
/// que a aba deve rodar depois do init do PowerShell.
/// </summary>
public static class AgentCatalog
{
    public static readonly string[] KnownAgents = ["grok", "claude", "codex", "pi", "jcode", "shell", "pwsh", "none"];

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
            "j" or "jcode" or "j-code" => "jcode",
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

        string? found = OperatingSystem.IsWindows()
            ? agentId switch
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
                "jcode" => FirstExisting(
                    Path.Combine(local, "jcode", "bin", "jcode.exe"),
                    Path.Combine(home, ".local", "bin", "jcode.exe"),
                    Path.Combine(roaming, "npm", "jcode.cmd")),
                _ => null,
            }
            : agentId switch
            {
                "grok" => FirstExisting(
                    Path.Combine(home, ".grok", "bin", "grok"),
                    Path.Combine(home, ".local", "bin", "grok")),
                "claude" => FirstExisting(
                    Path.Combine(home, ".local", "bin", "claude"),
                    Path.Combine(home, ".npm-global", "bin", "claude")),
                "codex" => FirstExisting(
                    Path.Combine(home, ".local", "bin", "codex"),
                    Path.Combine(home, ".npm-global", "bin", "codex")),
                "pi" => FirstExisting(
                    Path.Combine(home, ".local", "bin", "pi"),
                    Path.Combine(home, ".npm-global", "bin", "pi")),
                "jcode" => FirstExisting(
                    Path.Combine(home, ".local", "bin", "jcode"),
                    Path.Combine(home, ".npm-global", "bin", "jcode")),
                _ => null,
            };

        return found ?? FindOnPath(agentId);
    }

    /// <summary>
    /// Comando POSIX seguro pra lançar o agente no shell do Linux
    /// (após init). null = só shell.
    /// </summary>
    public static string? BuildUnixLaunchCommand(string? agent)
    {
        var id = Normalize(agent);
        if (id is null)
            return null;

        var path = ResolvePath(id);
        string cmd;
        if (path is not null)
            cmd = "'" + path.Replace("'", "'\\''") + "'";
        else if (id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.'))
            cmd = id;
        else
            return null;

        if (id == "jcode")
            cmd += " --provider-profile 9router";
        return cmd;
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
        {
            var exe = "& '" + path.Replace("'", "''") + "'";
            // jcode: força profile 9router (config em ~/.jcode/config.toml).
            // Garante NINE_ROUTER_API_KEY do User env se o processo atual não herdou.
            if (id == "jcode")
            {
                return "if (-not $env:NINE_ROUTER_API_KEY) { $env:NINE_ROUTER_API_KEY = [Environment]::GetEnvironmentVariable('NINE_ROUTER_API_KEY','User') }; " +
                       "Remove-Item Env:OPENROUTER_API_KEY -ErrorAction SilentlyContinue; " +
                       exe + " --provider-profile 9router";
            }
            return exe;
        }

        // fallback: nome no PATH (sem metachar)
        if (id.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.'))
        {
            if (id == "jcode")
            {
                return "if (-not $env:NINE_ROUTER_API_KEY) { $env:NINE_ROUTER_API_KEY = [Environment]::GetEnvironmentVariable('NINE_ROUTER_API_KEY','User') }; " +
                       "Remove-Item Env:OPENROUTER_API_KEY -ErrorAction SilentlyContinue; " +
                       "jcode --provider-profile 9router";
            }
            return id;
        }

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

    private static string? FindOnPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var exts = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".bat", "" }
            : new[] { "" };
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in exts)
            {
                var candidate = Path.Combine(dir, name + ext);
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        return null;
    }
}
