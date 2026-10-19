using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleStickyWeb153ResourcesReader
{
    public static NativeBattleStickyWeb153Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        byte[] table = memory.Read(0xDBE284, 56 * 4);
        ImmutableArray<int> selectors = Enumerable.Range(0, 56).Select(index => BinaryPrimitives.ReadInt32LittleEndian(table.AsSpan(index * 4))).ToImmutableArray();
        if (selectors.Any(selector => selector is < 0 or > 5))
        {
            throw new InvalidDataException("Native Sticky Web requires its original six-slot clipping selectors.");
        }
        return new(NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDBB33Cu), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xDBDBF4u), NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xDBB190), new("FF8_EN:DBAF3C", memory.Read(0xDBAF3C, 0x254).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xDBE27C, 4)), selectors);
    }
}
public sealed partial class GameAssets
{
    private NativeBattleStickyWeb153Resources? nativeStickyWeb153Resources;
    public NativeBattleStickyWeb153Resources ReadNativeStickyWeb153Resources() => nativeStickyWeb153Resources ??= NativeBattleStickyWeb153ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
