using System.Reflection;

namespace Albeoris.Games.Core.NsResources;

/// <summary>
/// Opens resources embedded into an assembly's manifest.
/// </summary>
public static class EmbeddedResources
{
    /// <summary>
    /// Opens the embedded resource at <paramref name="resourcePath"/>, a path relative to the
    /// assembly's root namespace (e.g. "Resources/european.codepage.json" or
    /// "/Resources/european.codepage.json").
    /// </summary>
    public static Stream Open(Assembly assembly, String resourcePath)
    {
        String resourceName = ToResourceName(resourcePath);
        String fullResourceName = assembly.GetName().Name + "." + resourceName;
        Stream? stream = assembly.GetManifestResourceStream(fullResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded resource '{fullResourceName}' was not found in assembly '{assembly.GetName().Name}'.");
        }

        return stream;
    }

    /// <summary>
    /// Converts a "/"-or-"\"-separated resource path into the dot-separated name used by
    /// the assembly manifest, stripping a leading path separator if present.
    /// </summary>
    private static String ToResourceName(String resourcePath)
    {
        Int32 start = 0;
        if (resourcePath.Length > 0 && (resourcePath[0] == '/' || resourcePath[0] == '\\'))
        {
            start = 1;
        }

        Char[] characters = new Char[resourcePath.Length - start];
        Int32 index = 0;
        for (Int32 i = start; i < resourcePath.Length; i++)
        {
            Char c = resourcePath[i];
            characters[index] = c == '/' || c == '\\' ? '.' : c;
            index++;
        }

        return new String(characters);
    }
}
