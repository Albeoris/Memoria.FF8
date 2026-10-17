using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGfMeshAnchorsTests
{
    [Theory]
    [InlineData(0, -2, 1001, -98, 32767)]
    [InlineData(1, -2, 1004, -95, 32767)]
    [InlineData(2, 3, 32767, -32768, 9)]
    public void FlattenedVertexIndexSubtractsGroupsAndUsesTheSignedBoneSelectorWithNativeIRClamping(short vertex, int bone, short x, short y, short z)
    {
        List<int> selected = [];
        NativeBattleVector result = NativeBattleGfMeshAnchors.Resolve684BA0(Geometry(), 0, vertex, index =>
        {
            selected.Add(index);
            return new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], index == -2 ? 1000 : 40000, index == -2 ? -100 : -40000, index == -2 ? 32766 : 0, 4096);
        });
        Assert.Equal(new[] { bone }, selected);
        Assert.Equal(new NativeBattleVector(x, y, z), result);
    }

    [Fact]
    public void UnpopulatedAndNegativeSelectorsNeverBorrowUnwrittenTemporaryPointerBytes()
    {
        foreach (short vertex in new short[] { -1, 3 })
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleGfMeshAnchors.Resolve684BA0(Geometry(), 0, vertex, _ => throw new InvalidOperationException()));
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleGfMeshAnchors.Resolve684BA0(Geometry(), -1, 0, _ => throw new InvalidOperationException()));
        Assert.Throws<NotSupportedException>(() => NativeBattleGfMeshAnchors.Resolve684BA0(Geometry(), 1, 0, _ => throw new InvalidOperationException()));
    }

    [Fact]
    public void OriginalRandomSelectionUsesElevenWayUnsignedProductAndSignedWORDMetadata()
    {
        NativeBattleQuezacotlResources resources = new(null!, null!, null!, null!) { AnchorVertexIndices11 = [93, 33, 696, 562, 578, 450, 9, 240, 388, 408, 265] };
        ushort random = 1;
        Assert.Equal((short)93, NativeBattleQuezacotlParticleFlight.SelectVertex12AFE40(resources, ref random));
        Assert.Equal((ushort)139, random);
        Assert.Equal((short)450, NativeBattleQuezacotlParticleFlight.SelectVertex12AFE40(resources, ref random));
        Assert.Equal((ushort)17389, random);
        random = 1;
        Assert.Equal((short)-1, NativeBattleQuezacotlParticleFlight.SelectVertex12AFE40(resources with { AnchorVertexIndices11 = resources.AnchorVertexIndices11!.Value.SetItem(0, -1) }, ref random));
        resources = resources with { AnchorVertexIndices11 = null };
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlParticleFlight.SelectVertex12AFE40(resources, ref random));
    }

    private static ImmutableArray<byte> Geometry()
    {
        byte[] bytes = new byte[36];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        short[] words = [2, -2, 2, 1, 2, 3, 4, 5, 6, 3, 1, 7, 8, 9];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + 2 * index), words[index]);
        }
        return bytes.ToImmutableArray();
    }
}
