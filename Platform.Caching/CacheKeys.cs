using System.Security.Cryptography;
using System.Text.Json;

namespace Platform.Caching;

public static class CacheKeys
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string ForRead(string group, string generation, string operation, string identity)
    {
        ValidateGroup(group);
        ValidateSegment(generation);
        ValidateSegment(operation);
        ValidateSegment(identity);
        return $"platform:v1:{group}:{generation}:{operation}:{identity}";
    }

    // Callers normalize domain-specific values before hashing their complete query.
    public static string ForQuery<T>(T parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
            WriteCanonical(writer, JsonSerializer.SerializeToElement(parameters, Json));
        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    internal static void ValidateGroup(string group)
    {
        if (group is not ("accounts" or "newsletters" or "gamestats" or "dashboard"))
            throw new ArgumentException("Unknown cache group.", nameof(group));
    }

    private static void ValidateSegment(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 128 || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.')))
            throw new ArgumentException("Cache segments must contain only ASCII letters, digits, '-', '_' or '.'.");
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
                WriteCanonical(writer, item);
            writer.WriteEndArray();
        }
        else
        {
            element.WriteTo(writer);
        }
    }
}
