using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect290Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect290Lower;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect290LowerTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect290ProgrammeTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(0x200u)]
    public void Captured290_ACT_keeps_selected_resource_vertex_pointer_native_registers_and_temporary_bytes(uint header)
    {
        NativeBattleGfAnimationSource source = NativeBattleActDepthRoutingTests.ProgrammeSource(FirstProgrammeAddress);
        Fixture Build()
        {
            Fixture fixture = new();
            fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(source.Address + 16, new(0, source.Address + 16, source.Data.AsSpan().Slice(16).ToArray().ToImmutableArray())) };
            return fixture;
        }
        NativeBattleEffectArena Caller() => Put290(Put290(Put290(PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null)).MarkUnknown(0x20, 12), 0x2C, 0), 0x30, unchecked((uint)-128)), 0x34, Fixture.Arena + MorphOffset);
        byte[] bytes = NativeBattleActDepthRoutingTests.Node(0).Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        NativeBattleGfAnimationNode node = new(0, 0, bytes.ToImmutableArray());
        Fixture nativeFixture = Build();
        Fixture capturedFixture = Build();
        var projection = new NativeBattleCameraProjection(NativeBattleCuragaMatrices.Identity8DD770(), 512, 160, 108);
        var native = NativeBattleEffect290Programme.DrawNode(nativeFixture.Frame, source, node, Caller(), nativeFixture.Resources, nativeFixture.Inputs() with { ReadProjection = _ => projection });
        NativeBattlePilotVisualIdentity owner = new(290, 0x1000, 0, 11, 7);
        var captured = NativeBattleEffect290Programme.DrawNode(capturedFixture.Frame, source, node, Caller(), capturedFixture.Resources, capturedFixture.Inputs() with { ReadProjection = _ => projection }, owner);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(NativeBattleTestSnapshots.Json(nativeFixture.Deliveries), NativeBattleTestSnapshots.Json(capturedFixture.Deliveries));
        var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(Assert.Single(capturedFixture.Deliveries).Batch.Attempts).Primitive!.PilotGeometry);
        Assert.Equal(owner.Generation, visual.Identity.Generation);
        Assert.Equal(NativeBattleSelectedProgrammeVisuals.Resource(owner, source.Address + 16, source.Address + 28).ResourceGeneration, visual.Identity.ResourceGeneration);
        Assert.False(visual.ScreenSpace);
        Assert.True(visual.Continuous);
    }

    [Theory]
    [InlineData(0x200, -512)]
    [InlineData(0x40301, -128)]
    [InlineData(0x100, 0)]
    [InlineData(0x101, -128)]
    [InlineData(0x40101, -512)]
    [InlineData(0, 0)]
    public void Selected_headers_keep_caller_DWORD_depth_and_do_not_read_unused_extra_scale(uint header, int depth)
    {
        Fixture fixture = new();
        NativeBattleGfAnimationSource source = NativeBattleActDepthRoutingTests.ProgrammeSource(FirstProgrammeAddress);
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(source.Address + 16, new(0, source.Address + 16, source.Data.AsSpan().Slice(16).ToArray().ToImmutableArray())) };
        byte[] bytes = NativeBattleActDepthRoutingTests.Node(0).Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null)).MarkUnknown(0x20, 12);
        caller = Put290(Put290(Put290(caller, 0x2C, 0), 0x30, unchecked((uint)depth)), 0x34, Fixture.Arena + MorphOffset);
        NativeBattleEffect290Frame result = NativeBattleEffect290Programme.DrawNode(fixture.Frame, source, new(0, 0, bytes.ToImmutableArray()), caller, fixture.Resources, fixture.Inputs());
        NativeBattleEffect290DrawBatch delivery = Assert.Single(fixture.Deliveries);
        NativeBattleGeometryPrimitive primitive = Assert.Single(delivery.Batch.Attempts).Primitive!;
        Assert.NotNull(primitive);
        Assert.Equal(17 + (Math.Max(0, unchecked((int)result.Memory.GeometryRegisters!.Otz!.Value + depth)) >> 2), primitive.Bucket);
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 88);
        Assert.Equal(depth, unchecked((int)Dword290(temporary, 0x18)));
        Assert.Equal(0x2030u, Dword290(temporary, 0x1C));
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(result.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Intermediate_morph_keeps_partial_W_bytes_and_packed_shader_load_transports_them()
    {
        Fixture fixture = new();
        const int count = 4;
        byte[] bytes = new byte[16 + 12 + count * 16 + 32 + 24];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 12 + count * 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 2);
        for (int vertex = 0; vertex < count; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28 + vertex * 8), 100);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28 + count * 8 + vertex * 8), 300);
        }
        int face = 16 + 12 + count * 16 + 16;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x2C112233);
        for (int vertex = 0; vertex < count; vertex++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + vertex * 2), (ushort)(vertex * 2));
        }
        NativeBattleGfAnimationSource source = new(ThirdProgrammeAddress, bytes.ToImmutableArray());
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(source.Address + 16, new(0, source.Address + 16, bytes.AsSpan(16).ToArray().ToImmutableArray())) };
        uint scratch = Fixture.Arena + MorphOffset;
        NativeBattleEffectArena before = NativeBattleEffectArena.Clear();
        for (int vertex = 0; vertex < count; vertex++)
        {
            before = before.Write(vertex * 8 + 6, [0xA5]).MarkUnknown(vertex * 8 + 7, 1);
        }
        fixture.Frame = WriteBytes(fixture.Frame, scratch, before, count * 8);
        byte[] node = NativeBattleActDepthRoutingTests.Node(0).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(42), 1);
        NativeBattleEffectArena caller = Put290(Put290(Put290(PackedArena(NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null)), 0x2C, 0), 0x30, unchecked((uint)-128)), 0x34, scratch);
        NativeBattleEffect290Frame result = NativeBattleEffect290Programme.DrawNode(fixture.Frame, source, new(0, 0, node.ToImmutableArray()), caller, fixture.Resources, fixture.Inputs());
        for (int vertex = 0; vertex < count; vertex++)
        {
            NativeBattleEffectArena actual = ReadBytes(result, scratch + (uint)(vertex * 8), 8);
            Assert.Equal(200, Word290(actual, 0));
            Assert.Equal(0xA5, actual.Read(6, 1)[0]);
            Assert.False(actual.ReadSnapshot(7, 1).Known[0]);
        }
        Assert.True(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(result.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
        Assert.Equal(0xA5, result.Memory.ScriptedColorRegisters.NormalInput!.Value[6]);
        Assert.NotNull(Assert.Single(Assert.Single(fixture.Deliveries).Batch.Attempts).Primitive);
    }

    [Fact]
    public void Invisible_node_does_not_allocate_or_read_unknown_caller()
    {
        Fixture fixture = new();
        NativeBattleGfAnimationNode node = new(0, 0, new byte[44].ToImmutableArray());
        NativeBattleEffect290Frame result = NativeBattleEffect290Programme.DrawNode(fixture.Frame, null!, node, NativeBattleEffectArena.Clear().MarkUnknown(0, 56), null!, fixture.Inputs());
        Assert.Same(fixture.Frame, result);
        Assert.Empty(fixture.Deliveries);
    }
}
