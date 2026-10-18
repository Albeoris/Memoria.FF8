using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleChoco99ProgramResourcesReader
{
    public static NativeBattleChoco99ProgramResources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleChoco99ProgramFile>.Builder loaded = ImmutableArray.CreateBuilder<NativeBattleChoco99ProgramFile>(7);
        foreach ((short id, uint address, int length) in new (short, uint, int)[] { (471, 0x19E3018, 0x4EE0), (474, 0x19EB018, 0x3D08), (475, 0x19EED20, 0x2220), (476, 0x19F0F40, 0x1638), (482, 0x19DF380, 0x2CD4), (483, 0x19E2054, 0x2724), (484, 0x19E4778, 0x1034) })
        {
            NativeBattleEffectFileAsset[] matches = files.Where(file => file.FileId == id).ToArray();
            if (matches.Length != 1 || matches[0].Data.IsDefault || matches[0].Data.Length != length)
            {
                throw new InvalidDataException("Native Choco program requires its exact original loaded file owner.");
            }
            loaded.Add(new(id, new(address, matches[0].Data)));
        }
        return new(new(0x166DC58, memory.Read(0x166DC58, 0x2884).ToImmutableArray()), loaded.MoveToImmutable());
    }
}

public sealed partial class GameAssets
{
    private NativeBattleChoco99ProgramResources? nativeChoco99Program;

    public NativeBattleChoco99ProgramResources ReadNativeChoco99ProgramResources() => nativeChoco99Program ??= NativeBattleChoco99ProgramResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources), ReadNativeChoco99IoResources().Files);
}
