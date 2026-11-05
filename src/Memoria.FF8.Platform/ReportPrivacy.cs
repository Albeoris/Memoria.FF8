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
        List<string> metadata = new();
        string result = Replace(text, """(?i)\b(?:Launch[_-]?Id|Launch)\s*[:=]\s*(?:"[^"\r\n]*"|'[^'\r\n]*'|[^\s,;]+)""", match =>
        {
            metadata.Add(match.Value);
            return "<preservedLaunch" + (metadata.Count - 1) + ">";
        });
        // Remove whole quoted values before path matching can consume their labels.
        result = Replace(result, """(?i)\b(?:authorization|cookie)["']?\s*[:=]\s*(?:"[^"\r\n]*"|'[^'\r\n]*'|[^\r\n,;]+)""", _ => "<credential>");
        result = Replace(result, """(?i)\b(?:password|passwd|(?:(?:access|refresh)[_-]?)?token|secret|api[_-]?key)["']?\s*[:=]\s*(?:"[^"\r\n]*"|'[^'\r\n]*'|[^\s,;]+)|\bbearer\s+[^\s,;]+""", _ => "<credential>");
        result = Replace(result, """(?i)\b(?:user(?:(?:[_-]?(?:name|id|domain(?:name)?)))?|host(?:[_-]?name)?|machine(?:[_-]?name)?|profile(?:[_-]?id)?|account(?:[_-]?id)?|steam[_-]?id(?:64)?|ip(?:[_-]?address)?)["']?\s*[:=]\s*(?:"[^"\r\n]*"|'[^'\r\n]*'|[^\s,;]+)""", _ => "<identity>");
        result = Replace(result, @"(?i)\b(?:https?|ftp)://[^\s\""<>]+", _ => "<url>");
        result = Replace(result, """(?i)(?:[a-z]:[\\/]|\\\\|//)[^\r\n"'<>|;]*""", match => PathLabel(match.Value));
        result = Replace(result, """(?<![\w:/>])/(?!/)[^\s"'<>|][^\r\n"'<>|;]*""", match => PathLabel(match.Value));
        result = Replace(result, """(?<![\w\\])\\(?!\\)[^\r\n"'<>|;]+""", match => PathLabel(match.Value));
        result = Replace(result, """(?<![\w])(?:\.\.[\\/]|~/)[^\r\n"'<>|;]+""", _ => "<external>/<path>");
        result = Replace(result, @"(?i)\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b", _ => "<email>");
        result = Replace(result, @"\b\d{17}\b", _ => "<account>");
        foreach (string identity in new[] { Environment.UserName, Environment.MachineName }.Where(value => value.Length > 1).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            result = Replace(result, @"(?i)(?<![\w])" + Regex.Escape(identity) + @"(?![\w])", _ => "<user>");
        }
        for (int index = 0; index < metadata.Count; index++)
        {
            result = result.Replace("<preservedLaunch" + index + ">", metadata[index], StringComparison.Ordinal);
        }
        return result;
    }

    public static string LogText(string text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                return Json(document.RootElement).GetRawText();
            }
        }
        catch (JsonException)
        {
            // Console output commonly mixes individual records and ordinary text.
        }
        return string.Join('\n', text.Split('\n').Select(SanitizeLogLine));
    }

    private static string SanitizeLogLine(string line)
    {
        int offset = 0;
        for (int attempt = 0; attempt < 8 && offset < line.Length; attempt++)
        {
            int relative = line.AsSpan(offset).IndexOfAny('{', '[');
            if (relative < 0)
            {
                break;
            }
            int first = offset + relative;
            offset = first + 1;
            int last = Math.Max(line.LastIndexOf('}'), line.LastIndexOf(']'));
            if (last >= first)
            {
                try
                {
                    using JsonDocument document = JsonDocument.Parse(line[first..(last + 1)]);
                    return Text(line[..first]) + Json(document.RootElement).GetRawText() + Text(line[(last + 1)..]);
                }
                catch (JsonException)
                {
                    // Exception text and partial console records use the same text boundary.
                }
            }
        }
        return Text(line);
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
                    if (IsGameInput(property.Name) || IsMetadata(property.Name))
                    {
                        property.Value.WriteTo(writer);
                    }
                    else if (IsIdentity(property.Name) || parent.Equals("Owner", StringComparison.OrdinalIgnoreCase) && property.Name.Equals("Key", StringComparison.OrdinalIgnoreCase))
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
        return NormalizeLabel(name) is "user" or "username" or "userid" or "userdomain" or "userdomainname" or "machine" or "machinename" or "profile" or "profileid" or "account" or "accountid" or "steamid" or "steamid64" or "host" or "hostname" or "ip" or "ipaddress" or "email" or "password" or "passwd" or "token" or "accesstoken" or "refreshtoken" or "secret" or "apikey" or "authorization" or "cookie";
    }

    private static bool IsGameInput(string name)
    {
        return name is "Names" or "EnteredName" or "ChocoboName" or "NativeChocoboName";
    }

    private static bool IsMetadata(string name)
    {
        return NormalizeLabel(name) is "launchid" or "timestamp" or "capturedutc" or "savedlocalat";
    }

    private static string NormalizeLabel(string name)
    {
        return name.Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
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
