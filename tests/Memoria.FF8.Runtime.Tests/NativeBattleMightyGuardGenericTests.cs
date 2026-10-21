using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMightyGuardGenericTests
{
    [Theory]
    [InlineData(73)]
    [InlineData(78)]
    public void Generic_frame_preserves_actual_private_cursors_counters_and_one_retained_bank(ushort effect)
    {
        NativeBattleMightyGuardFrame frame = Frame(effect);
        NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForGenericNodes(effect);
        Assert.False(profile.UsesSplitPacketCursors);
        Assert.Equal(35, profile.EmitterCapacity);
        Assert.Equal(100, profile.GeometryCapacity);
        Assert.Equal(0xEC4, profile.EmitterStorageLength);
        Assert.Equal(0x10680, profile.GeometryStorageLength);
        Assert.Equal(frame.Profile.GenericCallback, profile.Callback(0x87E1A0));
        NativeBattleCuragaFrame generic = frame.ToGeneric();
        Assert.Equal((ushort)7, generic.State.Counter26D76C0);
        Assert.Equal((ushort)11, generic.State.Counter26D82E8);
        Assert.Equal((ushort)13, generic.State.Counter26D75C4);
        Assert.Equal((ushort)17, generic.State.Counter26D82D8);
        Assert.Equal((ushort)19, generic.State.PassCount26D82EA);
        Assert.Equal((ushort)23, generic.State.PassCount26D82DA);
        byte[] bank = new byte[profile.ExecutableLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bank, 1);
        bank[0x104] = 0xA5;
        NativeBattleCuragaExecutableState updated = new(1, bank.ToImmutableArray()) { ResourceAddress = profile.ExecutableAddress };
        generic = generic with { Memory = profile.WithExecutable(generic.Memory, updated), State = generic.State with { PrimaryDrawCursor = 1232, SecondaryDrawCursor = 5678, Counter26D76C0 = 29, PassCount26D82DA = 31 } };
        NativeBattleMightyGuardFrame result = frame.WithGeneric(generic);
        Assert.Equal(1232, result.State.PrimaryDrawCursor);
        Assert.Equal(5678, result.State.SecondaryDrawCursor);
        Assert.Equal((ushort)29, result.State.EmitterScan);
        Assert.Equal((ushort)31, result.State.PassGeometry);
        Assert.Equal((ushort)13, result.State.EmitterReset);
        Assert.Equal((ushort)17, result.State.GeometryReset);
        Assert.Equal(1u, result.State.InitializationFlag);
        Assert.Equal(bank, result.State.Executable!.Value.ToArray());
        Assert.Same(result.State, result.Memory.RequireMightyGuard(effect));
        Assert.Same(frame.Memory.SharedEffectView2793E58, result.Memory.SharedEffectView2793E58);
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(result.Memory))!;
        Assert.Equal(bank, restored.RequireMightyGuard(effect).Executable!.Value.ToArray());
        Assert.Equal((ushort)29, restored.RequireMightyGuard(effect).EmitterScan);
        Assert.Equal(1232, restored.RequireMightyGuard(effect).PrimaryDrawCursor);
    }

    [Theory]
    [InlineData(73)]
    [InlineData(78)]
    public void Both_sprite_and_mesh_packets_append_to_private_arena_without_consuming_COMMON(ushort effect)
    {
        NativeBattleCuragaFrame frame = Frame(effect).ToGeneric();
        NativeBattleGeometryPrimitive primitive = new(17, [0x01000000, 0xE1000220], []);
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, primitive.Words[0]);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), primitive.Words[1]);
        NativeBattleGfGeometryBatch batch = new([new(0, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 8).ToImmutableArray(), primitive)], 8);
        foreach (bool meshes in new[] { false, true })
        {
            NativeBattleCuragaNodeDrawStep result = NativeBattleCuragaNodeKinematics.CommitBatch(frame, NativeBattleLogicalOrdering.Clear(), batch, meshes);
            Assert.Equal(frame.State.PrimaryDrawCursor + 8, result.Frame.State.PrimaryDrawCursor);
            Assert.Equal(frame.State.SecondaryDrawCursor, result.Frame.State.SecondaryDrawCursor);
            Assert.Equal(0, result.Frame.Memory.PrimitiveProcess!.ByteCursor);
            Assert.Equal(bytes.AsSpan(4).ToArray(), result.Frame.Memory.Arena!.Read(frame.State.PrimaryDrawCursor!.Value + 4, 4));
        }
    }

    [Fact]
    public void Missing_retained_owner_and_foreign_binding_fail_before_node_execution()
    {
        NativeBattleMightyGuardFrame frame = Frame(78);
        Assert.Throws<NotSupportedException>(() => NativeBattleCuragaFamilyProfile.ForGenericNodes(78).RequireExecutable(frame.Memory with { MightyGuard78 = null }));
        Assert.Throws<InvalidDataException>(() => (frame with { Effect = frame.Effect with { Binding = frame.Effect.Binding with { Entry = 0 } } }).ToGeneric());
    }

    private static NativeBattleMightyGuardFrame Frame(ushort effect)
    {
        NativeBattleMightyGuardProfile profile = NativeBattleMightyGuardProfile.ForEffect(effect);
        NativeBattleMightyGuardState state = NativeBattleMightyGuardState.FromColdProcess(effect) with { SourceArenaOffset = 0x1000, PrimaryDrawCursor = 0x1000, SecondaryDrawCursor = 0xA000, AuxiliaryCursor = 0x13000, Emitters = 0x13000, Geometry = 0x13EC4, EmitterScan = 7, GeometryScan = 11, EmitterReset = 13, GeometryReset = 17, PassEmitters = 19, PassGeometry = 23 };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().WithMightyGuard(state) with { Arena = NativeBattleEffectArena.Clear(), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), SharedEffectView2793E58 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0xBEEF) };
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), null, null);
        return new(memory, selected, state, new(0));
    }
}
