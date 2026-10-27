using System.Buffers.Binary;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStagePaletteTests
{
    [Fact]
    public void Original506CF0ReservesReal512BytePayloadWithoutCreatingOtPacketsAnd505DF0UploadsIt()
    {
        NativeBattlePrimitiveProcess before = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { ByteCursor = 28 };
        (NativeBattlePrimitiveProcess process, NativeBattleStagePaletteAllocation allocation) = NativeBattleStagePalette.Write506CF0(before);
        Assert.Equal(28, before.ByteCursor);
        Assert.Equal(540, process.ByteCursor);
        Assert.Equal(new NativeBattlePrimitiveReference("off_B6D070", 0, 28), allocation.Address);
        Assert.Equal(allocation.Source.Data.ToArray(), process.ReadCommonPacket(28, 512));
        Assert.Throws<NotSupportedException>(() => process.ReadCommonPacket(27, 1));
        NativeBattleEffectTextureState queued = NativeBattleEffectTextureState.FromColdProcess().EnqueueRectangle505DF0(new(0, 239, 256, 1), allocation.Source);
        Assert.Equal((byte)0, queued.Slots[0]!.Kind);
        NativeBattleEffectTextureState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectTextureState>(NativeBattleTestSnapshots.Json(queued))!;
        NativeBattleEffectTextureDrain drain = restored.Drain505D20();
        NativeBattleEffectVramUpload upload = Assert.Single(drain.Uploads);
        Assert.Equal(allocation.Source.Data.ToArray(), upload.Bytes.ToArray());
        Assert.Equal((short)239, upload.Y);
        Assert.Equal((short)256, upload.Width);
        Assert.Equal((short)1, upload.Height);
        byte[] row = drain.State.ReadVram().AsSpan(239 * 1024 * 2, 512).ToArray();
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(row));
        for (int index = 1; index < 256; index++)
        {
            Assert.Equal((ushort)0xC210, BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(2 * index)));
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleStagePalette.Write506CF0(before with { ByteCursor = before.CommonPacketWriteCapacity - 508 }));
        Assert.Throws<NotSupportedException>(() => NativeBattleStagePalette.Write506CF0(NativeBattlePrimitiveProcess.FromColdProcess()));
    }
}
