using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleHypnotize133ResourcesReader
{
    public static NativeBattleHypnotize133Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x1212B60, 11)) != "mag132.tim\0")
        {
            throw new InvalidDataException("Hypnotize requires its original initializer texture name.");
        }
        NativeBattleGfAnimationSource Act(uint address, int length) => new(address, memory.Read(address, length).ToImmutableArray());
        uint Pointer(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ImmutableArray<NativeBattleGfAnimationSource> anchored = Enumerable.Range(0, 8).Select(index => Act(Pointer(0x1212B20u + (uint)(index * 4)), 0xCB4)).ToImmutableArray();
        ImmutableArray<NativeBattleGfAnimationSource> fading = Enumerable.Range(0, 8).Select(index => Act(Pointer(0x1212B40u + (uint)(index * 4)), 0x70C)).ToImmutableArray();
        ImmutableArray<NativeBattleModuleAnchor> offsets = Enumerable.Range(0, 8).Select(index =>
        {
            byte[] bytes = memory.Read(0x1212AE0u + (uint)(index * 8), 8);
            return new NativeBattleModuleAnchor(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)));
        }).ToImmutableArray();
        NativeBattleHypnotize133Resources resources = new(new("FF8_EN:1212A30", memory.Read(0x1212A30, 0xAC).ToImmutableArray()), Act(0x1207958, 0x12D8), anchored, fading, offsets, new(0, 0x12077B8, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x12077B8)), Pointer(0x1212ADC));
        resources.Validate();
        return resources;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleHypnotize133Resources? nativeHypnotize133Resources;
    public NativeBattleHypnotize133Resources ReadNativeHypnotize133Resources() => nativeHypnotize133Resources ??= NativeBattleHypnotize133ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
