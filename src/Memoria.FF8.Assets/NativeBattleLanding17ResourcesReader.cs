using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleLanding17ResourcesReader
{
    public static NativeBattleLanding17Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> Read(uint address, int count) => NativeBattleEffectStaticResourcesReader.Read(memory, address, count);
        uint UInt(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        NativeBattleGfAnimationSource animation = new(0x14D734C, Read(0x14D734C, 0x14D8BAC - 0x14D734C));
        int table = BinaryPrimitives.ReadInt32LittleEndian(animation.Data.AsSpan().Slice(4));
        int nodes = BinaryPrimitives.ReadInt32LittleEndian(animation.Data.AsSpan().Slice(table));
        if (nodes is < 0 or > 1024)
        {
            throw new InvalidDataException("Native landing animation node table exceeds its original bounds.");
        }
        ImmutableArray<uint> flags = Enumerable.Range(0, nodes).Select(index => UInt(checked(0x14FA778u + (uint)(index * 4)))).ToImmutableArray();
        ushort cameraLength = BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(0x14D8BAC + 6, 2));
        NativeBattleEffectHeapAllocation camera = new("FF8_EN:14D8BAC", Read(0x14D8BAC, cameraLength));
        ImmutableArray<byte> vibrationData = Read(0x14D8E48, 0x110);
        ImmutableArray<int> offsets = Enumerable.Range(0, 64).Select(index => BinaryPrimitives.ReadInt32LittleEndian(vibrationData.AsSpan().Slice(index * 4))).ToImmutableArray();
        NativeVibrationBank vibration = new(memory.SourceHash, offsets, vibrationData);
        vibration.Validate();
        int first = checked((int)UInt(0x14D8F58 + 8));
        int timLength = checked(8 + first);
        if ((UInt(0x14D8F58 + 4) & 8) != 0)
        {
            timLength = checked(timLength + (int)UInt(checked(0x14D8F58u + (uint)timLength)));
        }
        if (UInt(0x14D8F58) != 16 || first < 12 || timLength is < 20 or > 0x100000)
        {
            throw new InvalidDataException("Native landing requires its complete original TIM blocks.");
        }
        return new(new("FF8_EN:14D8F58", Read(0x14D8F58, timLength)), camera, new(0, 0x14D71EC, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x14D71EC)), animation, flags, vibration, UInt(0x14D8E40), UInt(0x14D8E44), UInt(0xB8B9A8), BinaryPrimitives.ReadUInt32LittleEndian(memory.ReadLoader(0x2556FF8, 4)));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleLanding17Resources? nativeLanding17Resources;
    public NativeBattleLanding17Resources ReadNativeLanding17Resources() => nativeLanding17Resources ??= NativeBattleLanding17ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
