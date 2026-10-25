using System.Text;
using System.Globalization;

namespace Memoria.FF8.Mods;

internal static class ModConfiguration
{
    public static bool? ReadBoolean(string path, string key)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        bool? value = null;
        foreach (string line in File.ReadAllLines(path))
        {
            string text = line.Trim().TrimStart('\uFEFF');
            if (text.Length == 0 || text[0] is ';' or '#' or '[')
            {
                continue;
            }
            int equals = text.IndexOf('=');
            if (equals < 0 || !text[..equals].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            string setting = text[(equals + 1)..].Split([';', '#'], 2)[0].Trim();
            value = setting switch
            {
                "0" => false,
                "1" => true,
                _ => throw new InvalidDataException($"Invalid {key} value in {path}: expected 0 or 1.")
            };
        }
        return value;
    }

    public static IReadOnlyDictionary<string, int> ReadJunctionOptions(string path)
    {
        Dictionary<string, int> options = new(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
        {
            return options;
        }
        foreach (string line in File.ReadAllLines(path))
        {
            string text = line.Trim().TrimStart('\uFEFF');
            if (text.Length == 0 || text[0] is ';' or '#' or '[')
            {
                continue;
            }
            int equals = text.IndexOf('=');
            string key = (equals < 0 ? text : text[..equals]).Trim();
            if (!key.StartsWith("option.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            string id = key[7..].Trim();
            string setting = equals < 0 ? "" : text[(equals + 1)..].Split([';', '#'], 2)[0].Trim();
            if (id.Length == 0 || !int.TryParse(setting, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                throw new InvalidDataException($"Invalid Junction option setting {key}: expected an option ID and integer value.");
            }
            options[id] = value;
        }
        return options;
    }

    public static void WriteBoolean(string path, string key, bool value)
    {
        string original = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        string newline = original.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : Environment.NewLine;
        List<string> lines = original.Length == 0 ? [] : original.Replace("\r\n", "\n").Split('\n').ToList();
        bool replaced = false;
        for (int index = 0; index < lines.Count; index++)
        {
            string text = lines[index].Trim().TrimStart('\uFEFF');
            int equals = text.IndexOf('=');
            if (text.StartsWith(';') || text.StartsWith('#') || equals < 0 || !text[..equals].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            int comment = text.IndexOfAny([';', '#'], equals + 1);
            string suffix = comment >= 0 ? " " + text[comment..] : string.Empty;
            lines[index] = $"{key} = {(value ? 1 : 0)}{suffix}";
            replaced = true;
        }
        if (!replaced)
        {
            if (lines.Count > 0 && lines[^1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }
            lines.Add($"{key} = {(value ? 1 : 0)}");
            lines.Add(string.Empty);
        }
        WriteAtomic(path, Encoding.UTF8.GetBytes(string.Join(newline, lines)));
    }

    public static void WriteAtomic(string path, byte[] bytes)
    {
        string directory = Path.GetDirectoryName(path) ?? throw new ArgumentException("A configuration file must have a parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
