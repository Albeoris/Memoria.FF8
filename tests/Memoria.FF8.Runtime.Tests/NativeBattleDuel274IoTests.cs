using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274IoTests
{
    [Fact]
    public void Type16QueuesTwoCurrentPointerRelativeTexturesAndRetainsMutableSourcesAcrossSnapshot()
    {
        const int first = 512;
        const int second = first + 0x4220;
        NativeBattleEffectMemory memory = Begin(16);
        memory = memory with { Arena = memory.Arena!.Write(first, Tim(64, 0x3210)).Write(second, Tim(65, 0x7654)).MarkUnknown(second + 66, 10) };
        NativeBattleEffectIoCompletion result = NativeBattleEffectIo.Complete689810(memory, (_, _) => throw new InvalidOperationException("Actual TIM pointers are arena-backed."));

        Assert.Null(result.Stream);
        Assert.Equal(new byte[] { 0, 0, 1, 1, 1 }, result.Memory.Arena!.Read(64, 5));
        Assert.Equal(2, result.Memory.Textures!.Count);
        Assert.Equal(new int?[] { first, second }, result.Memory.Textures.Slots.Take(2).Select(slot => slot!.ArenaOffset));
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(result.Memory))!;
        NativeBattleEffectArena modified = restored.Arena!.Write(second + 64, [0xCD, 0xAB]);
        NativeBattleEffectTextureDrain drain = restored.Textures!.Drain505D20(modified);
        byte[] vram = drain.State.ReadVram();
        Assert.Equal((ushort)0x3210, BinaryPrimitives.ReadUInt16LittleEndian(vram.AsSpan(2 * (256 * 1024 + 64))));
        Assert.Equal((ushort)0xABCD, BinaryPrimitives.ReadUInt16LittleEndian(vram.AsSpan(2 * (256 * 1024 + 65))));
        Assert.Throws<NotSupportedException>(() => modified.Read(second + 66, 1));
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    public void StreamBranchUsesSharedAcknowledgmentAndOnlyActualLeadingDword(short type, byte delay)
    {
        NativeBattleEffectMemory memory = Begin(type);
        memory = memory with { Textures = null, Arena = memory.Arena!.Write(512, [119, 0, 0, 0]).MarkUnknown(516, 64) };
        NativeBattleEffectIoCompletion result = NativeBattleEffectIo.Complete689810(memory, (address, length) =>
        {
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 512, address);
            Assert.Equal(4, length);
            return new("current source", memory.Arena!.Read(512, length!.Value).ToImmutableArray());
        });

        Assert.Equal(new NativeBattleSummonStreamRequest(NativeBattleEffectNodeMemory.ArenaAddress + 512, 690119), result.Stream);
        Assert.Null(result.Memory.Textures);
        Assert.Equal(new byte[] { 0, delay, 1, 1, 1 }, result.Memory.Arena!.Read(64, 5));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena.Read(516, 1));
    }

    [Fact]
    public void CallbackIdentityAndMissingSecondTextureRemainExplicit()
    {
        NativeBattleEffectMemory memory = Begin(16);
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectIo.Complete689810(memory with { EffectIo = memory.EffectIo! with { Callback = 0x534150 } }, (_, _) => throw new InvalidOperationException()));
        memory = memory with { Arena = memory.Arena!.Write(512, Tim(64, 0)).MarkUnknown(512 + 0x4220, 66) };
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectIo.Complete689810(memory, (_, _) => throw new InvalidOperationException()));
    }

    private static NativeBattleEffectMemory Begin(short type)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectIo.Initialize534110(NativeBattleEffectMemory.FromColdProcess().BeginLoad(274), 64);
        memory = memory with { EffectIo = memory.EffectIo! with { Callback = 0x689810 } };
        memory = NativeBattleEffectIo.Queue5341D0(memory, 716, NativeBattleEffectNodeMemory.ArenaAddress + 512, type);
        return NativeBattleEffectIo.Advance534210(memory).Memory;
    }

    private static byte[] Tim(ushort x, ushort pixel)
    {
        byte[] data = new byte[66];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 16);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 44);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(18), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(52), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(56), x);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(58), 256);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(60), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(62), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(64), pixel);
        return data;
    }
}
