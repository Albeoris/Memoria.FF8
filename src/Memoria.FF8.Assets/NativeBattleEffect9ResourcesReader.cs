using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect9ResourcesReader
{
    public static NativeBattleEffect9Resources Read(byte[] executable, NativeBattleEffectInitializerAsset tim)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x1634F10, 11)) != "mag008.tim\0" || BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1634B7C, 4)) != NativeBattleEffect9State.ControlAddress)
        {
            throw new InvalidDataException("Effect9 requires its original initializer name and control pointer.");
        }
        NativeBattleEffect9Resources result = new(tim, new("FF8_EN:1634CB0", memory.Read(0x1634CB0, 134).ToImmutableArray()), new(0x18155F0, memory.Read(0x18155F0, 10488).ToImmutableArray()), new(0x1817EE8, memory.Read(0x1817EE8, 3216).ToImmutableArray()), new(0, 0x1634B84, 0x8C3F00, 0x8C4020, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x1634B84)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1634B80, 4)), memory.Read(0x1634EF4, 11).ToImmutableArray(), new(0x1634F00, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1634F00u + (uint)(index * 4), 4))).ToImmutableArray()));
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect9Resources? nativeEffect9Resources;

    public NativeBattleEffect9Resources ReadNativeEffect9Resources()
    {
        if (nativeEffect9Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive magic = OpenArchive("magic");
            NativeBattleEffectInitializerAsset tim = new("mag008.tim", Read(magic, "mag008.tim").ToImmutableArray());
            nativeEffect9Resources = NativeBattleEffect9ResourcesReader.Read(executable, tim);
        }
        return nativeEffect9Resources;
    }
}
