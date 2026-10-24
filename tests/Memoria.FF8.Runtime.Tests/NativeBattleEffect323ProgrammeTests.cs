using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect323Controller;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect323LowerTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect323ProgrammeTests
{
    [Theory]
    [InlineData(0x200)]
    [InlineData(0x10102)]
    [InlineData(0x40003)]
    [InlineData(0x104)]
    public void Selected_header_paths_draw_with_retained_negative_depth_and_unknown_caller_W(uint header)
    {
        Fixture fixture = new(0x65A660);
        NativeBattleGfAnimationSource source = NativeBattleActDepthRoutingTests.ProgrammeSource(0xFE2BE4);
        NativeBattleStaticMeshResource mesh = new(0, source.Address + 16, source.Data.AsSpan().Slice(16).ToArray().ToImmutableArray());
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        byte[] data = NativeBattleActDepthRoutingTests.Node(0).Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), header);
        NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().MarkUnknown(6, 2).MarkUnknown(12, 4).Write(4, BitConverter.GetBytes((short)2000)).Write(8, BitConverter.GetBytes(-128));
        NativeBattleEffect323Frame result = NativeBattleEffect323Programme.DrawNode(fixture.Frame, source, new(0, 0, data.ToImmutableArray()), caller, fixture.Resources, fixture.Inputs());
        NativeBattleEffect323DrawBatch batch = Assert.IsType<NativeBattleEffect323DrawBatch>(Assert.Single(fixture.Operations));
        Assert.Equal(17 + (((int)result.Memory.GeometryRegisters!.Otz!.Value - 128) >> 2), Assert.Single(batch.Batch.Attempts).Primitive!.Bucket);
        NativeBattleEffectArena temp = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 88);
        Assert.Equal(unchecked((uint)-128), NativeBattleEffect323Lower.Dword323(temp, 0x18));
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
    }

    [Fact]
    public void Intermediate_morph_writes_only_XYZ_to_current_aliased_scratch()
    {
        Fixture fixture = new(0x65A660);
        byte[] bytes = new byte[84];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 36);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 3);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), 100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), 300);
        NativeBattleGfAnimationSource source = new(0xFE2BE4, bytes.ToImmutableArray());
        NativeBattleStaticMeshResource mesh = new(0, source.Address + 16, bytes.AsSpan(16).ToArray().ToImmutableArray());
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        uint scratch = Fixture.Arena + 0x1754;
        fixture.Frame = WriteBytes(fixture.Frame, scratch, NativeBattleEffectArena.Clear().MarkUnknown(6, 2), 8);
        byte[] node = NativeBattleActDepthRoutingTests.Node(0).Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(42), 1);
        NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().MarkUnknown(6, 2).Write(8, BitConverter.GetBytes(-128)).Write(12, BitConverter.GetBytes(scratch));
        NativeBattleEffect323Frame result = NativeBattleEffect323Programme.DrawNode(fixture.Frame, source, new(0, 0, node.ToImmutableArray()), caller, fixture.Resources, fixture.Inputs());
        NativeBattleEffectArena row = ReadBytes(result, scratch, 8);
        Assert.Equal((short)200, BinaryPrimitives.ReadInt16LittleEndian(row.Read(0, 2)));
        Assert.DoesNotContain(true, row.ReadSnapshot(6, 2).Known);
        Assert.Equal(scratch, NativeBattleEffect323Lower.Dword323(NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 88), 4));
    }

    [Fact]
    public void Invisible_node_does_not_consume_unknown_caller_or_allocate()
    {
        Fixture fixture = new(0x65A660);
        NativeBattleGfAnimationNode node = new(0, 0, new byte[44].ToImmutableArray());
        NativeBattleEffect323Frame result = NativeBattleEffect323Programme.DrawNode(fixture.Frame, null!, node, NativeBattleEffectArena.Clear().MarkUnknown(0, 16), null!, fixture.Inputs());
        Assert.Same(fixture.Frame, result);
        Assert.Empty(fixture.Operations);
    }
}
