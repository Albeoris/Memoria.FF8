using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleConfuse36ResourcesReader
{
    public static NativeBattleConfuse36Resources Read(byte[] executable)
    {
        ImmutableArray<byte> cameraHeader = NativeBattleEffectStaticResourcesReader.Read(executable, 0x15E5674, 8);
        int cameraLength = BinaryPrimitives.ReadUInt16LittleEndian(cameraHeader.AsSpan().Slice(6));
        if (BinaryPrimitives.ReadUInt16LittleEndian(cameraHeader.AsSpan()) != 2 || 0x15E5674 + cameraLength != 0x15E637C)
        {
            throw new InvalidDataException("Native Confuse camera does not end at its original asset name.");
        }
        ImmutableArray<byte> bank = NativeBattleEffectStaticResourcesReader.Read(executable, 0x15D97FC, 0xBE78);
        if (BinaryPrimitives.ReadUInt32LittleEndian(bank.AsSpan().Slice(0xBD74)) != 1)
        {
            throw new InvalidDataException("Native Confuse requires its original already relocated actor directory.");
        }
        ImmutableArray<byte> overlay = NativeBattleEffectStaticResourcesReader.Read(executable, 0x15D97EC, 12);
        ImmutableArray<byte> shake = NativeBattleEffectStaticResourcesReader.Read(executable, 0x15D97E0, 10);
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x15D97F8, 4).AsSpan());
        NativeExecutableMemory memory = new(executable);
        return new(new("FF8_EN:15E5674", NativeBattleEffectStaticResourcesReader.Read(executable, 0x15E5674, cameraLength)), sound, bank, Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(overlay.AsSpan().Slice(index * 4))).ToImmutableArray(), Enumerable.Range(0, 5).Select(index => BinaryPrimitives.ReadInt16LittleEndian(shake.AsSpan().Slice(index * 2))).ToImmutableArray()) { ConstantChannels = memory.ReadLoader(0x26A9BC4, 0x2BBC).ToImmutableArray() };
    }
}

public sealed partial class GameAssets
{
    private NativeBattleConfuse36Resources? nativeConfuse36Resources;

    public NativeBattleConfuse36Resources ReadNativeConfuse36Resources() => nativeConfuse36Resources ??= NativeBattleConfuse36ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
