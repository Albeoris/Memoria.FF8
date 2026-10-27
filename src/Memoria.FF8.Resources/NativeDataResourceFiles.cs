namespace Memoria.FF8.Resources;

public enum NativeDataResourceKind
{
    Kernel,
    InitialData,
    BattleScenes,
    MenuTable,
    MenuSpriteIndex,
    AreaNames,
    Enemy,
    NameDictionary,
    MenuGroupData,
    MenuGroupIndex,
    WorldSettings,
    WorldData,
    Texture,
    CompressedImage
}

/// <summary>Canonical native binary resource families with independently validated layouts.</summary>
public static class NativeDataResourceFiles
{
    public static NativeDataResourceKind? Classify(ResourceKey key)
    {
        string path = key.Path;
        if (path == "main/init.out")
        {
            return NativeDataResourceKind.InitialData;
        }
        if (path == "battle/scene.out")
        {
            return NativeDataResourceKind.BattleScenes;
        }
        if (IsStandaloneRefinementTable(key) || path is "menu/shop.bin" or "menu/price.bin" or "menu/mwepon.bin" or "menu/mwepon.msg" or "menu/mthomas.bin" or "menu/mitem.bin" or "menu/cyocobo.bin" or "menu/mtmag.bin" or "menu/mmag.bin" or "menu/pet_exp.bin" or "menu/pet_exp.msg")
        {
            return NativeDataResourceKind.MenuTable;
        }
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
        if (path is "world/wmx.obj" or "world/texl.obj" or "world/rail.obj" or "world/chara.one" || path.Length == 16 && path.StartsWith("world/music", StringComparison.Ordinal) && path[11] is >= '0' and <= '5' && path.EndsWith(".obj", StringComparison.Ordinal))
        {
            return NativeDataResourceKind.WorldData;
        }
        if (path is "menu/icon.sp1" or "menu/face.sp2")
        {
            return NativeDataResourceKind.MenuSpriteIndex;
        }
        if (path == "menu/areames.dc1")
        {
            return NativeDataResourceKind.AreaNames;
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
            if (name is "icon.tex" or "face_b00.tex" or "face_b01.tex" or "gf_big00.tex" or "gf_big01.tex" or "magita.tex" or "face1.tex" or "face2.tex" or "start00.tex" or "start01.tex" or "sysfnt.tex" || name.Length == 8 && name.StartsWith("mc", StringComparison.Ordinal) && !name.AsSpan(2, 2).ContainsAnyExceptInRange('0', '9') && name.EndsWith(".tex", StringComparison.Ordinal))
            {
                return NativeDataResourceKind.Texture;
            }
            if (name.EndsWith(".tex", StringComparison.Ordinal) && ((stem.Length == 5 && stem.StartsWith("mag", StringComparison.Ordinal) && !stem.AsSpan(3).ContainsAnyExceptInRange('0', '9')) || (stem.Length == 8 && (stem.StartsWith("sysfld", StringComparison.Ordinal) || stem.StartsWith("sysfnt", StringComparison.Ordinal)) && !stem.AsSpan(6).ContainsAnyExceptInRange('0', '9'))))
            {
                return NativeDataResourceKind.Texture;
            }
        }
        if (parts.Length == 2 && parts[0] == "main" && parts[1].EndsWith(".lzs", StringComparison.Ordinal))
        {
            string stem = parts[1][..^4];
            if (stem == "ff8" || stem == "discerr" || stem.Length == 5 && stem.StartsWith("disc", StringComparison.Ordinal) && stem[4] is >= '1' and <= '4' || stem.Length == 8 && stem.StartsWith("discimg", StringComparison.Ordinal) && stem[7] is >= '1' and <= '4')
            {
                return NativeDataResourceKind.CompressedImage;
            }
        }
        return null;
    }

    public static string? Group(ResourceKey key) => key.Path switch
    {
        "menu/mngrp.bin" or "menu/mngrphd.bin" => "menu/mngrp",
        "menu/icon.sp1" or "menu/icon.tex" => "menu/icons",
        "menu/pet_exp.bin" or "menu/pet_exp.msg" => "menu/pet-training",
        "menu/mtmag.bin" or "menu/mmag.bin" => "menu/tutorial-metadata",
        "menu/face.sp2" or "menu/face_b00.tex" or "menu/face_b01.tex" or "menu/gf_big00.tex" or "menu/gf_big01.tex" => "menu/portraits",
        _ => null
    };

    public static bool IsStandaloneRefinementTable(ResourceKey key) => key.Path is "menu/m000.bin" or "menu/m001.bin" or "menu/m002.bin" or "menu/m003.bin" or "menu/m004.bin";

    public static bool IsDisplayedImage(ResourceKey key)
    {
        string name = Path.GetFileName(key.Path);
        return key.Path == "main/ff8.lzs" || Classify(key) == NativeDataResourceKind.Texture && (name is "icon.tex" or "face_b00.tex" or "face_b01.tex" or "gf_big00.tex" or "gf_big01.tex" or "magita.tex" || name.StartsWith("mag", StringComparison.Ordinal) || name.StartsWith("mc", StringComparison.Ordinal));
    }
}
