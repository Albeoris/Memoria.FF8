using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleElectricDischarge126Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCpuTrailsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bone_birth_retains_the_full_native_first_argument_for_the_anchored_direction_stack(bool previousSurvivor)
    {
        Fixture fixture = new();
        NativeBattleCpuTrailFrame frame = fixture.Frame;
        uint previous = 0;
        if (previousSurvivor)
        {
            (frame, previous) = NativeBattleCpuTrails.Append(frame, fixture.Layout.BonePoolIndex, fixture.Layout.BoneCallback);
        }
        NativeBattleCpuTrailAddressStep created = NativeBattleCpuTrails.CreateBoneTrail(frame, fixture.Layout, fixture.Path, new(null, 0x1951000), BonePathsAddress, 0, 1, 4096);
        uint expected = (previous & 0xFFFF0000u) | 2u;
        int draws = 0;
        NativeBattleCpuTrailTaskStep actual = NativeBattleCpuTrails.AdvanceBoneTrail(created.Frame, created.Address, fixture.Layout, fixture.Path, (current, _, _, _) =>
        {
            Assert.Equal(expected, current.BoneDirectionWord);
            draws++;
            return current;
        });
        Assert.Equal(1, draws);
        NativeBattleModulePoolTask task = NativeBattleCpuTrails.Read(actual.Frame, created.Address);
        NativeBattleCpuTrailFrame noBirth = NativeBattleCpuTrails.Write(actual.Frame, created.Address, task.WriteDword(24, 0));
        NativeBattleSharedEffectRandomState? random = noBirth.Memory.SharedEffectRandom;
        NativeBattleCpuTrails.AdvanceBoneTrail(noBirth, created.Address, fixture.Layout, fixture.Path, (current, _, _, _) =>
        {
            Assert.Null(current.BoneDirectionWord);
            Assert.Equal(random, current.Memory.SharedEffectRandom);
            return current;
        });
    }

    [Fact]
    public void Negative_bone_path_replaces_the_saved_register_upper_word_with_the_actual_path_pointer()
    {
        Fixture fixture = new();
        NativeBattleCpuTrailAddressStep created = NativeBattleCpuTrails.CreateBoneTrail(fixture.Frame, fixture.Layout, fixture.Path, new(null, 0x1951000), BonePathsAddress, 0, 1, 4096);
        int draws = 0;
        NativeBattleCpuTrails.AdvanceBoneTrail(created.Frame, created.Address, fixture.Layout, address => address == BonePathsAddress + 20 ? (short)-1 : fixture.Path(address), (current, _, _, _) =>
        {
            Assert.Equal((BonePathsAddress & 0xFFFF0000u) | 10u, current.BoneDirectionWord);
            draws++;
            return current;
        });
        Assert.Equal(1, draws);
    }

    [Fact]
    public void World_birth_uses_actual_layout_three_shared_draws_and_preserves_unwritten_point_lanes()
    {
        Fixture fixture = new();
        NativeBattleCpuTrailAddressStep actual = NativeBattleCpuTrails.StartWorld(fixture.Frame, fixture.Layout, new(1, 2, 3), new(4, 5, 6), new(7, 8, 9), null, 2, 100, 50, 80, 16, 3);
        Assert.Equal(fixture.Layout.WorldBankAddress, actual.Address);
        NativeBattleModulePoolTask task = actual.Frame.Effect.Pools[2].Slots[0];
        Assert.Equal(WorldTrailCallback, task.Callback);
        Assert.Equal(actual.Address, task.ReadDword(0x10));
        Assert.Equal(actual.Address, task.ReadDword(0x14));
        Assert.Equal((ushort)3, task.ReadWord(0x2E));
        NativeBattleSharedEffectRandomState random = new(1);
        for (int axis = 0; axis < 3; axis++)
        {
            random = random.Draw7059E0(out ushort value);
            Assert.Equal(unchecked((ushort)((value & 255) - 128)), task.ReadWord(0x30 + axis * 2));
        }
        Assert.Equal(random, actual.Frame.Memory.SharedEffectRandom);
        Assert.Equal((short)80, fixture.Word(actual.Frame, actual.Address + 6));
        Assert.Equal((short)512, fixture.Word(actual.Frame, actual.Address + 32));
        Assert.Throws<NotSupportedException>(() => actual.Frame.Memory.Arena!.Read(WorldPointsOffset + 8, 4));
    }

    [Fact]
    public void Cached_world_point_uses_supplied_drift_without_random_and_full_pool_releases_only_head()
    {
        Fixture fixture = new();
        uint cached = fixture.Layout.WorldBankAddress + 5 * 40;
        NativeBattleCpuTrailFrame frame = fixture.Dword(fixture.Frame, fixture.Layout.WorldFreePointerAddress, cached);
        NativeBattleCpuTrailAddressStep actual = NativeBattleCpuTrails.StartWorld(frame, fixture.Layout, new(1, 2, 3), new(4, 5, 6), new(7, 8, 9), new(10, 11, 12), 0, 0, 1, 80, 0, 0);
        Assert.Equal(cached, actual.Address);
        Assert.Equal(frame.Memory.SharedEffectRandom, actual.Frame.Memory.SharedEffectRandom);
        Assert.Equal(0u, fixture.ReadDword(actual.Frame, fixture.Layout.WorldFreePointerAddress));
        frame = fixture.Frame;
        for (int index = 0; index < WorldCount; index++)
        {
            (frame, _) = NativeBattleCpuTrails.Append(frame, 2, WorldTrailCallback);
        }
        actual = NativeBattleCpuTrails.StartWorld(frame, fixture.Layout, new(1, 2, 3), new(4, 5, 6), new(7, 8, 9), null, 0, 0, 1, 80, 0, 0);
        Assert.Equal(0u, actual.Address);
        Assert.Equal((short)-1, fixture.Word(actual.Frame, fixture.Layout.WorldBankAddress + 6));
        Assert.Equal(frame.Memory.SharedEffectRandom, actual.Frame.Memory.SharedEffectRandom);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bone_binding_retains_actual_CPU_or_staging_owner_through_JSON_and_next_draw(bool cpu)
    {
        Fixture fixture = new();
        NativeBattleResourceRowReference binding = cpu ? new(3, null) : new(null, 0x12345678);
        NativeBattleCpuTrailAddressStep created = NativeBattleCpuTrails.CreateBoneTrail(fixture.Frame, fixture.Layout, fixture.Path, binding, BonePathsAddress, 0, 1, 4096);
        NativeBattleModulePoolTask task = NativeBattleCpuTrails.Read(created.Frame, created.Address);
        Assert.Equal(binding, task.ReadResourceRowReference(36));
        if (cpu)
        {
            Assert.Throws<NotSupportedException>(() => task.ReadDword(36));
        }
        else
        {
            Assert.Equal(binding.StagingAddress, task.ReadDword(36));
        }
        NativeBattleCpuTrailFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleCpuTrailFrame>(NativeBattleTestSnapshots.Json(created.Frame))!;
        int draws = 0;
        NativeBattleCpuTrailTaskStep Step(NativeBattleCpuTrailFrame frame) => NativeBattleCpuTrails.AdvanceBoneTrail(frame, created.Address, fixture.Layout, fixture.Path, (current, row, head, count) =>
        {
            Assert.Equal(binding, row);
            Assert.Equal(fixture.Layout.BoneBankAddress, head);
            Assert.Equal((short)2, count);
            draws++;
            return current with { Staging = NativeBattleFileStaging.Unknown(), Memory = current.Memory.SeedSharedEffectRandom7059D0(99) };
        });
        NativeBattleCpuTrailTaskStep expected = Step(created.Frame);
        NativeBattleCpuTrailTaskStep actual = Step(restored);
        Assert.Equal(2, draws);
        Assert.Equal(0, actual.Result);
        Assert.NotNull(actual.Frame.Staging);
        Assert.Equal(new NativeBattleSharedEffectRandomState(99), actual.Frame.Memory.SharedEffectRandom);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected.Frame), NativeBattleTestSnapshots.Json(actual.Frame));
    }

    [Fact]
    public void Early_bone_sentinel_releases_chain_without_draw_or_new_random()
    {
        Fixture fixture = new();
        NativeBattleCpuTrailAddressStep created = NativeBattleCpuTrails.CreateBoneTrail(fixture.Frame, fixture.Layout, fixture.Path, new(3, null), BonePathsAddress, 0, 1, 4096);
        NativeBattleCpuTrailTaskStep actual = NativeBattleCpuTrails.AdvanceBoneTrail(created.Frame, created.Address, fixture.Layout, address => address == BonePathsAddress + 24 ? short.MaxValue : fixture.Path(address), (_, _, _, _) => throw new InvalidOperationException());
        Assert.Equal(2, actual.Result);
        Assert.Equal(created.Frame.Memory.SharedEffectRandom, actual.Frame.Memory.SharedEffectRandom);
        Assert.Equal((short)-1, fixture.Word(actual.Frame, fixture.Layout.BoneBankAddress));
        Assert.Equal(fixture.Layout.BoneBankAddress, fixture.ReadDword(actual.Frame, fixture.Layout.BoneFreePointerAddress));
    }

    [Fact]
    public void Missing_arena_and_unknown_cached_pointer_fail_without_initializing_an_alternate_owner()
    {
        Fixture fixture = new();
        Assert.Throws<NotSupportedException>(() => NativeBattleCpuTrails.AllocateWorldPoint(fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = null } }, fixture.Layout, new(1, 2, 3), 80));
        NativeBattleCpuTrailFrame unknown = fixture.Frame with { Memory = fixture.Frame.Memory with { Arena = fixture.Frame.Memory.Arena!.MarkUnknown(WorldFreePointerOffset, 4) } };
        Assert.Throws<NotSupportedException>(() => NativeBattleCpuTrails.AllocateWorldPoint(unknown, fixture.Layout, new(1, 2, 3), 80));
    }

    private sealed class Fixture
    {
        public NativeBattleCpuTrailLayout Layout { get; } = new(ArenaAddress + WorldPointsOffset, ArenaAddress + WorldFreePointerOffset, ArenaAddress + BonePointsOffset, ArenaAddress + BoneFreePointerOffset, PointCount, 2, WorldTrailCallback, 3, BoneTrailCallback);
        public NativeBattleCpuTrailFrame Frame { get; }
        public Fixture()
        {
            NativeBattleEffectTaskPool[] pools =
            [
                NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1),
                NativeBattleEffectTaskPool.Initialize(MainDescriptor, ArenaAddress + MainOffset, MainStride, 1),
                NativeBattleEffectTaskPool.Initialize(WorldTrailDescriptor, ArenaAddress + WorldOffset, WorldStride, WorldCount),
                NativeBattleEffectTaskPool.Initialize(BoneTrailDescriptor, ArenaAddress + BoneOffset, BoneStride, BoneCount)
            ];
            NativeBattleOriginalEffectState effect = new(126, 126, new(126, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = pools.ToImmutableArray() };
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0, SecondPacketOffset + PacketBankBytes).Write(WorldFreePointerOffset, BitConverter.GetBytes(0u)).Write(BoneFreePointerOffset, BitConverter.GetBytes(0u));
            for (int index = 0; index < PointCount; index++)
            {
                arena = arena.Write(WorldPointsOffset + index * 40 + 6, BitConverter.GetBytes((short)-1)).Write(BonePointsOffset + index * 40, BitConverter.GetBytes((short)-1));
            }
            NativeBattleEffectMemory memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(NativeBattleEffectMemory.FromColdProcess().SeedSharedEffectRandom7059D0(1) with { Arena = arena }, effect.Pools);
            Frame = new(memory, effect);
        }
        public short Path(uint address)
        {
            int offset = checked((int)(address - BonePathsAddress));
            if (offset == 2)
            {
                return 8;
            }
            return offset % 4 == 0 ? (short)2 : (short)10;
        }
        public short Word(NativeBattleCpuTrailFrame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena!.Read(checked((int)(address - ArenaAddress)), 2));
        public uint ReadDword(NativeBattleCpuTrailFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena!.Read(checked((int)(address - ArenaAddress)), 4));
        public NativeBattleCpuTrailFrame Dword(NativeBattleCpuTrailFrame frame, uint address, uint value) => frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(checked((int)(address - ArenaAddress)), BitConverter.GetBytes(value)) } };
    }
}
