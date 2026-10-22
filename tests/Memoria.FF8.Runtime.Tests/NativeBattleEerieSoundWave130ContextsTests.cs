using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEerieSoundWave130Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEerieSoundWave130ContextsTests
{
    [Fact]
    public void Composed_context_publishes_exact_matrix_translation_depth_and_scratch_without_padding()
    {
        NativeBattleActionMatrix view = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 100, 200, 300, 4096);
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770() with { X = 10, Y = 20, Z = 30 };
        NativeBattleEffectArena context = NativeBattleEerieSoundWave130Contexts.Compose(NativeBattlePackedMatrix32.FromMatrix(view, 0xABCD), local, -70000, ArenaAddress + MorphOffset);
        Assert.Equal(130, I32(context, 20));
        Assert.Equal(220, I32(context, 24));
        Assert.Equal(290, I32(context, 28));
        Assert.Equal(-70000, I32(context, 32));
        Assert.Equal(ArenaAddress + MorphOffset, unchecked((uint)I32(context, 36)));
        Assert.Throws<NotSupportedException>(() => context.Read(18, 2));
    }

    [Fact]
    public void Impact_unknown_R22_affects_only_composed_third_column_not_translation()
    {
        NativeBattleActionMatrix view = new([100, 200, 300, 400, 500, 600, 700, 800, 900], 7, 11, 13, 4096);
        NativeBattleEffectArena context = NativeBattleEerieSoundWave130Contexts.Impact(NativeBattlePackedMatrix32.FromMatrix(view, 0xABCD), new(4096, 0, 0), ArenaAddress + MorphOffset);
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 2; column++)
            {
                Assert.Equal(view.Rotation[row * 3 + column], BinaryPrimitives.ReadInt16LittleEndian(context.Read((row * 3 + column) * 2, 2)));
            }
            Assert.Throws<NotSupportedException>(() => context.Read((row * 3 + 2) * 2, 2));
        }
        Assert.Throws<NotSupportedException>(() => context.Read(18, 2));
        Assert.Equal(107, I32(context, 20));
        Assert.Equal(411, I32(context, 24));
        Assert.Equal(713, I32(context, 28));
        Assert.Equal(-128, I32(context, 32));
    }

    [Fact]
    public void Partial_impact_context_JSON_retains_exact_unknown_columns_and_inputs_are_required()
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        NativeBattleEffectArena context = NativeBattleEerieSoundWave130Contexts.Impact(packed, new(-12, 34, -56), ArenaAddress + MorphOffset);
        NativeBattleEffectArena restored = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(context))!;
        Assert.Equal(context.ReadSnapshot(0, 40).Known, restored.ReadSnapshot(0, 40).Known);
        Assert.Equal(context.ReadSnapshot(0, 40).Data, restored.ReadSnapshot(0, 40).Data);
        packed = packed with { Known = packed.Known.SetItem(0, false) };
        Assert.Throws<NotSupportedException>(() => NativeBattleEerieSoundWave130Contexts.Impact(packed, new(0, 0, 0), 0));
    }

    private static int I32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt32LittleEndian(arena.Read(offset, 4));
}
