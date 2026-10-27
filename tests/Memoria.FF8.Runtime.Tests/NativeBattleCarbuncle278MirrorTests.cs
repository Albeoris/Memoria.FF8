using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCarbuncle278MirrorTests
{
    [Fact]
    public void Mirror_transform_keeps_original_quotient_rounding_and_signed_scratch_focal()
    {
        NativeBattleCameraProjection source = new(null, 300, 160, 108);
        NativeBattleCarbuncle278MirrorTransform actual = NativeBattleCarbuncle278Mirror.Transform6812E0(new(0, 0, -400), new(new(-1606, 0, -400), new(1606, 0, 3200)), -216, source);
        Assert.Equal(new short[] { 163, 0, 0, 0, 0, -291, 0, 0, 0 }, actual.Matrix.Rotation);
        Assert.Equal(0, actual.Matrix.X);
        Assert.Equal(67, actual.Matrix.Y);
        Assert.Equal(-216, actual.Matrix.Z);
        Assert.Equal((ushort)300, actual.Projection.FocalLength);
        Assert.Equal((short)64, actual.Projection.OffsetX);
        Assert.Equal((short)128, actual.Projection.OffsetY);
        Assert.Equal((short)160, actual.PriorOffsetX);
        Assert.Equal((short)108, actual.PriorOffsetY);
        Assert.Null(source.View);
        NativeBattleCarbuncle278MirrorTransform exact = NativeBattleCarbuncle278Mirror.Transform6812E0(new(121, 0, 121), new(new(0, 0, 0), new(1280, 0, 2560)), 200, source);
        Assert.Equal(-69, exact.Matrix.X);
        Assert.Equal(127, exact.Matrix.Y);
    }

    [Fact]
    public void Original_zero_extent_division_is_not_replaced_with_a_default_projection()
    {
        Assert.Throws<DivideByZeroException>(() => NativeBattleCarbuncle278Mirror.Transform6812E0(new(0, 0, 0), new(new(0, 0, 0), new(0, 0, 256)), 200, new(null, null, null, null)));
        Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278Mirror.Transform6812E0(new(0, 0, 0), new(new(0, 0, 0), new(128, 0, 256)), 200, new(null, null, null, null)));
    }

    [Fact]
    public void Head_insertion_executes_offset_area_black_and_restore_with_exact_private_allocation()
    {
        NativeBattleEffectMemory memory = Fixture();
        NativeBattleCarbuncle278MirrorWrite result = NativeBattleCarbuncle278Mirror.Append6812E0(NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(memory)), NativeBattleLogicalOrdering.Clear(), 0x1000, 0x1C);
        byte[] lengths = new byte[256];
        for (int opcode = 0xE1; opcode <= 0xE6; opcode++)
        {
            lengths[opcode] = 1;
        }
        lengths[0x28] = 5;
        ImmutableArray<NativeBattleLogicalCommand> commands = result.Ordering.Traverse(lengths.ToImmutableArray());
        Assert.Equal(new byte[] { 0xE5, 0xE3, 0xE4, 0x28, 0xE3, 0xE4, 0xE5, 0xE1, 0xE2 }, commands.Select(command => command.Opcode));
        Assert.Equal(0x1070, result.Cursor);
        Assert.Equal(new[] { 0x1000, 0x1040, 0x1058, 0x1064 }, result.Ordering.Proxies.Select(proxy => proxy.Packet.ByteOffset));
        Assert.Equal(new ushort[] { 0, 1, 0, 0 }, result.Ordering.Proxies.Select(proxy => proxy.Flags20));
        Assert.All(commands, command => Assert.Equal("20DFAB8", command.Packet.Bank));
        Assert.Equal(0xE5000000u | 704u | 256u << 11, commands[0].Words[0]);
        Assert.Equal(0x28000000u, commands[3].Words[0]);
        Assert.Equal(new uint[] { 0x28000000, 0, 128, 256u << 16, (256u << 16) | 128 }, commands[3].Words);
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena!.Read(0x1018, 40));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena!.Read(0x106C, 4));
        Assert.Equal(NativeBattleTestSnapshots.Json(memory), NativeBattleTestSnapshots.Json(Fixture()));
    }

    [Fact]
    public void Independent_restore_preserves_ordered_packets_and_unsupported_model_branch_does_not_mutate_source()
    {
        NativeBattleEffectMemory source = Fixture();
        string original = NativeBattleTestSnapshots.Json(source);
        NativeBattleCarbuncle278MirrorWrite first = NativeBattleCarbuncle278Mirror.Append6812E0(source, NativeBattleLogicalOrdering.Clear(), 0x1000, 0);
        NativeBattleEffectMemory independent = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(original)!;
        NativeBattleCarbuncle278MirrorWrite second = NativeBattleCarbuncle278Mirror.Append6812E0(independent, NativeBattleLogicalOrdering.Clear(), 0x1000, 0);
        Assert.Equal(NativeBattleTestSnapshots.Json(first), NativeBattleTestSnapshots.Json(second));
        string before = NativeBattleTestSnapshots.Json(source);
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => NativeBattleCarbuncle278Mirror.Append6812E0(source, NativeBattleLogicalOrdering.Clear(), 0x1000, 0x100));
        Assert.Contains("1D989D0", error.Message);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(source));
    }

    private static NativeBattleEffectMemory Fixture()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        return memory with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0x1018, 40).MarkUnknown(0x106C, 4), PrimitiveProcess = memory.PrimitiveProcess!.Initialize500400() };
    }
}
