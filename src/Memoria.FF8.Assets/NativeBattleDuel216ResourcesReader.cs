using System.Collections.Immutable;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDuel216ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable) => NativeBattleEffectFileDirectoryReader.Read(executable, new short[] { 360, 361, 362 });

    public static NativeBattleDuel216Resources Read(byte[] executable, NativeBattleEffectHeapAllocation initialTim, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        NativeBattleDuel216Resources resources = new(memory.Read(NativeBattleDuel216Resources.Address, checked((int)(NativeBattleDuel216Resources.EndAddress - NativeBattleDuel216Resources.Address))).ToImmutableArray(), memory.ReadLoader(0x2508870, 192).ToImmutableArray(), initialTim, files) { SingleWindow = memory.Read(0xFE0FC8, 2).ToImmutableArray() };
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDuel216Resources? nativeDuel216Resources;

    public NativeBattleDuel216Resources ReadNativeDuel216Resources()
    {
        if (nativeDuel216Resources is null)
        {
            using IFlArchive magic = OpenArchive("magic");
            NativeBattleEffectHeapAllocation initialTim = new("mag215.tim", Read(magic, "mag215.tim").ToImmutableArray());
            using IFlArchive battle = OpenArchive("battle");
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleDuel216ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeDuel216Resources = NativeBattleDuel216ResourcesReader.Read(executable, initialTim, files);
        }
        return nativeDuel216Resources;
    }
}
