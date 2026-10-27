using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco100ProgramTests
{
    [Fact]
    public void Loaded_borrowed_source_scrolls_its_physical_owner_without_payload_export()
    {
        var profile = NativeBattleChoco100ProgramDrawing.LoadedProfile(NativeBattleChoco100LoadedProgram.File492);
        byte[] bytes = new byte[profile.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 32);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 160);
        foreach (int resource in new[] { 32, 160 })
        {
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(resource), 4);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(resource + 12), 1);
            bytes[resource + 27] = 250;
            bytes[resource + 29] = 10;
            bytes[resource + 33] = 20;
        }
        ImmutableArray<byte> expected = NativeBattleChoco97Uv.Scroll7311C0(bytes.ToImmutableArray(), 32, 12);
        expected = NativeBattleChoco97Uv.Scroll7311C0(expected, 160, 12);
        NativeBattleEffectArena arena = NativeBattleEffectArena.CreateUnknown(bytes.Length).Write(0, bytes);
        NativeBattleGfAnimationSource source = NativeBattleGfAnimationSource.Borrow(profile.Address, arena, 0, bytes.Length);
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, profile.Callback, new byte[0x540 - 12].ToImmutableArray()).WriteDword(0x74, profile.Address).WriteDword(0x78, (uint)profile.Clear).WriteByte(0x29, 2);
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);
        NativeBattleChoco99ProgramDraw result = NativeBattleChoco100ProgramDrawing.AdvanceLoaded(NativeBattleChoco100LoadedProgram.File492, task, source, new byte[8].ToImmutableArray(), new byte[8].ToImmutableArray(), 0, new byte[0x120].ToImmutableArray(), matrix, new(matrix, 216, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, 0);
        Assert.Equal(expected, arena.Read(0, bytes.Length).ToImmutableArray());
        NativeBattleOwnedGfAnimationSource.Own(result.Draw.Source).RequireStorage(arena, 0);
        Assert.Throws<InvalidOperationException>(() => result.Draw.Source.Data);
        NativeBattleChoco99ProgramDraw snapshot = NativeBattleStorageSnapshots.Capture(result);
        Assert.Equal(expected, snapshot.Draw.Source.Data);
        string json = System.Text.Json.JsonSerializer.Serialize(snapshot);
        NativeBattleChoco99ProgramDraw restored = NativeBattleStorageSnapshots.Restore(System.Text.Json.JsonSerializer.Deserialize<NativeBattleChoco99ProgramDraw>(json)!);
        using NativeBattleGfSourceReader reader = restored.Draw.Source.OpenReader();
        Assert.Equal(expected[59], reader.Byte(59));
    }

    [Fact]
    public void Header8000_skips_only_basis_and_direct200_also_skips_position_rotation()
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x8000);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 200);
        for (int offset = 24; offset <= 28; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        NativeBattleGfAnimationNode node = new(0, 0, bytes.ToImmutableArray());
        NativeBattleActionMatrix context = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), 1024) with { X = 10, Y = 20, Z = 30 };
        NativeBattleActionMatrix matrix = NativeBattleChoco100ProgramDrawing.ComposeNodeMatrix(node, context);
        Assert.Equal(NativeBattleCuragaMatrices.Identity8DD770().Rotation.ToArray(), matrix.Rotation.ToArray());
        Assert.Equal(210, matrix.X);
        Assert.Equal(20, matrix.Y);
        Assert.Equal(-70, matrix.Z);
        NativeBattleActionMatrix earlier = NativeBattleChoco97ProgramDrawing.ComposeNodeMatrix(node, context);
        Assert.Equal(context.Rotation.ToArray(), earlier.Rotation.ToArray());
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x8200);
        NativeBattleActionMatrix direct = NativeBattleChoco100ProgramDrawing.ComposeNodeMatrix(new(0, 0, bytes.ToImmutableArray()), context);
        Assert.Equal(110, direct.X);
        Assert.Equal(230, direct.Z);
    }

    [Theory]
    [InlineData(2, 32)]
    [InlineData(4, 28)]
    [InlineData(5, 36)]
    public void Custom_depth_uses_signed_bias_minimum16_and_retains_real_proxy_depths(int group, int bytes)
    {
        NativeBattleModuleProjectedVertex[] vertices = Corners(320, 216);
        NativeBattleGfGeometryBatch batch = NativeBattleChoco100DepthPackets.EmitFace(group, Face(group), index => vertices[index], Operands, -4096, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
        Assert.Equal(bytes, batch.AllocationBytes);
        Assert.Equal(21, primitive.Bucket);
        Assert.Equal(group == 5 ? new[] { 4096, 4096, 4096, 4096 } : new[] { 4096, 4096, 4096, 0 }, primitive.VertexPayload4);
        NativeBattleGfGeometryBatch positive = NativeBattleChoco100DepthPackets.EmitFace(group, Face(group), index => vertices[index], Operands, 16, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(group == 5 ? 277 : 276, Assert.Single(positive.Attempts).Primitive!.Bucket);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    public void Custom_screen_clips_after_prefix_while_normal_policy_remains_unchanged(int group)
    {
        NativeBattleModuleProjectedVertex[] vertices = Corners(320, 216).Select(vertex => vertex with { Screen = XY(321, 100) }).ToArray();
        NativeBattleGfGeometryBatch batch = NativeBattleChoco100DepthPackets.EmitFace(group, Face(group), index => vertices[index], Operands, -4096, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[8]);
        NativeBattleFullLifeMeshContext normal = new(0x30, null, null, null, null, null, 17, 2);
        NativeBattleGfGeometryBatch original = group switch
        {
            2 => NativeBattleRestorationMeshPackets.EmitFt3_884940(Face(group), index => vertices[index], normal),
            4 => NativeBattleRestorationMeshPackets.EmitG3_884E20(Face(group), index => vertices[index], normal),
            _ => NativeBattleRestorationMeshPackets.EmitG4_885050(Face(group), index => vertices[index], normal)
        };
        Assert.NotNull(Assert.Single(original.Attempts).Primitive);
        vertices[0] = vertices[0] with { Flags = 0x20000 };
        NativeBattleGfGeometryAttempt near = Assert.Single(NativeBattleChoco100DepthPackets.EmitFace(group, Face(group), index => index == 3 ? throw new InvalidOperationException() : vertices[index], Operands, -4096, NativeBattleGteColorInterpolation.CodeConstants45F270).Attempts);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, near.WriteMask[8]);
        if (group == 2)
        {
            Assert.Equal(NativeBattleGfPacketByteState.Known, near.WriteMask[12]);
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco100DepthPackets.EmitFace(7, new byte[36], index => vertices[index], Operands, -4096, NativeBattleGteColorInterpolation.CodeConstants45F270));
    }

    private static NativeBattleModuleMeshContext Operands => new(0x30, null, null, null, null, null, 17, 2);
    private static NativeBattleModuleProjectedVertex[] Corners(short x, short y) => [new(XY(0, 0), 4096, 0), new(XY(x, 0), 4096, 0), new(XY(0, y), 4096, 0), new(XY(x, y), 4096, 0)];
    private static uint XY(short x, short y) => unchecked((ushort)x) | (uint)unchecked((ushort)y) << 16;
    private static byte[] Face(int group)
    {
        byte[] bytes = new byte[group == 5 ? 24 : 20];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, group == 2 ? 0x24808080u : group == 4 ? 0x30808080u : 0x38808080u);
        return bytes;
    }
}
