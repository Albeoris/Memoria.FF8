using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDuel215ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable) => NativeBattleEffectFileDirectoryReader.Read(executable, new short[] { 360, 361, 362 });

    public static NativeBattleDuel215Resources Read(byte[] executable, NativeBattleEffectHeapAllocation initialTim, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        if (!memory.Read(0xFE0FB4, 3).AsSpan().SequenceEqual(new byte[] { 0x30, 0, 0 }))
        {
            throw new InvalidDataException("Native Duel215 child selectors differ from their original three-byte span.");
        }
        NativeBattleDuel215Resources result = new(memory.Read(NativeBattleDuel215Resources.Address, checked((int)(NativeBattleDuel215Resources.EndAddress - NativeBattleDuel215Resources.Address))).ToImmutableArray(), memory.ReadLoader(0x2508938, 0x74).ToImmutableArray(), initialTim, files);
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDuel215Resources? nativeDuel215Resources;

    public NativeBattleDuel215Resources ReadNativeDuel215Resources()
    {
        if (nativeDuel215Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            NativeBattleEffectHeapAllocation initialTim = new("mag214.tim", Read(magic, "mag214.tim").ToImmutableArray());
            using IFlArchive battle = OpenArchive("battle");
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleDuel215ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeDuel215Resources = NativeBattleDuel215ResourcesReader.Read(executable, initialTim, files);
        }
        return nativeDuel215Resources;
    }
}
