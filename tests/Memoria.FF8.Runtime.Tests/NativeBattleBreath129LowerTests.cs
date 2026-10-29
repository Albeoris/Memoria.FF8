using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBreath129Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBreath129LowerTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;

    [Theory]
    [InlineData(0)]
    [InlineData(4096)]
    public void ACT_early_gates_consume_no_temporary_or_drawing_inputs(int fade)
    {
        NativeBattleBreath129Frame frame = Frame();
        byte[] bytes = Node().Data.ToArray();
        if (fade == 0)
        {
            Array.Clear(bytes, 24, 6);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), (short)fade);
        NativeBattleBreath129Frame actual = NativeBattleBreath129Act.Draw6B57B0(frame, new(0, 0, bytes.ToImmutableArray()), null!, null!, Inputs((_, _) => throw new InvalidOperationException()));
        Assert.Same(frame, actual);
    }

    [Theory]
    [InlineData(0u, 130, 290)]
    [InlineData(0x200u, 110, 330)]
    public void ACT_direct_header_and_R_only_path_preserve_native_translation_and_temporary(uint header, int x, int z)
    {
        NativeBattleBreath129Frame frame = Frame();
        byte[] bytes = Node().Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        NativeBattleActionMatrix basis = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 100, 200, 300, 4096);
        int calls = 0;
        NativeBattleBreath129Frame actual = NativeBattleBreath129Act.Draw6B57B0(frame, new(0, 0, bytes.ToImmutableArray()), ActSource(), new(NativeBattlePackedMatrix32.FromMatrix(basis, null), -128, Base + 0x1000), Inputs((current, operation) =>
        {
            NativeBattleBreath129DrawDefaultMesh88 draw = Assert.IsType<NativeBattleBreath129DrawDefaultMesh88>(operation);
            NativeBattleEffectArena context = Context(current, draw.TemporaryAddress, 88);
            Assert.Equal(0x10000010u, Dword(context, 0));
            Assert.Equal(0x1000001Cu, Dword(context, 4));
            Assert.Equal(0x2030u, Dword(context, 28));
            Assert.Equal(unchecked((uint)-128), Dword(context, 24));
            Assert.Throws<NotSupportedException>(() => context.Read(8, 4));
            Assert.Equal(new int?[] { x, 220, z }, current.Memory.GeometryRegisters!.Translation);
            Assert.Null(current.Memory.GeometryRegisters.Rotation20[18]);
            calls++;
            return current;
        }));
        Assert.Equal(1, calls);
        Assert.Equal(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), actual.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
        if (header == 0)
        {
            Assert.Equal(new int?[] { 30, 20, -10 }, actual.Memory.ScriptedColorRegisters!.Mac);
        }
    }

    [Fact]
    public void ACT_intermediate_morph_publishes_GPF_GPL_state_and_preserves_destination_W()
    {
        NativeBattleBreath129Frame frame = Frame();
        frame = WriteBytes(frame, Base + 0x1000, NativeBattleEffectArena.Clear().MarkUnknown(6, 2), 8);
        byte[] bytes = Node().Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x200);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0x12345678);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), 1);
        NativeBattleBreath129Frame actual = NativeBattleBreath129Act.Draw6B57B0(frame, new(0, 0, bytes.ToImmutableArray()), ActSource(), new(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), 12, Base + 0x1000), Inputs((current, operation) =>
        {
            var draw = Assert.IsType<NativeBattleBreath129DrawDefaultMesh88>(operation);
            Assert.Equal(0x20F0u, Dword(Context(current, draw.TemporaryAddress, 88), 28));
            Assert.Equal(0x12345678u, Dword(Context(current, draw.TemporaryAddress, 88), 8));
            return current;
        }));
        Assert.Equal(new byte[] { 6, 0, 7, 0, 8, 0 }, ReadBytes(actual, Base + 0x1000, 6).Read(0, 6));
        Assert.Throws<NotSupportedException>(() => ReadBytes(actual, Base + 0x1006, 2).Read(0, 2));
        Assert.Equal(new int?[] { 6, 7, 8 }, actual.Memory.ScriptedColorRegisters!.Mac);
        Assert.Equal(2048u, actual.Memory.ScriptedColorRegisters.Ir0);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(2048u)]
    public void GT4_eager_rows_V_scroll_E2_order_and_depth_sidecars_are_physical(uint cue)
    {
        NativeBattleBreath129Frame frame = MeshFrame(1000, out uint temporary);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 92).Write(8, BitConverter.GetBytes(cue)), 92);
        List<NativeBattleBreath129AppendPacket> packets = [];
        NativeBattleBreath129Frame actual = NativeBattleBreath129Mesh.Draw6B5A50(frame, temporary, 17, 2, Inputs((current, operation) =>
        {
            packets.Add(Assert.IsType<NativeBattleBreath129AppendPacket>(operation));
            return current;
        }));
        Assert.Equal(new[] { Base + 0x3B70 + 52, Base + 0x3B70, Base + 0x3B70 + 64 }, packets.Select(packet => packet.Address));
        Assert.All(packets, packet => Assert.True(packet.Insert));
        Assert.Equal(new[] { 12, 52, 12 }, packets.Select(packet => packet.Allocation));
        Assert.Equal(new int[] { 0, 0, 0, 0 }, packets[0].VertexPayload4);
        Assert.Equal(actual.Memory.GeometryRegisters!.Sz.Select(value => unchecked((int)value!.Value)), packets[1].VertexPayload4);
        Assert.Equal(new byte[] { 132, 133, 134, 135 }, Enumerable.Range(0, 4).Select(corner => packets[1].Data[13 + corner * 12]));
        Assert.Equal(Base + 0x3B70 + 76, actual.State.ReadDword(0x252117C));
        Assert.Equal(0x12272DCu + 0x40 + 36, Dword(Context(actual, temporary, 92), 36));
        Assert.Throws<NotSupportedException>(() => Context(actual, temporary, 92).Read(86, 2));
        if (cue != 0)
        {
            Assert.Equal(0xCCu, (uint)packets[1].Data[19]);
            Assert.Equal(0xCCu, (uint)packets[1].Data[31]);
            Assert.Equal(0xCCu, (uint)packets[1].Data[43]);
            Assert.Equal(0x3E808080u, actual.Memory.ScriptedColorRegisters!.Material);
            Assert.Equal(cue, actual.Memory.ScriptedColorRegisters.Ir0);
            Assert.Equal(32u, actual.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
        }
    }

    [Fact]
    public void GT4_first_FLAG_rejection_keeps_prefix_and_copies_unknown_fourth_row_before_gate()
    {
        NativeBattleBreath129Frame frame = MeshFrame(0, out uint temporary);
        frame = WriteBytes(frame, Base + 0x35F0 + 24, ReadBytes(frame, Base + 0x35F0 + 24, 8).MarkUnknown(0, 2), 8);
        List<NativeBattleBreath129AppendPacket> packets = [];
        NativeBattleBreath129Frame actual = NativeBattleBreath129Mesh.Draw6B5A50(frame, temporary, 17, 2, Inputs((current, operation) =>
        {
            packets.Add(Assert.IsType<NativeBattleBreath129AppendPacket>(operation));
            return current;
        }));
        NativeBattleBreath129AppendPacket rejected = Assert.Single(packets);
        Assert.False(rejected.Insert);
        Assert.Equal(frame.State.ReadDword(0x252117C), actual.State.ReadDword(0x252117C));
        Assert.Equal(NativeBattleGfPacketByteState.Known, rejected.WriteMask[48]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, rejected.WriteMask[8]);
        Assert.Throws<NotSupportedException>(() => Context(actual, temporary, 92).Read(80, 2));
        Assert.Equal(0xDEAD1234u, Dword(Context(actual, temporary, 92), 48));
        Assert.NotEqual(0u, Dword(Context(actual, temporary, 92), 52) & 0x60000);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selected_GT4_capture_preserves_native_operand_preread_and_late_fourth_gate(bool clipped)
    {
        NativeBattleBreath129Frame Run(bool capture, out List<NativeBattleBreath129AppendPacket> output)
        {
            NativeBattleBreath129Frame frame = MeshFrame(clipped ? (short)0 : (short)1000, out uint temporary);
            frame = frame with { State = frame.State with { PendingColdSources = false } };
            if (clipped)
            {
                frame = WriteBytes(frame, Base + 0x35F0 + 24, ReadBytes(frame, Base + 0x35F0 + 24, 8).MarkUnknown(0, 2), 8);
            }
            List<NativeBattleBreath129AppendPacket> packets = [];
            NativeBattleBreath129Inputs inputs = Inputs((current, operation) =>
            {
                packets.Add(Assert.IsType<NativeBattleBreath129AppendPacket>(operation));
                return current;
            }) with { ReadProjection = _ => new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), PilotIdentity = capture ? new(129, 0x25211C0, 0, 4, 0) { ResourceGeneration = 0x12272DC } : null };
            NativeBattleBreath129Frame result = NativeBattleBreath129Mesh.Draw6B5A50(frame, temporary, 17, 2, inputs);
            if (clipped)
            {
                Assert.Throws<NotSupportedException>(() => Context(result, temporary, 92).Read(80, 2));
            }
            output = packets;
            return result;
        }
        NativeBattleBreath129Frame native = Run(false, out var nativePackets);
        string nativeFrame = NativeBattleTestSnapshots.Json(native);
        string nativeWrites = System.Text.Json.JsonSerializer.Serialize(nativePackets);
        NativeBattleBreath129Frame captured = Run(true, out var packets);
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(nativeWrites, System.Text.Json.JsonSerializer.Serialize(packets));
        if (clipped)
        {
            Assert.Null(Assert.Single(packets).PilotGeometry);
        }
        else
        {
            NativeBattleBreath129AppendPacket polygon = Assert.Single(packets, p => p.PilotGeometry is not null);
            NativeBattlePilotVisualGeometry geometry = polygon.PilotGeometry!;
            Assert.Equal(new System.Numerics.Vector3(-100, -100, 1000), geometry.A);
            Assert.Equal(new System.Numerics.Vector3(100, 100, 1000), geometry.D);
            Assert.Equal(new System.Numerics.Vector4(polygon.VertexPayload4[0], polygon.VertexPayload4[1], polygon.VertexPayload4[2], polygon.VertexPayload4[3]), geometry.SourceDepths);
            Assert.True(geometry.UseSourceDepths);
        }
    }

    internal static NativeBattleBreath129Frame Frame()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770(), true, true, null), ScriptedColorRegisters = new([0, 0, 0], [0, 0, 0], [1, 2, 3], 0) { Material = 0, Ir0 = 0 }, PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleBreath129State state = NativeBattleBreath129State.FromColdProcess().WriteDword(0x252115C, Base).WriteDword(0x252117C, Base + 0x3B70) with { MutableSources = NativeBattleEffectArena.Clear() };
        return new(memory, new(129, 128, new(129, 0x6B4C00, 0x6B4C10), null, 0), new(55), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }
    internal static NativeBattleBreath129Inputs Inputs(Func<NativeBattleBreath129Frame, NativeBattleBreath129Operation, NativeBattleBreath129Frame>? execute = null) => new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), _ => new(null, 100, 160, 108), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), execute ?? ((current, _) => current));
    private static NativeBattleBreath129Frame MeshFrame(short z, out uint temporary)
    {
        NativeBattleBreath129Frame frame = Frame();
        NativeBattleEffectArena mesh = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x20)).Write(0x3C, BitConverter.GetBytes(1)).Write(0x40, BitConverter.GetBytes(0x3E808080u)).Write(0x44, BitConverter.GetBytes((ushort)0)).Write(0x46, BitConverter.GetBytes((ushort)2)).Write(0x48, BitConverter.GetBytes((ushort)4)).Write(0x4A, BitConverter.GetBytes((ushort)6)).Write(0x4C, BitConverter.GetBytes(0x1111FA01u)).Write(0x50, BitConverter.GetBytes(0x2222FB02u)).Write(0x54, BitConverter.GetBytes(0xFD04FC03u)).Write(0x58, BitConverter.GetBytes(0xAA102030u)).Write(0x5C, BitConverter.GetBytes(0xBB405060u)).Write(0x60, BitConverter.GetBytes(0xCC708090u));
        frame = WriteBytes(frame, 0x12272DC, mesh, 0x64);
        for (int corner = 0; corner < 4; corner++)
        {
            frame = WriteBytes(frame, Base + 0x35F0 + (uint)corner * 8, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)((corner & 1) * 200 - 100))).Write(2, BitConverter.GetBytes((short)((corner >> 1) * 200 - 100))).Write(4, BitConverter.GetBytes(z)).MarkUnknown(6, 2), 8);
        }
        (frame, temporary) = Allocate(frame, 92);
        NativeBattleEffectArena context = Context(frame, temporary, 92).Write(0, BitConverter.GetBytes(0x12272DCu)).Write(4, BitConverter.GetBytes(0x00060402u)).Write(8, new byte[4]).Write(14, BitConverter.GetBytes((ushort)10)).Write(16, new byte[4]).Write(20, BitConverter.GetBytes((ushort)256)).Write(22, BitConverter.GetBytes((ushort)256)).Write(24, new byte[4]).Write(28, BitConverter.GetBytes((ushort)128)).Write(30, BitConverter.GetBytes((ushort)128)).Write(32, BitConverter.GetBytes(Base + 0x35F0)).Write(48, BitConverter.GetBytes(0xDEAD1234u));
        return PublishContext(frame, temporary, context, 92);
    }
    private static NativeBattleGfAnimationNode Node()
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 10);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 20);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 30);
        for (int offset = 24; offset <= 28; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        return new(0, 0, bytes.ToImmutableArray());
    }
    private static NativeBattleGfAnimationSource ActSource()
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 1);
        for (int shape = 0; shape < 2; shape++)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28 + shape * 8 + lane * 2), (short)(1 + shape * 10 + lane));
            }
        }
        return new(0x10000000, bytes.ToImmutableArray());
    }
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
