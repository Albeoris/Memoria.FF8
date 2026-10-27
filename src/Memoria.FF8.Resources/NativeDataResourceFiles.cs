namespace Memoria.FF8.Resources;

public enum NativeDataResourceKind
{
    Kernel,
    Enemy,
    NameDictionary,
    MenuGroupData,
    MenuGroupIndex,
    WorldSettings,
    Texture,
    CompressedImage
}

/// <summary>Canonical native binary resource families with independently validated layouts.</summary>
public static class NativeDataResourceFiles
{
    public static NativeDataResourceKind? Classify(ResourceKey key)
    {
        string path = key.Path;
        if (path == "main/kernel.bin")
        {
            return NativeDataResourceKind.Kernel;
        }
        if (path == "main/namedic.bin")
        {
            return NativeDataResourceKind.NameDictionary;
        }
        if (path == "menu/mngrp.bin")
        {
            return NativeDataResourceKind.MenuGroupData;
        }
        if (path == "menu/mngrphd.bin")
        {
            return NativeDataResourceKind.MenuGroupIndex;
        }
        string[] parts = path.Split('/');
        if (parts.Length == 2 && parts[0] == "battle" && parts[1].Length == 10 && parts[1].StartsWith("c0m", StringComparison.Ordinal) && parts[1].AsSpan(3, 3).ContainsAnyExceptInRange('0', '9') == false && parts[1][6..] == ".dat")
        {
            return NativeDataResourceKind.Enemy;
        }
        if (parts.Length == 2 && parts[0] == "world" && parts[1].Length == 11 && parts[1].StartsWith("wmset", StringComparison.Ordinal) && parts[1].AsSpan(5, 2).ContainsAnyExceptInRange('a', 'z') == false && parts[1][7..] == ".obj")
        {
            return NativeDataResourceKind.WorldSettings;
        }
        if (parts.Length is 2 or 3 && parts[0] == "menu" && (parts.Length == 2 || parts[1] == "hires"))
        {
            string name = parts[^1];
            string stem = Path.GetFileNameWithoutExtension(name);
            if (name.EndsWith(".tex", StringComparison.Ordinal) && ((stem.Length == 5 && stem.StartsWith("mag", StringComparison.Ordinal) && !stem.AsSpan(3).ContainsAnyExceptInRange('0', '9')) || (stem.Length == 8 && (stem.StartsWith("sysfld", StringComparison.Ordinal) || stem.StartsWith("sysfnt", StringComparison.Ordinal)) && !stem.AsSpan(6).ContainsAnyExceptInRange('0', '9'))))
            {
                return NativeDataResourceKind.Texture;
            }
        }
        if (parts.Length == 2 && parts[0] == "main" && parts[1].EndsWith(".lzs", StringComparison.Ordinal))
        {
            string stem = parts[1][..^4];
            if (stem == "discerr" || stem.Length == 5 && stem.StartsWith("disc", StringComparison.Ordinal) && stem[4] is >= '1' and <= '4' || stem.Length == 8 && stem.StartsWith("discimg", StringComparison.Ordinal) && stem[7] is >= '1' and <= '4')
            {
                return NativeDataResourceKind.CompressedImage;
            }
        }
        return null;
    }

    public static string? Group(ResourceKey key) => Classify(key) is NativeDataResourceKind.MenuGroupData or NativeDataResourceKind.MenuGroupIndex ? "menu/mngrp" : null;
}
