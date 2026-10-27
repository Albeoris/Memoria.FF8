using System.Text;
using System.Xml;
using System.Xml.Linq;

static class MacBundleVersion
{
    public static byte[] Stamp(byte[] content, string version)
    {
        string numeric = "0.0.0";
        if (version != "local" && version != "nightly")
        {
            if (!version.StartsWith('v') || !Version.TryParse(version.AsSpan(1), out Version? parsed) || parsed.Build < 0)
            {
                throw new InvalidDataException("Invalid macOS release version.");
            }
            // Apple version keys use three numeric components. The full release tag,
            // including an optional revision, remains explicit in the bundle and manifest.
            numeric = $"{parsed.Major}.{parsed.Minor}.{parsed.Build}";
        }
        using MemoryStream source = new(content);
        using XmlReader reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 });
        XDocument document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        XElement dictionary = document.Root?.Element("dict") ?? throw new InvalidDataException("The macOS bundle has no property dictionary.");
        Set(dictionary, "CFBundleShortVersionString", numeric);
        Set(dictionary, "CFBundleVersion", numeric);
        Set(dictionary, "MemoriaReleaseVersion", version);
        using MemoryStream output = new();
        using (XmlWriter writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false, NewLineHandling = NewLineHandling.None }))
        {
            document.Save(writer);
        }
        return output.ToArray();
    }

    public static void Validate(byte[] content, string version)
    {
        using MemoryStream original = new(content);
        using XmlReader originalReader = XmlReader.Create(original, new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 });
        XElement actual = XDocument.Load(originalReader).Root?.Element("dict") ?? throw new InvalidDataException("The macOS bundle has no property dictionary.");
        using MemoryStream stamped = new(Stamp(content, version));
        using XmlReader expectedReader = XmlReader.Create(stamped, new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null });
        XElement expected = XDocument.Load(expectedReader).Root!.Element("dict")!;
        foreach (string key in new[] { "CFBundleShortVersionString", "CFBundleVersion", "MemoriaReleaseVersion" })
        {
            string? value = actual.Elements("key").SingleOrDefault(element => element.Value == key)?.ElementsAfterSelf().FirstOrDefault()?.Value;
            string required = expected.Elements("key").Single(element => element.Value == key).ElementsAfterSelf().First().Value;
            if (value != required)
            {
                throw new InvalidDataException("The macOS bundle version disagrees with its distribution manifest: " + key);
            }
        }
    }

    private static void Set(XElement dictionary, string key, string value)
    {
        XElement[] matches = dictionary.Elements("key").Where(element => element.Value == key).ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidDataException("Duplicate macOS bundle version key: " + key);
        }
        if (matches.Length == 0)
        {
            dictionary.Add(new XElement("key", key), new XElement("string", value));
            return;
        }
        XElement? target = matches[0].ElementsAfterSelf().FirstOrDefault();
        if (target?.Name != "string")
        {
            throw new InvalidDataException("Invalid macOS bundle version value: " + key);
        }
        target.Value = value;
    }
}
