using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Memoria.FF8;

internal sealed record OriginalSaveMetadataUpdate(string Path, ImmutableArray<byte>? OriginalBytes, ImmutableArray<byte> UpdatedBytes);

internal static class OriginalSaveMetadata
{
    public static OriginalSaveMetadataUpdate? Prepare(string savePath, byte[] content, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        string fullPath = Path.GetFullPath(savePath);
        string directory = Path.GetDirectoryName(fullPath)!;
        string metadataPath = Path.Combine(directory, "metadata.xml");
        bool exists = File.Exists(metadataPath);
        Match profile = Regex.Match(Path.GetFileName(directory), "^user_([0-9a-fA-F]+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!profile.Success)
        {
            if (exists)
            {
                throw new InvalidDataException("Original save metadata requires its user profile folder identifier.");
            }
            return null;
        }
        Match target = Regex.Match(Path.GetFileName(fullPath), "^slot([12])_save(0[1-9]|[12][0-9]|30)\\.ff8$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!target.Success)
        {
            throw new InvalidDataException("Original save metadata requires a native bank and slot filename.");
        }
        int bank = int.Parse(target.Groups[1].Value, CultureInfo.InvariantCulture);
        int number = int.Parse(target.Groups[2].Value, CultureInfo.InvariantCulture);
        string userId = profile.Groups[1].Value;
        byte[]? original = exists ? File.ReadAllBytes(metadataPath) : null;
        XDocument document;
        if (original is not null)
        {
            try
            {
                using MemoryStream input = new(original, writable: false);
                using XmlReader reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            }
            catch (XmlException exception)
            {
                throw new InvalidDataException("Original save metadata is not valid XML.", exception);
            }
        }
        else
        {
            document = CreateProfile(directory, userId);
        }
        XElement root = document.Root ?? throw new InvalidDataException("Original save metadata has no game status root.");
        if (root.Name != "gamestatus")
        {
            throw new InvalidDataException("Original save metadata has an unsupported game status root.");
        }
        ValidateEntries(root);
        XElement[] matches = root.Elements("savefile").Where(element => (string?)element.Attribute("type") == "ff8" && (int?)element.Attribute("slot") == bank && (int?)element.Attribute("num") == number).ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidDataException("Original save metadata contains duplicate target slots.");
        }
        XElement entry;
        if (matches.Length == 0)
        {
            entry = CreateEntry(bank, number, "", Signature([], userId));
            root.Add(entry);
        }
        else
        {
            entry = matches[0];
        }
        entry.Element("timestamp")!.Value = (now ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        entry.Element("signature")!.Value = Signature(content, userId);
        using MemoryStream output = new();
        using (XmlWriter writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, OmitXmlDeclaration = false }))
        {
            document.Save(writer);
        }
        return new(metadataPath, original?.ToImmutableArray(), output.ToArray().ToImmutableArray());
    }

    private static void ValidateEntries(XElement root)
    {
        foreach (XElement entry in root.Elements("savefile"))
        {
            string? type = (string?)entry.Attribute("type");
            if (type is not ("ff8" or "choco"))
            {
                continue;
            }
            if (type == "ff8" && (!int.TryParse((string?)entry.Attribute("slot"), NumberStyles.None, CultureInfo.InvariantCulture, out int bank) || bank is < 1 or > 2 || !int.TryParse((string?)entry.Attribute("num"), NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number is < 1 or > 30))
            {
                throw new InvalidDataException("Original save metadata contains an invalid bank or slot.");
            }
            XElement[] timestamps = entry.Elements("timestamp").ToArray();
            XElement[] signatures = entry.Elements("signature").ToArray();
            if (timestamps.Length != 1 || signatures.Length != 1 || timestamps[0].HasElements || signatures[0].HasElements || timestamps[0].Value.Length != 0 && !long.TryParse(timestamps[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _) || !Regex.IsMatch(signatures[0].Value, "^[0-9a-fA-F]{32}$", RegexOptions.CultureInvariant))
            {
                throw new InvalidDataException("Original save metadata requires one valid timestamp and signature per saved entry.");
            }
        }
    }

    private static XDocument CreateProfile(string directory, string userId)
    {
        XElement root = new("gamestatus");
        for (int bank = 1; bank <= 2; bank++)
        {
            for (int number = 1; number <= 30; number++)
            {
                string path = Path.Combine(directory, $"slot{bank}_save{number:00}.ff8");
                (string timestamp, string signature) = ExistingSignature(path, userId);
                root.Add(CreateEntry(bank, number, timestamp, signature));
            }
        }
        (string chocoTimestamp, string chocoSignature) = ExistingSignature(Path.Combine(directory, "chocorpg.ff8"), userId);
        root.Add(new XElement("savefile", new XAttribute("type", "choco"), new XElement("timestamp", chocoTimestamp), new XElement("signature", chocoSignature)));
        return new(new XDeclaration("1.0", "UTF-8", null), root);
    }

    private static XElement CreateEntry(int bank, int number, string timestamp, string signature)
    {
        return new("savefile", new XAttribute("num", number), new XAttribute("type", "ff8"), new XAttribute("slot", bank), new XElement("timestamp", timestamp), new XElement("signature", signature));
    }

    private static (string Timestamp, string Signature) ExistingSignature(string path, string userId)
    {
        return File.Exists(path)
            ? (new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture), Signature(File.ReadAllBytes(path), userId))
            : ("", Signature([], userId));
    }

    private static string Signature(byte[] content, string userId)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        hash.AppendData(content);
        hash.AppendData(Encoding.ASCII.GetBytes(userId));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
