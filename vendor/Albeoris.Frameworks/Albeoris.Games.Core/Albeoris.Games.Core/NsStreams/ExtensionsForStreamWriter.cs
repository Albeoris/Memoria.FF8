namespace Albeoris.Games.Core.NsStreams;

public static class ExtensionsForStreamWriter
{
    public static void WriteAllLines(this StreamWriter streamWriter, IEnumerable<String> entries)
    {
        ArgumentNullException.ThrowIfNull(streamWriter);
        ArgumentNullException.ThrowIfNull(entries);

        foreach (String line in entries)
            streamWriter.WriteLine(line);
    }
}