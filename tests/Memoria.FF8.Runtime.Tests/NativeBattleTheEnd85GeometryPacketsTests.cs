using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTheEnd85GeometryPacketsTests
{
    private const uint Mesh = 0x156C7FC;
    private const uint Context = NativeBattleTemporaryMemory.Address;
    private static readonly int[] Lengths = [12, 12, 20, 24, 20, 24, 28, 36];
    private static readonly byte[] Commands = [0x20, 0x28, 0x24, 0x2C, 0x30, 0x38, 0x34, 0x3C];

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 24)]
    [InlineData(2, 32)]
    [InlineData(3, 40)]
    [InlineData(4, 28)]
    [InlineData(5, 36)]
    [InlineData(6, 40)]
    [InlineData(7, 52)]
    public void Main_dispatcher_uses_each_proved_group_and_retains_current_native_context(int group, int allocation)
    {
        byte[] source = Source(group);
        NativeBattleTheEnd85MeshStep result = Emit(source, Initial(), false);
        Assert.Equal(allocation, result.Packets.AllocationBytes);
        Assert.NotNull(Assert.Single(result.Packets.Attempts).Primitive);
        Assert.Equal(Mesh + (uint)source.Length, Dword(result.Staging, 44));
        Assert.Equal(Mesh + 8, Dword(result.Staging, 4));
        Assert.Equal(0u, Dword(result.Staging, 60));
        Assert.Equal((uint?)0x110, result.FarRed);
        Assert.Equal((uint?)0x220, result.FarGreen);
        Assert.Equal((uint?)0x330, result.FarBlue);
        Assert.Throws<NotSupportedException>(() => result.Staging.Read(Context + 11, 1));
        Assert.Throws<NotSupportedException>(() => result.Staging.Read(Context + 92, 4));
    }

    [Fact]
    public void Title_threshold_applies_after_depth_cue_and_turns_each_red_difference_into_gray()
    {
        byte[] source = Source(7);
        SetColors(source, 5, 6, 7, 20);
        NativeBattleTheEnd85MeshStep result = Emit(source, Initial().Write(Context + 100, BitConverter.GetBytes((short)5)), true);
        NativeBattleGeometryPrimitive primitive = Assert.Single(result.Packets.Attempts).Primitive!;
        Assert.Equal(52, result.Packets.AllocationBytes);
        Assert.Equal(0x3C000000u, primitive.Words[1]);
        Assert.Equal(0x3C101010u, primitive.Words[4]);
        Assert.Equal(0x3C202020u, primitive.Words[7]);
        Assert.Equal(0x3C808080u, primitive.Words[10]);
        Assert.True(Dword(result.Staging, 56) > 0);
    }

    [Fact]
    public void Threshold_rejection_keeps_full_prefix_and_colors_without_allocating_or_linking()
    {
        byte[] source = Source(7);
        SetColors(source, 4, 6, 7, 20);
        NativeBattleTheEnd85MeshStep result = Emit(source, Initial().Write(Context + 100, BitConverter.GetBytes((short)5)), true);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Packets.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, result.Packets.AllocationBytes);
        Assert.Equal((byte)4, attempt.Data[4]);
        Assert.Equal((byte)6, attempt.Data[16]);
        Assert.All(attempt.WriteMask, mask => Assert.Equal(NativeBattleGfPacketByteState.Known, mask));
        Assert.True(Dword(result.Staging, 56) > 0);
    }

    [Fact]
    public void Nonzero_UV_selects_native_window_brackets_and_never_reads_unknown_threshold()
    {
        byte[] source = Source(7);
        int face = Face(7);
        source[face + 12] = 250;
        source[face + 16] = 240;
        source[face + 20] = 230;
        source[face + 22] = 220;
        NativeBattleFileStaging staging = Initial().Write(Context + 24, BitConverter.GetBytes((ushort)16)).Write(Context + 40, BitConverter.GetBytes((ushort)128));
        NativeBattleTheEnd85MeshStep result = Emit(source, staging, true);
        Assert.Equal(76, result.Packets.AllocationBytes);
        Assert.Equal(4, result.Packets.Attempts.Length);
        NativeBattleGfGeometryAttempt stores = result.Packets.Attempts[0];
        Assert.Equal(new byte[] { 138, 128, 118, 108 }, new[] { stores.Data[12], stores.Data[24], stores.Data[36], stores.Data[48] });
        Assert.Equal(52, result.Packets.Attempts[1].ByteOffsetRelative);
        Assert.Equal(0, result.Packets.Attempts[2].ByteOffsetRelative);
        Assert.Equal(64, result.Packets.Attempts[3].ByteOffsetRelative);
        Assert.Equal(0xE2000000u, result.Packets.Attempts[1].Primitive!.Words[1]);
        Assert.Equal(0xE2000010u, result.Packets.Attempts[3].Primitive!.Words[1]);
        Assert.Throws<NotSupportedException>(() => result.Staging.Read(Context + 100, 2));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(-32768, false)]
    public void Threshold_subtraction_tests_the_wrapped_signed_low_WORD(short threshold, bool accepted)
    {
        byte[] source = Source(7);
        SetColors(source, 0, 0, 0, 0);
        NativeBattleTheEnd85MeshStep result = Emit(source, Initial().Write(Context + 100, BitConverter.GetBytes(threshold)), true);
        Assert.Equal(accepted ? 52 : 0, result.Packets.AllocationBytes);
        if (accepted)
        {
            Assert.Equal(0x3C101010u, Assert.Single(result.Packets.Attempts).Primitive!.Words[1]);
        }
    }

    [Fact]
    public void Selected_vertex_pointer_is_preserved_and_only_required_XYZ_bytes_are_read()
    {
        byte[] source = Source(7);
        NativeBattleFileStaging staging = Initial().Write(Context + 4, BitConverter.GetBytes(Mesh + 8)).Write(Context + 20, BitConverter.GetBytes(0x20F0u));
        List<(uint Address, int Count)> reads = [];
        NativeBattleTheEnd85MeshStep result = NativeBattleTheEnd85GeometryPackets.Emit785A90(staging, Context, View(), Projection(), NativeBattleGteColorInterpolation.CodeConstants45F270, (address, count) =>
        {
            reads.Add((address, count));
            return source.AsSpan(checked((int)(address - Mesh)), count).ToArray();
        });
        Assert.Equal(Mesh + 8, Dword(result.Staging, 4));
        Assert.Equal(new[] { Mesh + 8, Mesh + 16, Mesh + 24, Mesh + 32 }, reads.Where(read => read.Count == 6).Select(read => read.Address));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Presentation_all_authored_groups_capture_actual_selected_corners_and_preserve_native_context_and_packets(int group)
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 5, 7, 4096], 0, 0, 4000, 4096);
        var native = Emit(Source(group), Initial(), false, matrix: matrix);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Emit(Source(group), Initial(), false, new(85, 1, 2, 3, 4), matrix);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        var primitive = Assert.Single(captured.Packets.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!;
        var geometry = primitive.PilotGeometry!;
        Assert.Equal(new System.Numerics.Vector3(-100, -100, 100 - 1200f / 4096), geometry.A);
        Assert.Equal(new System.Numerics.Vector3(100, -100, 100 - 200f / 4096), geometry.B);
        Assert.Equal(new System.Numerics.Vector3(-100, 100, 100 + 200f / 4096), geometry.C);
        Assert.Equal((group & 1) == 0 ? 3 : 4, geometry.CornerCount);
        if (geometry.CornerCount == 4)
        {
            Assert.Equal(new System.Numerics.Vector3(100, 100, 100 + 1200f / 4096), geometry.D);
        }
        var finalized = NativeBattleTheEnd85Presentation.Depths(captured.Packets);
        var retained = Assert.Single(finalized.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!;
        Assert.True(retained.PilotGeometry!.UseSourceDepths);
        Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), retained.PilotGeometry.SourceDepths);
        Assert.All(native.Packets.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
    }

    [Fact]
    public void Presentation_title_threshold_window_packets_keep_state_commands_unannotated_and_native_words_unchanged()
    {
        byte[] SourceWithUvs()
        {
            byte[] bytes = Source(7);
            bytes[Face(7) + 12] = 250;
            bytes[Face(7) + 16] = 240;
            return bytes;
        }
        NativeBattleFileStaging ContextWithUvs() => Initial().Write(Context + 24, BitConverter.GetBytes((ushort)16)).Write(Context + 40, BitConverter.GetBytes((ushort)128));
        var native = Emit(SourceWithUvs(), ContextWithUvs(), true);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Emit(SourceWithUvs(), ContextWithUvs(), true, new(85, 1, 2, 3, 4));
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Single(captured.Packets.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null);
        Assert.All(captured.Packets.Attempts.Where(attempt => attempt.Primitive is { } primitive && (primitive.Words[1] >> 24 & 0xE0) == 0xE0), attempt => Assert.Null(attempt.Primitive!.PilotGeometry));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Presentation_rejected_first_triple_preserves_native_deferred_fourth_load(bool capture)
    {
        byte[] source = Source(3);
        for (int corner = 0; corner < 3; corner++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(12 + corner * 8), -5000);
        }
        List<(uint Address, int Count)> reads = [];
        var result = NativeBattleTheEnd85GeometryPackets.Emit785A90(Initial(), Context, View(), Projection(), NativeBattleGteColorInterpolation.CodeConstants45F270, (address, count) =>
        {
            reads.Add((address, count));
            if (address == Mesh + 32 && count == 6)
            {
                throw new InvalidOperationException("Native fourth corner must stay unread after rejected first triple.");
            }
            return source.AsSpan(checked((int)(address - Mesh)), count).ToArray();
        }, pilotIdentity: capture ? new(85, 1, 2, 3, 4) : null);
        Assert.Null(Assert.Single(result.Packets.Attempts).Primitive);
        Assert.DoesNotContain(reads, read => read.Address == Mesh + 32 && read.Count == 6);
    }

    private static NativeBattleTheEnd85MeshStep Emit(byte[] source, NativeBattleFileStaging staging, bool threshold, NativeBattlePilotVisualIdentity? pilotIdentity = null, NativeBattleActionMatrix? matrix = null)
    {
        byte[] Read(uint address, int length) => source.AsSpan(checked((int)(address - Mesh)), length).ToArray();
        return threshold ? NativeBattleTheEnd85GeometryPackets.Emit78C660(staging, Context, matrix ?? View(), Projection(), NativeBattleGteColorInterpolation.CodeConstants45F270, Read, pilotIdentity: pilotIdentity) : NativeBattleTheEnd85GeometryPackets.Emit785A90(staging, Context, matrix ?? View(), Projection(), NativeBattleGteColorInterpolation.CodeConstants45F270, Read, pilotIdentity: pilotIdentity);
    }

    private static NativeBattleFileStaging Initial()
    {
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(Context, BitConverter.GetBytes(Mesh)).Write(Context + 8, new byte[] { 0x11, 0x22, 0x33 }).Write(Context + 12, BitConverter.GetBytes(0)).Write(Context + 16, BitConverter.GetBytes(-256)).Write(Context + 20, BitConverter.GetBytes(0xF0u));
        for (int offset = 24; offset < 44; offset += 2)
        {
            staging = staging.Write(Context + (uint)offset, BitConverter.GetBytes((ushort)(offset is 32 or 34 or 40 or 42 ? 256 : 0)));
        }
        return staging;
    }

    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4000, 4096);
    private static NativeBattleCameraProjection Projection() => new(View(), 160, 160, 108);
    private static uint Dword(NativeBattleFileStaging staging, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(Context + (uint)offset, 4));
    private static int Face(int group) => 40 + (group + 1) * 4;

    private static void SetColors(byte[] bytes, byte first, byte second, byte third, byte fourth)
    {
        int face = Face(7);
        byte[] red = [first, second, third, fourth];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + (corner == 0 ? 0 : 20 + corner * 4)), 0x3C665500u | red[corner]);
        }
    }

    private static byte[] Source(int group)
    {
        byte[] bytes = new byte[40 + 32 + Lengths[group]];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        NativeBattleVector[] vertices = [new(-100, -100, 100), new(100, -100, 100), new(-100, 100, 100), new(100, 100, 100)];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + corner * 8), vertices[corner].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + corner * 8), vertices[corner].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + corner * 8), vertices[corner].Z);
        }
        int face = Face(group);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), (uint)Commands[group] << 24 | 0x808080);
        for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        if (group >= 4)
        {
            int start = (group & 2) == 0 ? 12 : group == 7 ? 24 : 20;
            for (int corner = 1; corner < ((group & 1) == 0 ? 3 : 4); corner++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + start + (corner - 1) * 4), 0x3C808080);
            }
        }
        return bytes;
    }
}
