using System.Text.Json.Serialization;

namespace safe_travels.Models;

/// <summary>
/// Serializable model for a saved favorite stop with optional user nickname.
/// Property names match existing Stop names so XAML bindings continue to work.
/// </summary>
public class FavoriteStop
{
    [JsonPropertyName("type")]
    public string? type { get; set; }

    [JsonPropertyName("id")]
    public string? id { get; set; }

    [JsonPropertyName("stopId")]
    public string stopId { get; set; } = string.Empty;

    [JsonPropertyName("stopName")]
    public string stopName { get; set; } = string.Empty;

    [JsonPropertyName("stopLat")]
    public double stopLat { get; set; }

    [JsonPropertyName("stopLong")]
    public double stopLong { get; set; }

    /// <summary>
    /// Optional user-provided nickname for this saved stop.
    /// </summary>
    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }

    [JsonIgnore]
    public string DisplayLabel => !string.IsNullOrWhiteSpace(Nickname)
            ? $"{Nickname} ({stopName})"
            : stopName;

    public override string ToString() => DisplayLabel;
}
