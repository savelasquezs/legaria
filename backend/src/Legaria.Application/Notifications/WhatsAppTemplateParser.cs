using System.Text.Json;
using System.Text.RegularExpressions;

namespace Legaria.Application.Notifications;

public static partial class WhatsAppTemplateParser
{
    public static IReadOnlyCollection<string> DetectVariables(string componentsJson)
    {
        using var document = JsonDocument.Parse(componentsJson);
        var variables = new List<string>();
        Walk(document.RootElement, "$", variables);
        return variables.Distinct(StringComparer.Ordinal).ToArray();
    }

    public static string DetectButtonsJson(string componentsJson)
    {
        using var document = JsonDocument.Parse(componentsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array) return "[]";
        var buttons = document.RootElement.EnumerateArray()
            .Where(component => GetString(component, "type") == "BUTTONS")
            .SelectMany(component => component.TryGetProperty("buttons", out var value) && value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray().Select(button => new
                {
                    type = GetString(button, "type"),
                    text = GetString(button, "text"),
                    url = GetString(button, "url")
                })
                : [])
            .ToArray();
        return JsonSerializer.Serialize(buttons);
    }

    private static void Walk(JsonElement element, string path, ICollection<string> variables)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) Walk(property.Value, $"{path}.{property.Name}", variables);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray()) Walk(item, $"{path}[{index++}]", variables);
                break;
            case JsonValueKind.String:
                foreach (Match match in PlaceholderRegex().Matches(element.GetString() ?? string.Empty))
                    variables.Add($"{path}:{match.Groups[1].Value}");
                break;
        }
    }

    private static string? GetString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String
            ? item.GetString()
            : null;

    [GeneratedRegex(@"\{\{\s*([^{}]+?)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
