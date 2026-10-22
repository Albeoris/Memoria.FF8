using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleElectrocute275Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleElectrocute275BoneTrailsTests
{
    [Theory]
    [InlineData(0u, 110)]
    [InlineData(1u, 130)]
    [InlineData(2u, 75)]
    [InlineData(3u, 90)]
    public void Directory_creation_scales_all_four_native_width_choices_and_consumes_three_shared_draws(uint seed, ushort width)
    {
        Fixture fixture = new(seed);
        var actual = NativeBattleElectrocute275Controller.CreateBoneTrail(fixture.Frame, 0x12345678, BonePathsAddress, 0, 3, 2048, fixture.Resources);
        Assert.NotEqual(0u, actual.Address);
        NativeBattleModulePoolTask task = NativeBattleElectrocute275Controller.Read(actual.Frame, actual.Address);
        Assert.Equal(0x689280u, task.Callback);
        Assert.Equal((ushort)3, task.ReadWord(12));
        Assert.Equal((ushort)1, task.ReadWord(14));
        Assert.Equal(width, task.ReadWord(16));
        Assert.Equal(BonePathsAddress + 20, task.ReadDword(24));
        Assert.Equal(0x12345678u, task.ReadDword(36));
        Assert.Equal(fixture.Point(0), task.ReadDword(28));
        Assert.Equal(task.ReadDword(28), task.ReadDword(32));
        Assert.Equal((short)10, ReadWord(actual.Frame, fixture.Point(0)));
        Assert.Equal((byte)2, actual.Frame.Memory.Arena!.Read(fixture.At(0) + 15, 1)[0]);
        Assert.Equal(Advance(new(seed), 3), actual.Frame.Memory.SharedEffectRandom);
        Assert.Equal(fixture.Frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Allocation_prefers_actual_cached_pointer_preserves_unwritten_bytes_and_uses_no_random_without_jitter()
    {
        Fixture fixture = new();
        NativeBattleElectrocute275Frame frame = fixture.WriteDword(fixture.Frame, ArenaAddress + BoneFreePointerOffset, fixture.Point(5));
        var actual = NativeBattleElectrocute275Controller.AllocateBonePoint(frame, 7, 123, 96, false, 1024, -128);
        Assert.Equal(fixture.Point(5), actual.Address);
        Assert.Equal(0u, ReadDword(actual.Frame, ArenaAddress + BoneFreePointerOffset));
        Assert.Equal((short)123, ReadWord(actual.Frame, actual.Address));
        Assert.Equal((short)96, ReadWord(actual.Frame, actual.Address + 2));
        Assert.Equal(new byte[6], actual.Frame.Memory.Arena!.Read(fixture.At(5) + 4, 6));
        Assert.Equal(new byte[] { 0, 0, 0, 7 }, actual.Frame.Memory.Arena!.Read(fixture.At(5) + 12, 4));
        Assert.Equal((short)1024, ReadWord(actual.Frame, actual.Address + 32));
        Assert.Equal((short)-128, ReadWord(actual.Frame, actual.Address + 34));
        Assert.Equal(0u, ReadDword(actual.Frame, actual.Address + 36));
        Assert.Throws<NotSupportedException>(() => actual.Frame.Memory.Arena!.Read(fixture.At(5) + 10, 2));
        Assert.Throws<NotSupportedException>(() => actual.Frame.Memory.Arena!.Read(fixture.At(5) + 16, 16));
        Assert.Equal(frame.Memory.SharedEffectRandom, actual.Frame.Memory.SharedEffectRandom);
    }

    [Fact]
    public void Jitter_uses_exact_six_shared_draws_in_X_Y_Z_order()
    {
        Fixture fixture = new(123);
        var actual = NativeBattleElectrocute275Controller.AllocateBonePoint(fixture.Frame, 1, 2, 3, true, 1000, -100);
        NativeBattleSharedEffectRandomState expected = new(123);
        byte[] velocities = new byte[3];
        for (int axis = 0; axis < 3; axis++)
        {
            expected = expected.Draw7059E0(out ushort first);
            expected = expected.Draw7059E0(out ushort second);
            velocities[axis] = unchecked((byte)(((first + second) >> 10) - 32));
        }
        Assert.Equal(velocities, actual.Frame.Memory.Arena!.Read(fixture.At(0) + 12, 3));
        Assert.Equal(expected, actual.Frame.Memory.SharedEffectRandom);
        Assert.Equal(fixture.Frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Full_point_bank_has_no_jitter_draws_and_full_task_pool_releases_only_initial_point()
    {
        Fixture fixture = new();
        NativeBattleElectrocute275Frame frame = fixture.Frame;
        for (int index = 0; index < PointCount; index++)
        {
            frame = fixture.WriteWord(frame, fixture.Point(index), 0);
        }
        var full = NativeBattleElectrocute275Controller.AllocateBonePoint(frame, 1, 2, 3, true, 1024, -128);
        Assert.Equal(0u, full.Address);
        Assert.Equal(frame.Memory.SharedEffectRandom, full.Frame.Memory.SharedEffectRandom);
        frame = fixture.Frame;
        for (int index = 0; index < 64; index++)
        {
            (frame, _) = NativeBattleElectrocute275Controller.Append(frame, 3, 1);
        }
        var rejected = NativeBattleElectrocute275Controller.CreateBoneTrail(frame, 123, BonePathsAddress, 0, 3, 4096, fixture.Resources);
        Assert.Equal(0u, rejected.Address);
        Assert.Equal((short)-1, ReadWord(rejected.Frame, fixture.Point(0)));
        Assert.Equal(fixture.Point(0), ReadDword(rejected.Frame, ArenaAddress + BoneFreePointerOffset));
        Assert.Equal(Advance(new(1), 3), rejected.Frame.Memory.SharedEffectRandom);
    }

    [Fact]
    public void Advance_uses_fixed_steps_without_age_increment_and_draws_returned_chain_after_motion()
    {
        Fixture fixture = new();
        var created = NativeBattleElectrocute275Controller.CreateBoneTrail(fixture.Frame, 0x1234, BonePathsAddress, 0, 3, 4096, fixture.Resources);
        var actual = NativeBattleElectrocute275Controller.AdvanceBoneTrail(created.Frame, created.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, actual.Result);
        NativeBattleModulePoolTask task = NativeBattleElectrocute275Controller.Read(actual.Frame, created.Address);
        Assert.Equal((ushort)3, task.ReadWord(12));
        Assert.Equal((ushort)4, task.ReadWord(14));
        NativeBattleElectrocute275DrawBoneTrail draw = Assert.Single(fixture.Draws);
        Assert.Equal(0x1234u, draw.BindingAddress);
        Assert.Equal(fixture.Point(0), draw.HeadAddress);
        Assert.Equal((short)4, draw.Count);
        Assert.Equal(Advance(new(1), 24), actual.Frame.Memory.SharedEffectRandom);
        Assert.Equal(created.Frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Early_sentinel_releases_entire_chain_without_extra_size_draw_or_drawing()
    {
        Fixture fixture = new();
        fixture.PathWord(24, short.MaxValue);
        var created = NativeBattleElectrocute275Controller.CreateBoneTrail(fixture.Frame, 1, BonePathsAddress, 0, 1, 4096, fixture.Resources);
        var actual = NativeBattleElectrocute275Controller.AdvanceBoneTrail(created.Frame, created.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(2, actual.Result);
        Assert.Empty(fixture.Draws);
        Assert.Equal((short)-1, ReadWord(actual.Frame, fixture.Point(0)));
        Assert.Equal(fixture.Point(0), ReadDword(actual.Frame, ArenaAddress + BoneFreePointerOffset));
        Assert.Equal(created.Frame.Memory.SharedEffectRandom, actual.Frame.Memory.SharedEffectRandom);
        Assert.Equal((ushort)1, NativeBattleElectrocute275Controller.Read(actual.Frame, created.Address).ReadWord(14));
    }

    [Fact]
    public void Negative_path_branch_appends_real_child_and_preserves_latest_parent_Next()
    {
        Fixture fixture = new();
        fixture.PathWord(20, -5);
        fixture.PathWord(22, 5);
        fixture.PathWord(24, 60);
        fixture.PathWord(26, 6);
        fixture.PathWord(28, 61);
        fixture.PathWord(30, 3);
        fixture.PathWord(32, 20);
        fixture.PathWord(34, 4);
        fixture.PathWord(36, 30);
        var created = NativeBattleElectrocute275Controller.CreateBoneTrail(fixture.Frame, 0x1234, BonePathsAddress, 0, 1, 4096, fixture.Resources);
        var actual = NativeBattleElectrocute275Controller.AdvanceBoneTrail(created.Frame, created.Address, fixture.Resources, fixture.Inputs());
        NativeBattleModulePoolTask parent = NativeBattleElectrocute275Controller.Read(actual.Frame, created.Address);
        Assert.Equal((int?)1, parent.Next);
        NativeBattleModulePoolTask child = actual.Frame.Effect.Pools[3].Slots[1];
        Assert.Equal(0x689280u, child.Callback);
        Assert.Equal(BonePathsAddress + 26, child.ReadDword(24));
        Assert.Equal((short)60, ReadWord(actual.Frame, child.ReadDword(28)));
        Assert.Equal((byte)5, actual.Frame.Memory.Arena!.Read(checked((int)(child.ReadDword(28) - ArenaAddress)) + 15, 1)[0]);
        Assert.Equal(Advance(new(1), 12), actual.Frame.Memory.SharedEffectRandom);
    }

    [Fact]
    public void Trim_occurs_after_frame_returning_draw_frees_only_head_and_preserves_new_link()
    {
        Fixture fixture = new();
        var created = NativeBattleElectrocute275Controller.CreateBoneTrail(fixture.Frame, 0x1234, BonePathsAddress, 0, 1, 4096, fixture.Resources);
        NativeBattleElectrocute275Frame frame = created.Frame;
        for (int index = 1; index < 3; index++)
        {
            (frame, _) = NativeBattleElectrocute275Controller.AllocateBonePoint(frame, 0, (short)index, 100, false, 500, -8);
        }
        frame = fixture.WriteDword(frame, fixture.Point(0) + 36, fixture.Point(1));
        frame = fixture.WriteDword(frame, fixture.Point(1) + 36, fixture.Point(2));
        NativeBattleModulePoolTask task = NativeBattleElectrocute275Controller.Read(frame, created.Address).WriteWord(14, 3).WriteDword(24, 0).WriteDword(32, fixture.Point(2));
        frame = NativeBattleElectrocute275Controller.Write(frame, created.Address, task);
        fixture.AfterDraw = current =>
        {
            (current, _) = NativeBattleElectrocute275Controller.Append(current, 3, 1);
            return fixture.WriteWord(fixture.WriteWord(current, fixture.Point(0) + 32, 0), fixture.Point(1) + 32, 0);
        };
        var actual = NativeBattleElectrocute275Controller.AdvanceBoneTrail(frame, created.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(0, actual.Result);
        task = NativeBattleElectrocute275Controller.Read(actual.Frame, created.Address);
        Assert.Equal((int?)1, task.Next);
        Assert.Equal((ushort)2, task.ReadWord(14));
        Assert.Equal(fixture.Point(1), task.ReadDword(28));
        Assert.Equal((short)-1, ReadWord(actual.Frame, fixture.Point(0)));
        Assert.Equal((short)1, ReadWord(actual.Frame, fixture.Point(1)));
        Assert.Equal(fixture.Point(0), ReadDword(actual.Frame, ArenaAddress + BoneFreePointerOffset));
        Assert.Equal((short)3, Assert.Single(fixture.Draws).Count);
    }

    [Fact]
    public void Restored_live_chain_repeats_next_callback_and_missing_native_source_fails()
    {
        Fixture fixture = new();
        var created = NativeBattleElectrocute275Controller.CreateBoneTrail(fixture.Frame, 0x1234, BonePathsAddress, 0, 1, 4096, fixture.Resources);
        NativeBattleElectrocute275Frame restored = JsonSerializer.Deserialize<NativeBattleElectrocute275Frame>(JsonSerializer.Serialize(created.Frame))!;
        var expected = NativeBattleElectrocute275Controller.AdvanceBoneTrail(created.Frame, created.Address, fixture.Resources, fixture.Inputs());
        var actual = NativeBattleElectrocute275Controller.AdvanceBoneTrail(restored, created.Address, fixture.Resources, fixture.Inputs());
        Assert.Equal(expected.Result, actual.Result);
        Assert.Equal(JsonSerializer.Serialize(expected.Frame), JsonSerializer.Serialize(actual.Frame));
        NativeBattleElectrocute275Resources missing = fixture.Resources with { BonePaths = [] };
        Assert.Throws<InvalidDataException>(() => NativeBattleElectrocute275Controller.CreateBoneTrail(fixture.Frame, 1, BonePathsAddress, 0, 1, 4096, missing));
    }

    private static short ReadWord(NativeBattleElectrocute275Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena!.Read(checked((int)(address - ArenaAddress)), 2));
    private static uint ReadDword(NativeBattleElectrocute275Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena!.Read(checked((int)(address - ArenaAddress)), 4));
    private static NativeBattleSharedEffectRandomState Advance(NativeBattleSharedEffectRandomState state, int count)
    {
        for (int index = 0; index < count; index++)
        {
            state = state.Draw7059E0(out _);
        }
        return state;
    }

    private sealed class Fixture
    {
        private readonly byte[] paths = new byte[0x6A8];
        public NativeBattleElectrocute275Frame Frame { get; }
        public List<NativeBattleElectrocute275DrawBoneTrail> Draws { get; } = [];
        public Func<NativeBattleElectrocute275Frame, NativeBattleElectrocute275Frame>? AfterDraw { get; set; }
        public NativeBattleElectrocute275Resources Resources => new(new("unused", []), new(0, []), new(0, 0, 0, 0, []), new(0, 0, 0, 0, []), new("unused", []), [], [], [], paths.ToImmutableArray(), 0, 0, 0);
        public uint Point(int index) => ArenaAddress + (uint)(BonePointsOffset + index * PointStride);
        public int At(int index) => BonePointsOffset + index * PointStride;
        public void PathWord(int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(paths.AsSpan(offset), value);

        public Fixture(uint seed = 1)
        {
            NativeBattleEffectTaskPool[] pools =
            [
                NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1),
                NativeBattleEffectTaskPool.Initialize(MainDescriptor, ArenaAddress + 0x22A0, 52, 1),
                NativeBattleEffectTaskPool.Initialize(WorldTrailDescriptor, ArenaAddress + 0x13A0, 60, 64),
                NativeBattleEffectTaskPool.Initialize(BoneTrailDescriptor, ArenaAddress + 0x9A0, 40, 64),
                NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, ArenaAddress + 0x6A0, 24, 32),
                NativeBattleEffectTaskPool.Initialize(ActDescriptor, ArenaAddress, 212, 8)
            ];
            NativeBattleOriginalEffectState effect = new(275, 275, new(275, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = pools.ToImmutableArray() };
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0, 0x2C474).Write(BoneFreePointerOffset, BitConverter.GetBytes(0u));
            for (int index = 0; index < PointCount; index++)
            {
                arena = arena.Write(At(index), BitConverter.GetBytes((short)-1));
            }
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().SeedSharedEffectRandom7059D0(seed) with { Arena = arena };
            Frame = new(memory, effect, new(123), NativeBattleElectrocute275State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress));
            PathWord(0, 1);
            PathWord(2, 8);
            for (int index = 0; index < 8; index++)
            {
                PathWord(16 + index * 4, (short)(index + 2));
                PathWord(18 + index * 4, (short)((index + 1) * 10));
            }
            PathWord(48, short.MaxValue);
        }

        public NativeBattleElectrocute275Frame WriteWord(NativeBattleElectrocute275Frame frame, uint address, short value) => frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(checked((int)(address - ArenaAddress)), BitConverter.GetBytes(value)) } };
        public NativeBattleElectrocute275Frame WriteDword(NativeBattleElectrocute275Frame frame, uint address, uint value) => frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(checked((int)(address - ArenaAddress)), BitConverter.GetBytes(value)) } };
        public NativeBattleElectrocute275Inputs Inputs() => new(NativeBattleActionPacketsState.Fresh(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (frame, operation) =>
        {
            Draws.Add(Assert.IsType<NativeBattleElectrocute275DrawBoneTrail>(operation));
            return AfterDraw?.Invoke(frame) ?? frame;
        });
    }
}
