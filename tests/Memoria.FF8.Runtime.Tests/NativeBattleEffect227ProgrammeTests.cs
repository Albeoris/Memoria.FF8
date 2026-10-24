using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect227RecordsTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect227ProgrammeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0x200)]
    public void Selected_node_uses_full_MAC_and_never_reads_the_pointer_only_morph_field(uint header)
    {
        NativeBattleEffect227Frame frame = Frame();
        byte[] data = new byte[64];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(16), 12);
        NativeBattleGfAnimationSource source = new(0x112DCF8, data.ToImmutableArray());
        NativeBattleStaticMeshResource mesh = new(0, source.Address + 16, data.AsSpan(16, 44).ToArray().ToImmutableArray());
        NativeBattleEffect227Resources resources = Resources() with { Geometry = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(mesh.Address, mesh) };
        byte[] nodeData = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(nodeData.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(nodeData.AsSpan(8), 2000);
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(nodeData.AsSpan(offset), 4096);
        }
        NativeBattleGfAnimationNode node = new(0, 0, nodeData.ToImmutableArray());
        NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().MarkUnknown(6, 2).MarkUnknown(12, 4).Write(0, BitConverter.GetBytes((short)32000));
        Harness harness = new();
        NativeBattleEffect227Frame result = NativeBattleEffect227Programme.DrawNode(frame, source, node, caller, resources, harness.Inputs());
        Assert.Equal(header == 0 ? unchecked((short)34000) : 34000, result.Memory.GeometryRegisters!.Translation[0]);
        Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.IsType<NativeBattleEffect227DrawBatch>(Assert.Single(harness.Operations));
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
    }

    [Fact]
    public void Unselected_shape_fails_before_reading_or_writing_any_morph_pointer()
    {
        byte[] bytes = new byte[64];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 16);
        NativeBattleGfAnimationSource source = new(0x112DCF8, bytes.ToImmutableArray());
        NativeBattleStaticMeshResource mesh = new(0, source.Address + 16, new byte[44].ToImmutableArray());
        NativeBattleEffect227Resources resources = Resources() with { Geometry = ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty.Add(mesh.Address, mesh) };
        byte[] node = new byte[44];
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(24), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(42), 1);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect227Programme.DrawNode(Frame(), source, new(0, 0, node.ToImmutableArray()), NativeBattleEffectArena.Clear().MarkUnknown(0, 16), resources, new Harness().Inputs()));
    }
}
