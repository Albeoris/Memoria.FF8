using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco97ProgramDrawingTests
{
    private static NativeBattleActionMatrix Context => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 4096, 4096);
    private static NativeBattleCameraProjection Projection => new(Context, 216, 160, 108);
    private static NativeBattleSummonColors Colors => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Theory]
    [InlineData(0x4AC)]
    [InlineData(0x44C)]
    [InlineData(0x170)]
    [InlineData(0x290)]
    public void Admitted_initialization_clears_only_requested_DWORD_span_and_keeps_the_raw_origin(int clearBytes)
    {
        byte[] sourceBytes = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(sourceBytes.AsSpan(4), 8);
        NativeBattleGfAnimationSource source = new(0x1000, sourceBytes.ToImmutableArray());
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, 0x72A660, Enumerable.Repeat((byte)0xA5, 0x540 - 12).ToImmutableArray()).WriteDword(0x74, source.Address).WriteDword(0x78, (uint)clearBytes);
        NativeBattleModulePoolTask initialized = NativeBattleChoco97ProgramDrawing.InitializeWorkspace72A6C0(task, source, [1, 0, 2, 0, 3, 0, 4, 0]);
        int start = 0x94 - 12;
        Assert.Equal(source.Address, initialized.ReadDword(0x94));
        ImmutableArray<byte> initializedPayload = NativeBattleStorageSnapshots.Capture(initialized).Payload;
        Assert.All(initializedPayload.Skip(start + 4).Take(clearBytes - 4), value => Assert.Equal((byte)0, value));
        Assert.All(initializedPayload.Skip(start + clearBytes), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal((ushort)2, initialized.ReadWord(0x1E));
        Assert.Equal((ushort)4, initialized.ReadWord(0x22));
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco97ProgramDrawing.InitializeWorkspace72A6C0(task.WriteDword(0x78, 0x171), source, new byte[8].ToImmutableArray()));
    }

    [Fact]
    public void Header200_bypasses_basis_for_position_and_rotation_and_40000_selects_the_actual_Euler_order()
    {
        NativeBattleActionMatrix basis = Context with { Rotation = [8192, 4096, 0, 0, 4096, 0, 0, 0, 4096] };
        NativeBattleGfAnimationNode node = Node(0x40200);
        NativeBattleActionMatrix direct = NativeBattleChoco97ProgramDrawing.ComposeNodeMatrix(node, basis);
        Assert.Equal((30100, 20200, 4096), (direct.X, direct.Y, direct.Z));
        Assert.Equal(NativeBattleGfMatrices.Euler7015B0(node.Vector16).Rotation.ToArray(), direct.Rotation.ToArray());
        NativeBattleActionMatrix transformed = NativeBattleChoco97ProgramDrawing.ComposeNodeMatrix(Node(0x40000), basis);
        Assert.Equal((80100, 20200, 4096), (transformed.X, transformed.Y, transformed.Z));
        NativeBattleActionMatrix ordinary = NativeBattleChoco97ProgramDrawing.ComposeNodeMatrix(Node(0x200), basis);
        NativeBattleActionMatrix unused400 = NativeBattleChoco97ProgramDrawing.ComposeNodeMatrix(Node(0x600), basis);
        Assert.Equal(ordinary.Rotation.ToArray(), unused400.Rotation.ToArray());
        Assert.NotEqual(ordinary.Rotation.ToArray(), direct.Rotation.ToArray());
    }

    [Theory]
    [InlineData(0u, 1)]
    [InlineData(0x100u, 2)]
    public void Column_scale_and_rounded_matrix_diagonal_keep_distinct_halfway_results(uint header, int expected)
    {
        NativeBattleGfAnimationNode node = Node(header, angles: new(0, 0, 0), scale: new(2048, 4096, 4096));
        NativeBattleActionMatrix actual = NativeBattleChoco97ProgramDrawing.ComposeNodeMatrix(node, Context with { Rotation = [3, 0, 0, 0, 4096, 0, 0, 0, 4096] });
        Assert.Equal((short)expected, actual.Rotation[0]);
    }

    [Fact]
    public void Partial_morph_writes_only_XYZ_and_retains_W_extra_scratch_and_authored_source()
    {
        NativeBattleGfAnimationSource source = Source();
        ImmutableArray<byte> scratch = Enumerable.Repeat((byte)0xA5, 0x3C0).ToImmutableArray();
        NativeBattleChoco97NodeDraw actual = NativeBattleChoco97ProgramDrawing.Node72A840(source, Node(0, geometry: 0, second: 1, ratio: 2048), scratch, Context, Projection, Colors);
        Assert.Equal((short)200, BinaryPrimitives.ReadInt16LittleEndian(actual.MorphScratch.AsSpan()));
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(actual.MorphScratch.AsSpan().Slice(2)));
        Assert.Equal((short)200, BinaryPrimitives.ReadInt16LittleEndian(actual.MorphScratch.AsSpan().Slice(4)));
        Assert.All(actual.MorphScratch.Skip(6), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(Source().Data.ToArray(), source.Data.ToArray());
        NativeBattleChoco97NodeDraw direct = NativeBattleChoco97ProgramDrawing.Node72A840(source, Node(0, geometry: 0, second: 1, ratio: 4096), scratch, Context, Projection, Colors);
        Assert.Equal(scratch.ToArray(), direct.MorphScratch.ToArray());
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco97ProgramDrawing.Node72A840(source, Node(0, geometry: 0, second: 1, ratio: 2048), default, Context, Projection, Colors));
        NativeBattleChoco97NodeDraw skipped = NativeBattleChoco97ProgramDrawing.Node72A840(new(0, []), Node(0, scale: new(0, 0, 0)), default, Context, Projection, Colors);
        Assert.Empty(skipped.Packets.Attempts);
    }

    [Theory]
    [InlineData(97)]
    [InlineData(98)]
    [InlineData(99)]
    [InlineData(100)]
    public void Captured_morph_uses_current_selected_vertices_without_changing_native_packets_or_scratch(ushort effect)
    {
        byte[] bytes = new byte[160];
        Put(0, 1);
        Put(4, 160);
        Put(8, 12);
        Put(12, 60);
        Put(16, 3);
        Put(20, 2);
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < 3; vertex++)
            {
                int offset = 24 + shape * 24 + vertex * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), (short)((vertex == 1 ? 128 : 0) + shape * 64));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), (short)(vertex == 2 ? 128 : 0));
            }
        }
        Put(80, 1);
        Put(84, 0x24808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(90), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(92), 4);
        NativeBattleGfAnimationSource source = new(0x1000, bytes.ToImmutableArray());
        NativeBattleChocoProgramProfile profile = effect switch
        {
            98 => NativeBattleChocoProgramProfile.Choco98,
            99 => NativeBattleChocoProgramProfile.Choco99,
            100 => NativeBattleChocoProgramProfile.Choco100,
            _ => NativeBattleChocoProgramProfile.Choco97
        };
        ImmutableArray<byte> scratch = new byte[profile.ScratchBytes].ToImmutableArray();
        NativeBattleGfAnimationNode node = Node(0, angles: new(0, 0, 0), second: 1, ratio: 2048);
        byte[] nodeBytes = node.Data.ToArray();
        Array.Clear(nodeBytes, 8, 8);
        node = node with { Data = nodeBytes.ToImmutableArray() };
        NativeBattleCameraProjection projection = Projection;
        if (effect == 100)
        {
            NativeBattleChoco97NodeDraw rejected = NativeBattleChoco97ProgramDrawing.NodeProfiled(source, node, scratch, Context, projection, Colors, profile, depthBias: 32);
            NativeBattleChoco97NodeDraw rejectedCapture = NativeBattleChoco97ProgramDrawing.NodeProfiled(source, node, scratch, Context, projection, Colors, profile, depthBias: 32, pilotIdentity: new(effect, 17, 1, 2, 3));
            NativeBattleGfGeometryAttempt rejectedBefore = Assert.Single(rejected.Packets.Attempts);
            NativeBattleGfGeometryAttempt rejectedAfter = Assert.Single(rejectedCapture.Packets.Attempts);
            Assert.Equal(0, rejected.Packets.AllocationBytes);
            Assert.Equal(0, rejectedCapture.Packets.AllocationBytes);
            Assert.Null(rejectedBefore.Primitive);
            Assert.Null(rejectedAfter.Primitive);
            Assert.Equal(rejectedBefore.Data.ToArray(), rejectedAfter.Data.ToArray());
            Assert.Equal(rejectedBefore.WriteMask.ToArray(), rejectedAfter.WriteMask.ToArray());
            Assert.Equal(rejected.MorphScratch.ToArray(), rejectedCapture.MorphScratch.ToArray());
            projection = projection with { OffsetX = 0, OffsetY = 0 };
        }
        NativeBattleChoco97NodeDraw plain = NativeBattleChoco97ProgramDrawing.NodeProfiled(source, node, scratch, Context, projection, Colors, profile, depthBias: effect == 100 ? (short)32 : (short)0);
        NativeBattleChoco97NodeDraw captured = NativeBattleChoco97ProgramDrawing.NodeProfiled(source, node, scratch, Context, projection, Colors, profile, depthBias: effect == 100 ? (short)32 : (short)0, pilotIdentity: new(effect, 17, 1, 2, 3));
        Assert.Equal(plain.MorphScratch.ToArray(), captured.MorphScratch.ToArray());
        Assert.Equal(plain.Packets.AllocationBytes, captured.Packets.AllocationBytes);
        NativeBattleGfGeometryAttempt before = Assert.Single(plain.Packets.Attempts);
        NativeBattleGfGeometryAttempt after = Assert.Single(captured.Packets.Attempts);
        Assert.Equal(before.Data.ToArray(), after.Data.ToArray());
        Assert.Equal(before.WriteMask.ToArray(), after.WriteMask.ToArray());
        Assert.Equal(before.Primitive!.Words.ToArray(), after.Primitive!.Words.ToArray());
        Assert.Equal(before.Primitive.VertexPayload4.ToArray(), after.Primitive.VertexPayload4.ToArray());
        NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(after.Primitive.PilotGeometry);
        Assert.Equal(effect, visual.Identity.Effect);
        Assert.Equal(0x100C, visual.Identity.ResourceGeneration);
        Assert.Equal(32, visual.A.X);
        Assert.Equal(0, visual.A.Y);
        Assert.True(visual.UseSourceDepths);
        Assert.Equal(bytes, source.Data.ToArray());

        void Put(int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        }
    }

    private static NativeBattleGfAnimationNode Node(uint header, NativeBattleVector? angles = null, NativeBattleVector? scale = null, ushort geometry = 0, short second = 0, short ratio = 0)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), geometry);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        Put(8, new(30000, 20000, 0));
        Put(16, angles ?? new(512, 768, 1024));
        Put(24, scale ?? new(4096, 4096, 4096));
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), ratio);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), second);
        return new(0, 0, bytes.ToImmutableArray());

        void Put(int offset, NativeBattleVector value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), value.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), value.Z);
        }
    }

    private static NativeBattleGfAnimationSource Source()
    {
        byte[] bytes = new byte[72];
        Put32(0, 1);
        Put32(4, 72);
        Put32(8, 12);
        Put32(12, 28);
        Put32(16, 1);
        Put32(20, 2);
        PutVertex(24, 100, 200, 300);
        PutVertex(32, 300, -200, 100);
        return new(0x1000, bytes.ToImmutableArray());

        void Put32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        void PutVertex(int offset, short x, short y, short z)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), x);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), z);
        }
    }
}
