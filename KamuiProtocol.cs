using System.Text.Json;
using System.Text.Json.Serialization;

namespace KamuiT;

/// <summary>Request JSON (uma linha) no named pipe \\.\pipe\kamuit.</summary>
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
