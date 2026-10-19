using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274HierarchyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(4096, 4096)]
    public void NativeSkipGuardsDoNotReadSourceContextOrProjection(short scale, short fade)
    {
        NativeBattleGfAnimationNode node = Node(scale, fade, 0, 0);
        NativeBattleDuel274Frame frame = Frame();
        NativeBattleDuel274HierarchyStep step = NativeBattleDuel274Hierarchy.Draw68A400(frame, null!, null!, node, null!, null!, null!);
        Assert.Same(frame, step.Frame);
        Assert.Empty(step.Batch.Attempts);
    }

    [Fact]
    public void IntermediateShapePublishesOnlyActualXyzAndRetainsDestinationPadding()
    {
        NativeBattleDuel274Resources resources = Resources();
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(116, Enumerable.Repeat((byte)0x77, 8).ToArray());
        NativeBattleDuel274Frame frame = Frame() with { Memory = Frame().Memory with { Arena = arena } };
        NativeBattleDuel274HierarchyContext context = new(Identity(), null, null, 1, null, NativeBattleEffectNodeMemory.ArenaAddress + 116);
        NativeBattleDuel274HierarchyStep step = NativeBattleDuel274Hierarchy.Draw68A400(frame, resources, resources.Hierarchy(NativeBattleDuel274Resources.Address + 0x80), Node(4096, 0, 1, 2048), context, null!, null!);
        byte[] stored = step.Frame.Memory.Arena!.Read(116, 8);
        Assert.Equal(new short[] { 20, 30, 40 }, Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadInt16LittleEndian(stored.AsSpan(index * 2))));
        Assert.Equal(new byte[] { 0x77, 0x77 }, stored.AsSpan(6, 2).ToArray());
        Assert.Empty(step.Batch.Attempts);
        Assert.Throws<NotSupportedException>(() => NativeBattleDuel274Hierarchy.Draw68A400(frame, resources, resources.Hierarchy(NativeBattleDuel274Resources.Address + 0x80), Node(4096, 0, 1, 2048), context with { MorphAddress = null }, null!, null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4096)]
    public void EndpointShapesDoNotConsumeUnwrittenMorphOrModeOneContextFields(short ratio)
    {
        NativeBattleDuel274Resources resources = Resources();
        NativeBattleDuel274HierarchyStep step = NativeBattleDuel274Hierarchy.Draw68A400(Frame(), resources, resources.Hierarchy(NativeBattleDuel274Resources.Address + 0x80), Node(4096, 0, 1, ratio), new(Identity(), null, null, 1, null, null), null!, null!);
        Assert.Empty(step.Batch.Attempts);
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    private static NativeBattleDuel274Frame Frame() => new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0), 0), NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()), new(1));

    private static NativeBattleGfAnimationNode Node(short scale, short fade, short secondShape, short ratio)
    {
        byte[] data = new byte[44];
        for (int offset = 24; offset <= 28; offset += 2)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), scale);
        }
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(36), fade);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), ratio);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), secondShape);
        return new(0, 0, data.ToImmutableArray());
    }

    private static NativeBattleDuel274Resources Resources()
    {
        byte[] data = new byte[checked((int)(NativeBattleDuel274Resources.EndAddress - NativeBattleDuel274Resources.Address))];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x88), 0x80);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x100), 28);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x104), 1);
        for (int component = 0; component < 3; component++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x10C + component * 2), (short)(10 + component * 10));
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x114 + component * 2), (short)(30 + component * 10));
        }
        return new(data.ToImmutableArray(), []);
    }
}
