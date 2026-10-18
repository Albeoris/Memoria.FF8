using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterActorAnchorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(0xF1)]
    public void Anchor_uses_parent_length_and_masks_alias_high_bit_without_fraction_override(short selector)
    {
        NativeBattleWaterActorAnchorResult result = NativeBattleWaterActorAnchor.EvaluateB66B80(Frame(), Skeleton(), Bones(), selector, 0x800, Identity());
        Assert.Equal(new NativeBattleFullMacVertex(10, 20, 80, 0), result.Anchor);
        Assert.Equal(50, result.Frame.ReadTemporaryWord(0xC4));
        Assert.Equal(0u, result.Frame.ReadTemporaryDword(0xC0));
        Assert.False(result.Frame.TemporaryFrame.Known[0xC6]);
        Assert.False(result.Frame.TemporaryFrame.Known[0xE2]);
        Assert.False(result.Frame.TemporaryFrame.Known[0xE3]);
        Assert.True(result.Frame.TemporaryFrame.Known[0xE4]);
        Assert.Equal(80u, result.Frame.ReadTemporaryDword(0xF8));
        Assert.Equal(0u, result.Frame.ReadTemporaryDword(0xFC));
    }

    [Fact]
    public void Negative_parent_uses_zero_distance_and_preserves_unwritten_temporary_bytes()
    {
        NativeBattleWaterExecutionFrame frame = Frame();
        NativeBattleWaterActorAnchorResult result = NativeBattleWaterActorAnchor.EvaluateB66B80(frame, Skeleton(), Bones(), 0, int.MaxValue, Identity());
        Assert.Equal(new NativeBattleFullMacVertex(0, 0, 0, 0), result.Anchor);
        Assert.Equal(frame.TemporaryFrame.Bytes[0xC6], result.Frame.TemporaryFrame.Bytes[0xC6]);
        Assert.Equal(frame.TemporaryFrame.Bytes[0xCF], result.Frame.TemporaryFrame.Bytes[0xCF]);
        Assert.False(result.Frame.TemporaryFrame.Known[0xCF]);
    }

    [Fact]
    public void Parent_product_wraps_before_arithmetic_shift_and_word_store()
    {
        NativeBattleWaterActorAnchorResult result = NativeBattleWaterActorAnchor.EvaluateB66B80(Frame(), Skeleton(), Bones(), 1, int.MaxValue, Identity());
        Assert.Equal(ushort.MaxValue, result.Frame.ReadTemporaryWord(0xC4));
        Assert.Equal(29, result.Anchor.Z);
    }

    [Fact]
    public void Composed_basis_keeps_full_MAC_translation_and_exact_IR_flags()
    {
        NativeBattleActionMatrix basis = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 40000, -50000, 60000, 4096);
        NativeBattleWaterActorAnchorResult result = NativeBattleWaterActorAnchor.EvaluateB66B80(Frame(), Skeleton(), Bones(), 1, 0x1000, basis);
        Assert.Equal(new NativeBattleFullMacVertex(39980, -49990, 60130, 0x81C00000), result.Anchor);
        Assert.Equal(39980u, result.Frame.ReadTemporaryDword(0xE4));
        Assert.Equal(unchecked((uint)-49990), result.Frame.ReadTemporaryDword(0xE8));
        Assert.Equal(60030u, result.Frame.ReadTemporaryDword(0xEC));
    }

    [Fact]
    public void Direct60_resolves_captured_physical_actor_uses_current_pose_and_updates_without_position_cache()
    {
        NativeBattleWaterExecutionFrame frame = Frame().WriteTemporaryWord(0x14A, 0x60);
        frame = WriteCoreDword(frame, 0x279744C, NativeBattleScriptedEffectMemory.Address);
        frame = WriteCoreDword(frame, NativeBattleScriptedEffectMemory.Address + 0x68, 0x1D97400);
        frame = frame with { Core = frame.Core.MarkUnknown(0x2797454, 128) };
        byte[] cached = Enumerable.Repeat((byte)0xA6, 12).ToArray();
        byte[] angles = [0x11, 0x22, 0x33, 0x44, 0x55, 0x66];
        frame = frame with { Arena = frame.Arena.Write(0x201B, [2]).Write(0x2050, cached).Write(0x208C, angles) };
        NativeBattleWaterSlotBank bank = new(0, 0x4000, 2, 0x2000);
        NativeBattleWaterScriptProgram program = new(0x100, 4, NativeBattleEffectNodeMemory.ArenaAddress + 0x100);
        frame = frame with { Arena = frame.Arena.Write(0x102, [1, 0]) };
        int calls = 0;
        NativeBattleWaterExecutionFrame result = NativeBattleWaterActorAnchor.Execute60AA2190(frame, program, bank, (current, actor) =>
        {
            calls++;
            Assert.Equal(0x1D97400u, actor);
            Assert.Equal(CoreDword(frame, 0x27973E8), CoreDword(current, 0x27973E8));
            return new(Skeleton(), Bones(), Identity());
        });
        Assert.Equal(1, calls);
        Assert.Equal(10u << 16, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x205C, 4)));
        Assert.Equal(20u << 16, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x2060, 4)));
        Assert.Equal(130u << 16, BinaryPrimitives.ReadUInt32LittleEndian(result.Arena.Read(0x2064, 4)));
        Assert.Equal(new byte[] { 10, 0, 20, 0, 130, 0 }, result.Arena.Read(0x2094, 6));
        Assert.Equal(cached, result.Arena.Read(0x2050, 12));
        Assert.Equal(angles, result.Arena.Read(0x208C, 6));
        Assert.Equal(program.Address + 4, CoreDword(result, 0x2797450));
    }

    [Fact]
    public void Unknown_padding_and_next_anchor_survive_independent_JSON()
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleWaterActorAnchor.EvaluateB66B80(Frame(), Skeleton(), Bones(), 1, 0x1000, Identity()).Frame;
        NativeBattleWaterExecutionFrame restored = JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleWaterActorAnchorResult expected = NativeBattleWaterActorAnchor.EvaluateB66B80(frame, Skeleton(), Bones(), 0xF1, -0x800, Identity());
        NativeBattleWaterActorAnchorResult actual = NativeBattleWaterActorAnchor.EvaluateB66B80(restored, Skeleton(), Bones(), 0xF1, -0x800, Identity());
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(0x7FFF)]
    public void Unsupported_bone_owner_does_not_fall_back(short selector)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterActorAnchor.EvaluateB66B80(Frame(), Skeleton(), Bones(), selector, 0x1000, Identity()));
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static ImmutableArray<NativeBattleActionMatrix> Bones() => [Identity(), Identity() with { X = 10, Y = 20, Z = 30 }];

    private static ImmutableArray<byte> Skeleton()
    {
        byte[] skeleton = new byte[0x70];
        skeleton[0] = 2;
        skeleton[5] = 0x81;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x10), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x12), 100);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x42), 900);
        return skeleton.ToImmutableArray();
    }

    private static NativeBattleWaterExecutionFrame Frame()
    {
        NativeBattleWaterExecutionFrame frame = new(NativeBattleScriptedEffectMemory.FromColdProcess(), NativeBattleEffectArena.Clear(), new(0x1A77238, Enumerable.Repeat((byte)0xB7, 0x180).ToImmutableArray(), Enumerable.Repeat(false, 0x180).ToImmutableArray()), new(7));
        frame = WriteCoreDword(frame, 0x27973E8, NativeBattleEffectNodeMemory.ArenaAddress + 0x2000);
        frame = WriteCoreDword(frame, 0x2797450, NativeBattleEffectNodeMemory.ArenaAddress + 0x100);
        return frame with { Core = frame.Core.Write(0x2797454, [0, 0xFF]) };
    }
}
