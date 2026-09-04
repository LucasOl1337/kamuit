using System.Text.Json;
using System.Text.Json.Serialization;

namespace KamuiT;

/// <summary>Request JSON (uma linha) no pipe/socket do KamuiT.</summary>
public sealed class KamuiRequest
{
    [JsonPropertyName("op")]
    public string Op { get; set; } = "";

    [JsonPropertyName("agent")]
    public string? Agent { get; set; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    [JsonPropertyName("count")]
    public int? Count { get; set; }

    [JsonPropertyName("slot")]
    public int? Slot { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("enter")]
    public bool? Enter { get; set; }

    [JsonPropertyName("focus")]
    public bool? Focus { get; set; }

    [JsonPropertyName("show")]
    public bool? Show { get; set; }

    /// <summary>
    /// CLI embutida: <c>KamuiT.exe open grok -C path</c> (Windows) ou
    /// <c>KamuiT open grok -C path</c> (Linux).
    /// </summary>
    public static KamuiRequest ParseCli(string[] args)
    {
        if (args.Length == 0)
            return new KamuiRequest { Op = "show" };

        var op = args[0].Trim().ToLowerInvariant();
        if (op is "grok" or "claude" or "codex" or "pi" or "jcode" or "shell")
            return new KamuiRequest { Op = "open", Agent = op, Count = 1, Show = true };

        if (op is not ("open" or "new" or "tab" or "list" or "show" or "focus" or "type" or "ping" or "close" or "agents"))
            return new KamuiRequest { Op = "show" };

        var req = new KamuiRequest { Op = op, Show = true };
        for (var i = 1; i < args.Length; i++)
        {
            var a = args[i];
            if ((a is "-a" or "--agent" or "-agent") && i + 1 < args.Length)
                req.Agent = args[++i];
            else if ((a is "-C" or "--cwd" or "-cwd" or "--dir") && i + 1 < args.Length)
                req.Cwd = args[++i];
            else if ((a is "-n" or "--count" or "-count") && i + 1 < args.Length && int.TryParse(args[++i], out var n))
                req.Count = n;
            else if ((a is "-s" or "--slot" or "-slot") && i + 1 < args.Length && int.TryParse(args[++i], out var s))
                req.Slot = s;
            else if ((a is "-t" or "--text" or "-text") && i + 1 < args.Length)
                req.Text = args[++i];
            else if (a is "--enter" or "-enter")
                req.Enter = true;
            else if (a is "--no-show")
                req.Show = false;
            else if (a.StartsWith('-'))
                continue;
            else if ((req.Op is "open" or "new" or "tab") && req.Agent is null)
                req.Agent = a;
            else if ((req.Op is "focus" or "close") && req.Slot is null && int.TryParse(a, out var slot))
                req.Slot = slot;
            else if (req.Op is "type" && req.Text is null)
                req.Text = a;
        }
        return req;
    }
}

public sealed class KamuiTabInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("slot")]
    public int Slot { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("agent")]
    public string? Agent { get; set; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("limbo")]
    public bool Limbo { get; set; }
}

public sealed class KamuiResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("tabs")]
    public List<KamuiTabInfo>? Tabs { get; set; }

    [JsonPropertyName("limbo")]
    public List<KamuiTabInfo>? Limbo { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    public static KamuiResponse Success(string? message = null) => new() { Ok = true, Message = message };
    public static KamuiResponse Fail(string error) => new() { Ok = false, Error = error };
}

public static class KamuiJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
