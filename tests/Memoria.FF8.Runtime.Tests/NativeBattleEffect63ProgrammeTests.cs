using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect63Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect63Lower;
using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect63LowerTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect63ProgrammeTests
{
    [Theory]
    [InlineData((short)0)]
    [InlineData((short)2048)]
    [InlineData((short)4096)]
    public void Captured_programme_keeps_selected_packed_W_morph_scratch_and_native_packet_outputs(short factor)
    {
        Fixture nativeFixture = new();
        NativeBattleEffect63Frame native = Draw(nativeFixture, null);
        string frozen = NativeBattleTestSnapshots.Json(native);
        string frozenPackets = NativeBattleTestSnapshots.Json(nativeFixture.Draws);
        Fixture visualFixture = new();
        NativeBattlePilotVisualIdentity identity = new(63, MainDescriptor, 0, 1, 0x18);
        NativeBattleEffect63Frame visual = Draw(visualFixture, identity);
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(visual));
        Assert.Equal(frozenPackets, NativeBattleTestSnapshots.Json(visualFixture.Draws));
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(Assert.Single(visualFixture.Draws).Batch.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!.PilotGeometry);
        Assert.Equal((ushort)63, geometry.Identity.Effect);
        Assert.Equal(0x1350068, geometry.Identity.ResourceGeneration);
        Assert.Equal(1000, geometry.A.Z);

        NativeBattleEffect63Frame Draw(Fixture fixture, NativeBattlePilotVisualIdentity? capture)
        {
            (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source(true);
            NativeBattleEffect63Resources resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
            uint scratch = Fixture.Common + MorphOffset;
            NativeBattleEffect63Frame frame = fixture.Frame;
            for (int vertex = 0; vertex < 4; vertex++)
            {
                frame = WriteBytes(frame, scratch + (uint)(vertex * 8), NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(6, [(byte)(0xA0 + vertex)]), 8);
            }
            NativeBattleEffect63Inputs inputs = fixture.Inputs with { ReadProjection = _ => new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108) };
            return NativeBattleEffect63Programme.DrawNode(frame, source, Node(0, 0, 0, 1, factor), Caller(NativeBattleCuragaMatrices.Identity8DD770()), resources, inputs, capture);
        }
    }

    [Theory]
    [InlineData(0, 8192)]
    [InlineData(0x8000, 4096)]
    public void Translation_always_uses_full_MAC_even_when_header_skips_rotation_composition(uint header, short expectedRotation)
    {
        Fixture f = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source(false);
        f.Resources = f.Resources with { Geometry = f.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleActionMatrix caller = NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [8192, 0, 0, 0, 4096, 0, 0, 0, 4096], X = 100 };
        NativeBattleEffect63Frame result = NativeBattleEffect63Programme.DrawNode(f.Frame, source, Node(header, 20000), Caller(caller), f.Resources, f.Inputs);
        Assert.Equal(40100, result.Memory.GeometryRegisters!.Translation[0]);
        Assert.Equal(unchecked((byte)expectedRotation), result.Memory.GeometryRegisters.Rotation20[0]);
        Assert.Equal(unchecked((byte)(expectedRotation >> 8)), result.Memory.GeometryRegisters.Rotation20[1]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Single(f.Draws);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1024, 0x20F0)]
    public void Actual_flags_ignore_source_high_bits_and_depth_retains_negative_128(short fade, uint expected)
    {
        Fixture f = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source(false);
        f.Resources = f.Resources with { Geometry = f.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleEffect63Frame result = NativeBattleEffect63Programme.DrawNode(f.Frame, source, Node(0x48100, 0, fade: fade), Caller(NativeBattleCuragaMatrices.Identity8DD770()), f.Resources, f.Inputs);
        NativeBattleEffectArena context = f.Temporary(result, 108);
        Assert.Equal(fade == 0 ? 0x2030u : expected, Dword63(context, 0x1C));
        Assert.Equal(-128, unchecked((int)Dword63(context, 0x18)));
        if (fade == 0)
        {
            Assert.Equal(0xA5A5A5A5u, Dword63(context, 8));
        }
        else
        {
            Assert.Equal(0x010203u, Dword63(context, 8));
        }
    }

    [Fact]
    public void ASCII_zero_does_not_read_unknown_phase_and_retains_unwritten_suffix()
    {
        Fixture f = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source(false);
        f.Resources = f.Resources with { Geometry = f.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleEffect63Frame result = NativeBattleEffect63Programme.DrawNode(f.Frame, source, Node(0, 0), Caller(NativeBattleCuragaMatrices.Identity8DD770()).MarkUnknown(0x26, 10), f.Resources, f.Inputs);
        NativeBattleEffectArena context = f.Temporary(result, 108);
        Assert.Equal(0, Word63(context, 0x22));
        Assert.Equal(256, Word63(context, 0x28));
        Assert.All(context.Read(0x2C, 8), value => Assert.Equal(0xA5, value));
    }

    [Fact]
    public void ASCII_one_consumes_only_low_phase_byte_and_writes_actual_window_bounds()
    {
        Fixture f = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source(true);
        f.Resources = f.Resources with { Geometry = f.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleEffectArena caller = Caller(NativeBattleCuragaMatrices.Identity8DD770()).Write(0x20, BitConverter.GetBytes(SecondSelectorsAddress)).Write(0x26, [160]).MarkUnknown(0x27, 9);
        NativeBattleEffect63Frame result = NativeBattleEffect63Programme.DrawNode(f.Frame, source, Node(0, 0), caller, f.Resources, f.Inputs);
        NativeBattleEffectArena context = f.Temporary(result, 108);
        Assert.Equal(32, Word63(context, 0x22));
        Assert.Equal(0, Word63(context, 0x2C));
        Assert.Equal(0, Word63(context, 0x2E));
        Assert.Equal(256, Word63(context, 0x30));
        Assert.Equal(128, Word63(context, 0x32));
        Assert.NotEmpty(Assert.Single(f.Draws).Batch.Attempts);
    }

    [Fact]
    public void Morph_writes_XYZ_only_and_preserves_partial_W_through_FT4_projection()
    {
        Fixture f = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source(true);
        f.Resources = f.Resources with { Geometry = f.Resources.Geometry.Add(mesh.Address, mesh) };
        uint scratch = Fixture.Common + MorphOffset;
        for (int vertex = 0; vertex < 4; vertex++)
        {
            f.Frame = WriteBytes(f.Frame, scratch + (uint)(vertex * 8), NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(6, [(byte)(0xA0 + vertex)]), 8);
        }
        NativeBattleEffectArena caller = Caller(NativeBattleCuragaMatrices.Identity8DD770()).Write(0x30, BitConverter.GetBytes(scratch));
        NativeBattleEffect63Frame result = NativeBattleEffect63Programme.DrawNode(f.Frame, source, Node(0, 0, 0, 1, 2048), caller, f.Resources, f.Inputs);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            NativeBattleEffectArena row = ReadBytes(result, scratch + (uint)(vertex * 8), 8);
            Assert.Equal((byte)(0xA0 + vertex), row.ReadSnapshot(0, 8).Data[6]);
            Assert.Equal(new[] { true, true, true, true, true, true, true, false }, row.ReadSnapshot(0, 8).Known);
        }
        Assert.Equal(0, Word63(ReadBytes(result, scratch, 8), 0));
        Assert.Equal(1000, Word63(ReadBytes(result, scratch, 8), 4));
        Assert.True(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(result.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Equal(0xA3, result.Memory.ScriptedColorRegisters.NormalInput!.Value[6]);
        NativeBattleGeometryPrimitive primitive = Assert.Single(Assert.Single(f.Draws).Batch.Attempts.Where(attempt => attempt.Primitive is not null)).Primitive!;
        Assert.Equal(9u, primitive.Words[0] >> 24);
        Assert.Equal(result.Memory.GeometryRegisters!.Sz.Select(value => unchecked((int)value!.Value)), primitive.VertexPayload4);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(4096, 44)]
    public void Morph_endpoint_selection_does_not_consume_unknown_scratch(short factor, int offset)
    {
        Fixture f = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source(false);
        f.Resources = f.Resources with { Geometry = f.Resources.Geometry.Add(mesh.Address, mesh) };
        NativeBattleEffect63Frame result = NativeBattleEffect63Programme.DrawNode(f.Frame, source, Node(0, 0, 0, 1, factor), Caller(NativeBattleCuragaMatrices.Identity8DD770()).MarkUnknown(0x30, 4), f.Resources, f.Inputs);
        Assert.Equal(mesh.Address + (uint)offset, Dword63(f.Temporary(result, 108), 4));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Invisible_node_does_not_allocate_or_read_unknown_caller(bool fade)
    {
        Fixture f = new();
        byte[] bytes = Node(0, 0, fade: fade ? (short)4096 : (short)0).Data.ToArray();
        if (fade)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0);
        }
        else
        {
            Array.Clear(bytes, 24, 6);
        }
        NativeBattleGfAnimationNode node = new(0, 0, bytes.ToImmutableArray());
        NativeBattleEffect63Frame result = NativeBattleEffect63Programme.DrawNode(f.Frame, new(0, []), node, NativeBattleEffectArena.Clear().MarkUnknown(0, 52), f.Resources, f.Inputs);
        Assert.Same(f.Frame, result);
        Assert.Empty(f.Draws);
    }

    private static NativeBattleEffectArena Caller(NativeBattleActionMatrix matrix) => PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)).Write(0x20, BitConverter.GetBytes(FirstSelectorsAddress)).Write(0x24, BitConverter.GetBytes((short)-128)).MarkUnknown(0x26, 10).Write(0x30, BitConverter.GetBytes(Fixture.Common + MorphOffset));
    private static NativeBattleGfAnimationNode Node(uint header, short x, short first = 0, short second = 0, short factor = 0, short fade = 0)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), x);
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0x010203);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), fade);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), factor);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), first);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), second);
        return new(0, 0, bytes.ToImmutableArray());
    }
    private static (NativeBattleGfAnimationSource Source, NativeBattleStaticMeshResource Mesh) Source(bool polygon)
    {
        NativeBattleStaticMeshResource mesh = Fixture.Mesh(0x1350068, true, 3, polygon);
        byte[] bytes = new byte[16 + mesh.Bytes.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 16);
        mesh.Bytes.CopyTo(bytes, 16);
        return (new(mesh.Address - 16, bytes.ToImmutableArray()), mesh);
    }
}
