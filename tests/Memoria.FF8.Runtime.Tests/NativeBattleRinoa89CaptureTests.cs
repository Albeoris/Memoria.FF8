using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa89CaptureTests
{
    [Theory]
    [InlineData(0, 320)]
    [InlineData(320, 0)]
    public void CaptureUsesTheActualCurrentEnvironmentAndSeparatePrivateRoots(short priorOffset, short expectedOffset)
    {
        byte[] environment = new byte[92];
        BinaryPrimitives.WriteInt16LittleEndian(environment.AsSpan(8), priorOffset);
        BinaryPrimitives.WriteInt16LittleEndian(environment.AsSpan(10), 256);
        BinaryPrimitives.WriteInt16LittleEndian(environment.AsSpan(20), 0x12A);
        environment[22] = 1;
        environment[23] = 1;
        NativeBattleRinoa89Frame frame = Frame(environment.ToImmutableArray());
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleRinoa89CaptureStep begun = NativeBattleRinoa89Capture.Begin6FBEE0(frame, ordering);
        Assert.Equal(ordering.Table, begun.Ordering.Table);
        Assert.Equal(76, begun.Frame.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Equal(frame.Effect.DrawArenaOffset, begun.Frame.Effect.DrawArenaOffset);
        Assert.Equal(0xE100072Au, begun.Ordering.Packets[0].Words[1]);
        Assert.Equal(0xE2000000u, begun.Ordering.Packets[0].Words[2]);
        Assert.Equal(0xE3000000u | (uint)(ushort)expectedOffset | (256u << 10), begun.Ordering.Packets[1].Words[1]);
        Assert.Equal(0xE100052Au, begun.Ordering.Packets[1].Words[4]);
        Assert.Equal(0x1C48828u + 24, BinaryPrimitives.ReadUInt32LittleEndian(begun.Frame.Memory.Arena!.Read(NativeBattleRinoa89Capture.TableOffset + 4, 4)));
        Assert.Equal(uint.MaxValue, BinaryPrimitives.ReadUInt32LittleEndian(begun.Frame.Memory.Arena.Read(NativeBattleRinoa89Capture.TableOffset, 4)));
        Assert.Throws<NotSupportedException>(() => begun.Frame.Memory.PrimitiveProcess.ReadCommonPacket(12 + 24, 1));
        NativeBattleRinoa89Frame restored = JsonSerializer.Deserialize<NativeBattleRinoa89Frame>(JsonSerializer.Serialize(begun.Frame))!;
        NativeBattleRinoa89CaptureStep ended = NativeBattleRinoa89Capture.End6FBEE0(restored, begun.Ordering);
        Assert.Equal(140, ended.Frame.Memory.PrimitiveProcess!.ByteCursor);
        Assert.Equal(0xE3000000u | 384u | (256u << 10), ended.Ordering.Packets[2].Words[1]);
        Assert.Equal(0x02000000u, ended.Ordering.Packets[2].Words[6]);
        Assert.Equal(0x1C48828u + 48, BinaryPrimitives.ReadUInt32LittleEndian(ended.Frame.Memory.Arena!.Read(NativeBattleRinoa89Capture.TableOffset + 4385 * 4, 4)));
        Assert.Equal(frame.Effect.DrawArenaOffset, ended.Frame.Effect.DrawArenaOffset);
        Assert.All(ended.Ordering.Packets, packet => Assert.Equal("off_B6D070", packet.Reference.Bank));
    }

    [Fact]
    public void EnemyMatrixCopiesUnknownNativePaddingInsteadOfRetainingTheOldTemporaryWord()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(0x100000, Enumerable.Repeat((byte)0xA5, 76).ToArray());
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CE30(new(0, 0, 0)) with { X = 17, Y = -23, Z = 456 };
        staging = NativeBattleRinoa89Capture.PublishMatrix56C2F0(staging, 0x100000, matrix);
        Assert.Throws<NotSupportedException>(() => staging.Read(0x100012, 2));
        Assert.Equal(17, BinaryPrimitives.ReadInt32LittleEndian(staging.Read(0x100014, 4)));
        Assert.Equal(-23, BinaryPrimitives.ReadInt32LittleEndian(staging.Read(0x100018, 4)));
        Assert.Equal(456, BinaryPrimitives.ReadInt32LittleEndian(staging.Read(0x10001C, 4)));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 44).ToArray(), staging.Read(0x100020, 44));
    }

    [Fact]
    public void MissingCurrentGlobalDoesNotSubstituteAnAlternateEnvironment()
    {
        NativeBattleRinoa89Frame frame = Frame(null);
        Assert.Throws<NotSupportedException>(() => NativeBattleRinoa89Capture.Begin6FBEE0(frame, NativeBattleLogicalOrdering.Clear()));
    }

    private static NativeBattleRinoa89Frame Frame(ImmutableArray<byte>? environment)
    {
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        process = process with { Software = process.Software! with { EnvironmentBuffers = NativeSoftwareDrawingEnvironmentBuffers.Initialize500400(environment) } };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = process };
        NativeBattleOriginalEffectState effect = new(89, 88, new(89, 0x6F9CF0, 0x5755E0), new(0, 0, 8, 0, 0, 0), NativeBattleRinoa89Controller.Root) { DrawArenaOffset = 108684 };
        return new(memory, effect, NativeBattleFileStaging.Unknown(), new(1));
    }
}
