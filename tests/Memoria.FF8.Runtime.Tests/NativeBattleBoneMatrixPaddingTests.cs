using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBoneMatrixPaddingTests
{
    [Theory]
    [InlineData(1024, -4096)]
    [InlineData(-1024, 4096)]
    [InlineData(0, 0)]
    public void Euler_padding_is_the_actual_preceding_X_product_WORD5(short pitch, short expected)
    {
        NativeBattleVector angles = new(pitch, 721, -357);
        Assert.Equal(expected, NativeBattleBoneMatrixPadding.Euler56CE30(angles));
        Assert.Equal(NativeBattleGfMatrices.Euler56CE30(angles with { Y = 0 }).Rotation[5], NativeBattleBoneMatrixPadding.Euler56CE30(angles));
        Assert.Equal((short)-1, NativeBattleBoneMatrixPadding.UnscaledChild508C90(angles with { Y = -1 }));
        Assert.Equal((short)0, NativeBattleBoneMatrixPadding.UnscaledChild508C90(angles with { Y = 1 }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Actual_composition_padding_uses_float_residue_then_the_preceding_stored_Z_double(int parity)
    {
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770() with { Rotation = [4096, -2048, 0, 0, 4096, 0, 0, 0, 4096] };
        NativeBattleActionMatrix basis = NativeBattleCuragaMatrices.Identity8DD770();
        ImmutableArray<NativeBattleActionMatrix> bones = [basis with { Z = 65537 }, basis with { Z = -65537 }, basis];
        ImmutableArray<short?> padding = NativeBattleBoneMatrixPadding.Compose5095B0After62C83D(local, basis, bones, parity);
        Assert.Equal((short?)unchecked((short)0xC500), padding[0]);
        if (parity == 0)
        {
            Assert.All(padding, value => Assert.Equal(padding[0], value));
        }
        else
        {
            Assert.Equal((short?)unchecked((short)0x8001), padding[1]);
            Assert.Equal((short?)0x7FFE, padding[2]);
        }
        NativeBattleExecutionStack profile = new((uint)(0x200000 + parity), 0x569C3D, NativeFieldRuntimeProfile.FF8EnExecutableHash, new string('A', 64), 1, 1);
        Assert.Equal((int?)parity, NativeBattleEffect326Stack.ResolveLateModelDrawEntryModulo8(profile, 4));
        Assert.Null(NativeBattleEffect326Stack.ResolveLateModelDrawEntryModulo8(profile, 1));
    }

    [Fact]
    public void Missing_stack_profile_keeps_only_the_independently_produced_first_WORD_known()
    {
        NativeBattleActionMatrix basis = NativeBattleCuragaMatrices.Identity8DD770();
        ImmutableArray<short?> padding = NativeBattleBoneMatrixPadding.Compose5095B0After62C83D(basis, basis, [basis, basis], null);
        Assert.Equal((short?)0, padding[0]);
        Assert.Null(padding[1]);
        Assert.Null(NativeBattleEffect326Stack.ResolveLateModelDrawEntryModulo8(null, 4));
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(50, 2);
        Assert.Throws<NotSupportedException>(() => arena.Read(0, 128));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Scaled_restore_preserves_composed_padding_values_and_knownness_through_JSON(bool transactional)
    {
        NativeBattleEffectArena arena = Skeleton();
        arena = arena.Write(50, [0x34, 0x12]).Write(98, [0xCD, 0xAB]).Write(146, [0x78, 0x56]).MarkUnknown(147, 1);
        NativeBattleDecodedPose pose = new(new(0, 0, 0), Enumerable.Repeat(new NativeBattleBonePose(new(311, -700, 177), new(1024, 1024, 1024)), 3).ToImmutableArray(), true);
        using NativeBattleEffectArenaTransaction? transaction = transactional ? new(arena) : null;
        NativeBattleEffectArena actual = NativeBattleArenaPose.RestoreBoneMatrices(transaction?.View ?? arena, 0, pose);
        actual = transaction?.Freeze() ?? actual;
        Assert.Equal(new byte[] { 0x34, 0x12 }, actual.Read(50, 2));
        Assert.Equal(new byte[] { 0xCD, 0xAB }, actual.Read(98, 2));
        Assert.Equal((byte)0x78, actual.Read(146, 1)[0]);
        Assert.Throws<NotSupportedException>(() => actual.Read(147, 1));
        Assert.Equal(new byte[] { 0x78, 0x56 }, actual.ReadSnapshot(146, 2).Data);
        NativeBattleEffectArena restored = JsonSerializer.Deserialize<NativeBattleEffectArena>(JsonSerializer.Serialize(actual))!;
        Assert.Equal(actual.ReadSnapshot(146, 2).Known, restored.ReadSnapshot(146, 2).Known);
        Assert.Equal(actual.Read(98, 2), restored.Read(98, 2));
    }

    private static NativeBattleEffectArena Skeleton()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(2, BitConverter.GetBytes((short)4096));
        for (int bone = 0; bone < 3; bone++)
        {
            byte[] definition = new byte[4];
            BinaryPrimitives.WriteInt16LittleEndian(definition, (short)(bone - 1));
            BinaryPrimitives.WriteInt16LittleEndian(definition.AsSpan(2), 100);
            arena = arena.Write(16 + bone * 48, definition);
        }
        return arena;
    }
}
