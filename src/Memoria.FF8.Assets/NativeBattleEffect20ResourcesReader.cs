using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleEffect20ResourcesReader
{
    public static NativeBattleEffect20Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        byte[] offsets = memory.Read(0x1629F28, 12 * 8);
        byte[] ages = memory.Read(0x1629F88, 10 * 2);
        ImmutableArray<NativeBattleVector>.Builder emissions = ImmutableArray.CreateBuilder<NativeBattleVector>(12);
        for (int index = 0; index < 12; index++)
        {
            emissions.Add(new(BinaryPrimitives.ReadInt16LittleEndian(offsets.AsSpan(index * 8)), BinaryPrimitives.ReadInt16LittleEndian(offsets.AsSpan(index * 8 + 2)), BinaryPrimitives.ReadInt16LittleEndian(offsets.AsSpan(index * 8 + 4))));
        }
        NativeVibrationBank vibration = new(memory.SourceHash, [0, 0x100], memory.Read(0x17F36A0, 0x11A).ToImmutableArray());
        NativeBattleEffect20Resources resources = new(new("FF8_EN:1629E60", memory.Read(0x1629E60, 180).ToImmutableArray()), U32(0x1629C50), new(0x1629F14, Enumerable.Range(0, 4).Select(index => U32(0x1629F14u + (uint)index * 4)).ToImmutableArray()), Enumerable.Range(0, 10).Select(index => BinaryPrimitives.ReadInt16LittleEndian(ages.AsSpan(index * 2))).ToImmutableArray(), emissions.MoveToImmutable(), new(0x17F36A0, vibration), NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0x1629E00u), new(0, 0x1629C54, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x1629C54)));
        resources.Validate();
        return resources;

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleEffect20Resources? nativeEffect20Resources;

    public NativeBattleEffect20Resources ReadNativeEffect20Resources()
    {
        return nativeEffect20Resources ??= NativeBattleEffect20ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
    }
}
