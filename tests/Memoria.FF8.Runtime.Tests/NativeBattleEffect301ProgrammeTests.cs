using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect301Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect301Lower;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect301LowerTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect301ProgrammeTests
{
    [Theory]
    [InlineData(0, 0x2000)]
    [InlineData(256, 0x20C0)]
    public void Node_adds_caller_before_view_composition_and_retains_native_depth(int cue, uint flags)
    {
        Fixture fixture = new();
        (NativeBattleGfAnimationSource source, NativeBattleStaticMeshResource mesh) = Source();
        fixture.Resources = fixture.Resources with { Geometry = fixture.Resources.Geometry.Add(mesh.Address, mesh) };
        fixture.Frame = fixture.Frame with { Staging = fixture.Frame.Staging!.MarkUnknown(NativeBattleTemporaryMemory.Address + 8, 4) };
        NativeBattleEffect301Inputs inputs = fixture.Inputs() with { ReadView = _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [8192, 0, 0, 0, 4096, 0, 0, 0, 4096], X = 10 }, null) };
        NativeBattleEffectArena caller = Put301(PutWord301(NativeBattleEffectArena.Clear().MarkUnknown(6, 2).MarkUnknown(12, 4), 0, 100), 8, 256);
        NativeBattleEffect301Frame result = NativeBattleEffect301Programme.DrawNode(fixture.Frame, source, Node(20000, cue), caller, fixture.Resources, inputs);
        Assert.Equal(40210, result.Memory.GeometryRegisters!.Translation[0]);
        NativeBattleEffectArena temp = NativeBattleTemporaryMemory.ReadView(result.Staging!, NativeBattleTemporaryMemory.Address, 88);
        Assert.Equal(256u, Dword301(temp, 24));
        Assert.Equal(flags, Dword301(temp, 28));
        Assert.Equal(mesh.Address + 12, Dword301(temp, 4));
        if (cue == 0)
        {
            Assert.DoesNotContain(true, temp.ReadSnapshot(8, 4).Known);
        }
        else
        {
            Assert.Equal(0x123456u, Dword301(temp, 8));
        }
        Assert.Single(fixture.Operations);
    }

    [Fact]
    public void Invisible_node_skips_all_caller_and_source_consumption()
    {
        Fixture fixture = new();
        NativeBattleGfAnimationNode node = Node(0, 4096);
        byte[] bytes = node.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0);
        NativeBattleEffect301Frame result = NativeBattleEffect301Programme.DrawNode(fixture.Frame, null!, node with { Data = bytes.ToImmutableArray() }, NativeBattleEffectArena.Clear().MarkUnknown(0, 16), null!, fixture.Inputs());
        Assert.Same(fixture.Frame, result);
        Assert.Empty(fixture.Operations);
    }

    private static NativeBattleGfAnimationNode Node(short x, int cue)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), x);
        for (int offset = 24; offset < 30; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), 0x123456);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), (short)cue);
        return new(0, 0, bytes.ToImmutableArray());
    }
    private static (NativeBattleGfAnimationSource Source, NativeBattleStaticMeshResource Mesh) Source()
    {
        byte[] bytes = new byte[68];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 20);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 1);
        NativeBattleGfAnimationSource source = new(0x103D46C, bytes.ToImmutableArray());
        return (source, new(0, source.Address + 16, bytes.AsSpan(16).ToArray().ToImmutableArray()));
    }
}
