using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleXatmDeath276ResourcesReader
{
    public static NativeBattleXatmDeath276Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> Read(uint address, int length) => NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
        ushort cameraLength = BinaryPrimitives.ReadUInt16LittleEndian(Read(0x10D4A7E, 2).AsSpan());
        NativeBattleEffectHeapAllocation camera = new("FF8_EN:10D4A78", Read(0x10D4A78, cameraLength));
        ImmutableArray<NativeBattleModuleResource> sprites = new uint[] { 0x10D2EA0, 0x10D304C, 0x10D31C4 }.Select(address => new NativeBattleModuleResource(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        NativeBattleGfAnimationSource prim = new(0x10D3364, Read(0x10D3364, checked((int)(0x10D4A78 - 0x10D3364))));
        ImmutableArray<NativeBattleXatmDeath276Table>.Builder events = ImmutableArray.CreateBuilder<NativeBattleXatmDeath276Table>();
        foreach (uint address in new uint[] { 0x10D56A8, 0x10D55A8, 0x10D5528, 0x10D5628, 0x10D5728 })
        {
            ImmutableArray<byte> data = Read(address, 128);
            uint triangles = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(0x7C));
            int count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(0x78));
            if (BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan().Slice(12)) != 4096)
            {
                throw new NotSupportedException("Native6860B0 requires its original identity-scale event records.");
            }
            events.Add(new(address, data, triangles, Read(triangles, checked(count * 24))));
        }
        ImmutableArray<byte> vibrationData = Read(0x10D4C5C, checked((int)(0x10D4DA0 - 0x10D4C5C)));
        ImmutableArray<int> vibrationOffsets = Enumerable.Range(0, 64).Select(index => BinaryPrimitives.ReadInt32LittleEndian(vibrationData.AsSpan().Slice(index * 4))).ToImmutableArray();
        if (vibrationOffsets[0] != 0 || vibrationOffsets[1] != 0x100 || vibrationOffsets.Skip(2).Any(offset => offset != 0))
        {
            throw new InvalidDataException("Native684FA0 requires its original null and program1 vibration directory.");
        }
        NativeVibrationBank vibration = new(memory.SourceHash, vibrationOffsets, vibrationData);
        vibration.Validate();
        ImmutableArray<byte> effectCamera = memory.ReadLoader(0x2508328, 32).ToImmutableArray();
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(effectCamera.AsSpan().Slice(index * 2))).ToImmutableArray();
        NativeBattleActionMatrix matrix = new(rotation, BinaryPrimitives.ReadInt32LittleEndian(effectCamera.AsSpan().Slice(20)), BinaryPrimitives.ReadInt32LittleEndian(effectCamera.AsSpan().Slice(24)), BinaryPrimitives.ReadInt32LittleEndian(effectCamera.AsSpan().Slice(28)), 4096);
        ImmutableArray<byte> triangleLengths = Read(0x1524458, 512);
        ImmutableArray<ushort> lookup = Enumerable.Range(0, 256).Select(index => BinaryPrimitives.ReadUInt16LittleEndian(triangleLengths.AsSpan().Slice(index * 2))).ToImmutableArray();
        return new(camera, sprites, prim, events.ToImmutable(), Read(0x10D4DA0, 56), Read(0x10D9728, 512), Read(0x10D57A8, 16384), lookup, vibration, BinaryPrimitives.ReadUInt32LittleEndian(Read(0x10D4C58, 4).AsSpan()), matrix);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleXatmDeath276Resources? nativeXatmDeath276Resources;
    public NativeBattleXatmDeath276Resources ReadNativeXatmDeath276Resources() => nativeXatmDeath276Resources ??= NativeBattleXatmDeath276ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
