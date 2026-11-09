using System.Globalization;
using System.Text;

namespace Memoria.FF7.Formats;

public sealed record RsdDescriptor(string Id, string Polygon, IReadOnlyList<string> Textures, IReadOnlyDictionary<string, string> Properties)
{
    public static RsdDescriptor Read(ReadOnlySpan<byte> source, int maximumBytes = 1024 * 1024)
    {
        if (source.Length > maximumBytes || source.ContainsAnyInRange((byte)128, byte.MaxValue))
        {
            throw new InvalidDataException("RSD text exceeds its limit or is not ASCII.");
        }
        string[] lines = Encoding.ASCII.GetString(source).Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0 || !lines[0].StartsWith("@RSD", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unsupported RSD signature.");
        }
        Dictionary<string, string> properties = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines.Skip(1))
        {
            if (line.StartsWith('#'))
            {
                continue;
            }
            int equals = line.IndexOf('=');
            if (equals < 1 || !properties.TryAdd(line[..equals].Trim(), line[(equals + 1)..].Trim()))
            {
                throw new InvalidDataException("RSD contains an invalid or repeated property.");
            }
        }
        if (!properties.TryGetValue("PLY", out string? polygon) || !properties.TryGetValue("NTEX", out string? countText) || !int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count < 0 || count > properties.Count)
        {
            throw new InvalidDataException("RSD polygon or texture count is absent or invalid.");
        }
        string[] textures = new string[count];
        for (int i = 0; i < count; i++)
        {
            if (!properties.TryGetValue($"TEX[{i}]", out string? texture))
            {
                throw new InvalidDataException($"RSD texture {i} is absent.");
            }
            textures[i] = texture;
        }
        return new RsdDescriptor(lines[0], polygon, Array.AsReadOnly(textures), new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(properties));
    }
}
