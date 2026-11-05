using System.Text.Json;
using System.Text.RegularExpressions;

namespace Memoria.FF8.Platform;

/// <summary>Privacy boundary for diagnostics intended for public issue trackers.</summary>
public static class ReportPrivacy
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);
    private static readonly string repositoryRoot = RepositoryRoot();

    public static string Text(string text)
    {
        string result = Replace(text, @"(?i)\b(?:https?|ftp)://[^\s\""<>]+", _ => "<url>");
        result = Replace(result, @"(?i)(?:[a-z]:[\\/]|\\\\|//)[^\r\n\""<>|]*", match => PathLabel(match.Value));
        result = Replace(result, @"(?<![\w:/>])/(?!/)[^\s\""<>|][^\r\n\""<>|]*", match => PathLabel(match.Value));
        result = Replace(result, @"(?<![\w\\])\\(?!\\)[^\r\n\""<>|]+", match => PathLabel(match.Value));
        result = Replace(result, @"(?<![\w])(?:\.\.[\\/]|~/)[^\r\n\""<>|]+", _ => "<external>/<path>");
        result = Replace(result, @"(?i)\b(?:username|hostname|machine|profile|account|steamid)\s*[:=]\s*[^\s,;]+", _ => "<identity>");
        result = Replace(result, @"(?i)\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b", _ => "<email>");
        result = Replace(result, @"(?i)\b(?:bearer\s+\S+|(?:password|passwd|token|secret|api[_-]?key|authorization|cookie)\s*[:=]\s*[^\s,;]+)", _ => "<credential>");
        result = Replace(result, @"\b\d{17}\b", _ => "<account>");
        foreach (string identity in new[] { Environment.UserName, Environment.MachineName }.Where(value => value.Length > 1).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            result = Replace(result, @"(?i)(?<![\w])" + Regex.Escape(identity) + @"(?![\w])", _ => "<user>");
        }
        return result;
    }

    private static string Replace(string text, string pattern, MatchEvaluator replacement)
    {
        return Regex.Replace(text, pattern, replacement, RegexOptions.CultureInvariant, RegexTimeout);
    }

    private static string PathLabel(string value)
    {
        string path = value.Replace('\\', '/');
        string suffix = "";
        Match line = Regex.Match(path, @":line \d+", RegexOptions.CultureInvariant, RegexTimeout);
        if (line.Success)
        {
            suffix = path[line.Index..];
            path = path[..line.Index];
        }
        foreach ((string root, string label) in new[] { (repositoryRoot, "<repo>"), (AppContext.BaseDirectory, "<app>"), (Environment.CurrentDirectory, "<working>"), (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "<user>") }.OrderByDescending(pair => pair.Item1.Length))
        {
            string normalized = root.Replace('\\', '/').TrimEnd('/');
            if (normalized.Length > 0 && path.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase))
            {
                // Home directory suffixes can contain personal folder and file names too.
                return label == "<user>" ? "<user>/<path>" + suffix : label + "/" + path[(normalized.Length + 1)..] + suffix;
            }
        }
        return "<external>/<path>" + suffix;
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
        {
            string marker = Path.Combine(directory.FullName, ".git");
            if (File.Exists(marker) || Directory.Exists(marker))
            {
                return directory.FullName;
            }
        }
        return "";
    }

    public static JsonElement Json(JsonElement value)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            Write(writer, value, "");
        }
        using JsonDocument document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    public static byte[] JsonBytes(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        return JsonSerializer.SerializeToUtf8Bytes(Json(document.RootElement));
    }

    private static void Write(Utf8JsonWriter writer, JsonElement value, string parent)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject())
                {
                    writer.WritePropertyName(Text(property.Name));
                    if (property.Name is "LastOriginalAchievementObservation" or "SavedLocalAt")
                    {
                        writer.WriteNullValue();
                    }
                    else if (property.Name == "Names")
                    {
                        WriteNames(writer, property.Value);
                    }
                    else if (IsIdentity(property.Name))
                    {
                        WriteAnonymous(writer, property.Value);
                    }
                    else
                    {
                        Write(writer, property.Value, property.Name);
                    }
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    Write(writer, item, parent);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(Text(value.GetString()!));
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }

    private static bool IsIdentity(string name)
    {
        return name.Equals("User", StringComparison.OrdinalIgnoreCase) || name.Equals("UserName", StringComparison.OrdinalIgnoreCase) || name.Equals("MachineName", StringComparison.OrdinalIgnoreCase) || name.Equals("Profile", StringComparison.OrdinalIgnoreCase) || name.Equals("SteamId", StringComparison.OrdinalIgnoreCase) || name.Equals("Email", StringComparison.OrdinalIgnoreCase) || name.Equals("Password", StringComparison.OrdinalIgnoreCase) || name.Equals("Token", StringComparison.OrdinalIgnoreCase) || name.Equals("Key", StringComparison.OrdinalIgnoreCase) || name.Equals("HostName", StringComparison.OrdinalIgnoreCase) || name.Equals("ApiKey", StringComparison.OrdinalIgnoreCase) || name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) || name.Equals("Cookie", StringComparison.OrdinalIgnoreCase) || name is "EnteredName" or "ChocoboName" or "NativeChocoboName";
    }

    private static void WriteNames(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in value.EnumerateObject())
            {
                writer.WritePropertyName(Text(property.Name));
                WriteNames(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else
        {
            WriteAnonymous(writer, value);
        }
    }

    private static void WriteAnonymous(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue("Player");
        }
    }
}
