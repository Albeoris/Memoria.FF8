using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTornado146PiecesTests
{
    [Fact]
    public void Native_piece_motion_distinguishes_signed_division_from_arithmetic_shift_and_releases_before_counter_advance_above_ceiling()
    {
        NativeBattleModulePoolTask task = Task().WriteWord(12, 6).WriteWord(22, unchecked((ushort)-34));
        task = NativeBattleTornado146Pieces.Move(task, -1000, out bool complete);
        Assert.False(complete);
        Assert.Equal((short)-17, unchecked((short)task.ReadWord(18)));
        Assert.Equal((short)-18, unchecked((short)task.ReadWord(22)));
        Assert.Equal((ushort)5, task.ReadWord(24));
        Assert.Equal((ushort)7, task.ReadWord(12));
        task = task.WriteWord(18, unchecked((ushort)-4999)).WriteWord(22, unchecked((ushort)-17));
        task = NativeBattleTornado146Pieces.Move(task, -1000, out complete);
        Assert.True(complete);
        Assert.Equal((ushort)7, task.ReadWord(12));
        Assert.Equal((short)-5016, unchecked((short)task.ReadWord(18)));
    }

    [Fact]
    public void Native_grounded_packet_accepts_backface_and_full_DWORD_UV_tail_and_selects_the_actual_stage_table_from_light_IR1()
    {
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 4096 };
        NativeBattleCameraProjection projection = new(view, 320, 160, 112);
        NativeBattleModulePoolTask task = Task();
        NativeBattleGfGeometryBatch batch = NativeBattleTornado146Pieces.Draw(Mesh(), task, -500, 0, 0, view, projection);
        NativeBattleGeometryPrimitive face = Assert.Single(batch.Attempts).Primitive!;
        Assert.Equal(32, batch.AllocationBytes);
        Assert.Equal(17 + (1023 >> 2), face.Bucket);
        Assert.Equal(0x24808080u, face.Words[1]);
        Assert.Equal(0x1234u, face.Words[7]);
        Assert.All(batch.Attempts[0].WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        batch = NativeBattleTornado146Pieces.Draw(Mesh(), task.WriteWord(18, 200), -500, 0, 0, view, projection);
        Assert.Equal(4122 + (1023 >> 6), Assert.Single(batch.Attempts).Primitive!.Bucket);
        NativeBattleModulePoolTask independent = NativeBattleTestSnapshots.RestoreJson<NativeBattleModulePoolTask>(NativeBattleTestSnapshots.Json(task))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleTornado146Pieces.Draw(Mesh(), task, -500, 0, 0, view, projection)), NativeBattleTestSnapshots.Json(NativeBattleTornado146Pieces.Draw(Mesh(), independent, -500, 0, 0, view, projection)));
    }

    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 0x6155B0, new byte[24].ToImmutableArray());

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] bytes = new byte[84];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        short[] points = [-100, -100, 0, 0, 100, -100, 0, 0, 0, 100, 0, 0];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), points[index]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), 0x24808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(48), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(50), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(54), 0x1234);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), 0x3F940000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 0x00B70000);
        return new(0, 0x1000000, bytes.ToImmutableArray());
    }
}
