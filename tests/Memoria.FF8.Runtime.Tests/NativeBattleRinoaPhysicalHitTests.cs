using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoaPhysicalHitTests
{
    [Fact]
    public void RootCapturesAliasesAndBone21AndConsumesFortyCrtDraws()
    {
        Fixture fixture = new();
        NativeBattleModuleState state = Task(0x56F9C0);
        state = Set(state, 16, 1234);
        NativeBattleModuleCallback next = NativeBattleRinoaPhysicalHit.AdvanceRoot(state, 0, fixture.Inputs());
        Assert.Equal((byte)2, next.State.RinoaPhysicalHit!.Actor);
        Assert.Equal((byte)5, next.State.RinoaPhysicalHit.Target);
        Assert.Equal(new NativeBattleVector(10, -30, 30), next.State.RinoaPhysicalHit.Origin);
        Assert.Equal(new[] { (2, (byte)21, (short)0) }, fixture.Bones);
        Assert.Equal(new[] { 5 }, fixture.Anchors);
        Assert.Single(next.State.Slots.Where(task => task.Callback == 0x56FE10));
        Assert.Equal(8, next.State.Slots.Count(task => task.Callback == 0x56FCD0));
        int linked = 0;
        for (int? current = next.State.Head; current is int index; current = next.State.Slots[index].Next)
        {
            linked++;
        }
        Assert.Equal(10, linked);
        NativeCrtRandomState random = new(1);
        for (int index = 0; index < 40; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, next.CrtRandomAfter);
        Assert.Equal(0x200, Word(next.State, 14, 1));
        Assert.Equal(3, Word(next.State, 26, 1));
    }

    [Fact]
    public void PausedRootLeavesStateAndServicesUntouched()
    {
        Fixture fixture = new();
        NativeBattleModuleState state = Task(0x56F9C0);
        NativeBattleModuleCallback next = NativeBattleRinoaPhysicalHit.AdvanceRoot(state, 0, fixture.Inputs(1));
        Assert.Same(state, next.State);
        Assert.Empty(fixture.Bones);
        Assert.Empty(fixture.Anchors);
        Assert.Empty(fixture.Sounds);
        Assert.Equal(0, fixture.Consumed);
        Assert.False(next.Release);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void RootAgeElevenSchedulesContactResourcesAndConsumesOnce(bool secondary, int count)
    {
        Fixture fixture = new() { Secondary = secondary };
        NativeBattleModuleState state = Set(Retained(Task(0x56F9C0)), 12, 11);
        NativeBattleModuleCallback next = NativeBattleRinoaPhysicalHit.AdvanceRoot(state, 0, fixture.Inputs());
        Assert.Equal(count, fixture.Sounds.Count);
        Assert.Equal(1, fixture.Consumed);
        Assert.Equal(new uint[] { 0x100014, 0x100015 }.Take(count), next.State.Slots.Skip(1).Take(count).Select(task => BinaryPrimitives.ReadUInt32LittleEndian(task.Payload.AsSpan().Slice(16))));
        Assert.All(fixture.Sounds, sound => Assert.Equal((byte)2, sound.Actor));
        Assert.Equal(Enumerable.Range(1, count), fixture.Sounds.Select(sound => sound.Number));
        Assert.Equal(12, Word(next.State, 12));
    }

    [Theory]
    [InlineData(33, false)]
    [InlineData(34, true)]
    public void RootCompletesOnIncrementedAgeThirtyFive(int age, bool release)
    {
        NativeBattleModuleCallback next = NativeBattleRinoaPhysicalHit.AdvanceRoot(Set(Retained(Task(0x56F9C0)), 12, age), 0, new Fixture().Inputs());
        Assert.Equal(release, next.Release);
        Assert.Equal(age + 1, Word(next.State, 12));
    }

    [Fact]
    public void SparkDrawsBeforeMovementAndUsesArithmeticShiftDamping()
    {
        Fixture fixture = new();
        NativeBattleModuleState state = Task(0x56FCD0);
        foreach ((int offset, int value) in new[] { (16, 100), (18, 200), (20, 300), (24, -9), (26, -1), (28, 9), (32, 2048) })
        {
            state = Set(state, offset, value);
        }
        NativeBattleModuleCallback next = NativeBattleRinoaPhysicalHit.AdvanceSpark(state, 0, fixture.Inputs());
        NativeBattleModuleDraw draw = Assert.Single(fixture.Sprites);
        Assert.Equal(new NativeBattleModuleAnchor(100, 200, 300, 0), draw.Anchor);
        Assert.Equal(2048, draw.Scale);
        Assert.Equal(-128, draw.DepthOffset);
        Assert.Equal(91, Word(next.State, 16));
        Assert.Equal(199, Word(next.State, 18));
        Assert.Equal(309, Word(next.State, 20));
        Assert.Equal(-7, Word(next.State, 24));
        Assert.Equal(0, Word(next.State, 26));
        Assert.Equal(8, Word(next.State, 28));
        Assert.Equal(1984, Word(next.State, 32));
    }

    [Fact]
    public void SparkDelayAndDrawPauseFollowIndependentGates()
    {
        Fixture fixture = new();
        NativeBattleModuleState delayed = Set(Task(0x56FCD0), 14, 1);
        Assert.Equal(1, Word(NativeBattleRinoaPhysicalHit.AdvanceSpark(delayed, 0, fixture.Inputs(1)).State, 14));
        NativeBattleModuleCallback ready = NativeBattleRinoaPhysicalHit.AdvanceSpark(delayed, 0, fixture.Inputs());
        Assert.Equal(0, Word(ready.State, 14));
        Assert.Empty(fixture.Sprites);
        NativeBattleModuleCallback paused = NativeBattleRinoaPhysicalHit.AdvanceSpark(ready.State, 0, fixture.Inputs(1));
        Assert.Single(fixture.Sprites);
        Assert.Same(ready.State, paused.State);
        NativeBattleModuleCallback terminal = NativeBattleRinoaPhysicalHit.AdvanceSpark(Set(ready.State, 12, 14), 0, fixture.Inputs());
        Assert.True(terminal.Release);
        Assert.Equal(15, Word(terminal.State, 12));
    }

    [Fact]
    public void ProjectileBuildsCurveAndPreservesIndependentNextCallback()
    {
        Fixture fixture = new();
        NativeBattleModuleState initial = Set(Retained(Task(0x56FE10)), 26, 3);
        NativeBattleModuleCallback first = Projectile(initial, fixture.Inputs());
        Assert.Equal(5, first.State.RinoaPhysicalHit!.Controls.Length);
        Assert.Equal(64, first.State.RinoaPhysicalHit.Curve.Length);
        Assert.Equal(new NativeBattleVector(0, 0, 0), first.State.RinoaPhysicalHit.Curve[0]);
        Assert.Equal(new NativeBattleVector(0, 0, 0), first.State.RinoaPhysicalHit.Curve[63]);
        Assert.Equal(3, Word(first.State, 24));
        NativeBattleModuleState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModuleState>(NativeBattleTestSnapshots.Json(first.State));
        Assert.Equal(NativeBattleTestSnapshots.Json(Projectile(first.State, fixture.Inputs())), NativeBattleTestSnapshots.Json(Projectile(restored, fixture.Inputs())));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ProjectileRefreshesOriginOnEveryAgeFifteenPassIncludingPause(uint flags)
    {
        Fixture fixture = new();
        NativeBattleModuleState prepared = Projectile(Set(Retained(Task(0x56FE10)), 26, 3), fixture.Inputs()).State;
        NativeBattleModuleState state = Set(prepared, 12, 15);
        NativeBattleModuleCallback next = Projectile(state, fixture.Inputs(flags));
        Assert.Equal(new NativeBattleVector(10, -60, 30), next.State.RinoaPhysicalHit!.Origin);
        Assert.Single(fixture.Bones);
        Assert.Equal(flags == 1 ? 15 : 16, Word(next.State, 12));
        if (flags == 1)
        {
            _ = Projectile(next.State, fixture.Inputs(flags));
            Assert.Equal(2, fixture.Bones.Count);
        }
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    public void ProjectileFallbackAdvancesOneAndCompletesAfterThirty(int age, bool release)
    {
        Fixture fixture = new();
        NativeBattleModuleState prepared = Projectile(Set(Retained(Task(0x56FE10)), 26, 3), fixture.Inputs()).State;
        NativeBattleModuleState state = Set(Set(prepared, 12, age), 24, 63);
        int drawnStart = -1;
        int drawnEnd = -1;
        NativeBattleModuleCallback next = NativeBattleRinoaPhysicalHit.AdvanceProjectile(state, 0, fixture.Inputs(), Identity, (current, _, start, end) =>
        {
            drawnStart = start;
            drawnEnd = end;
            return current;
        });
        Assert.Equal(63, drawnStart);
        Assert.Equal(64, drawnEnd);
        Assert.Equal(64, Word(next.State, 24));
        Assert.Equal(release, next.Release);
    }

    [Fact]
    public void PhysicalTargetAliasChangesSlotFlagsAndAllocatorSkipsOccupiedAlias()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear();
        for (int index = 0; index < 50; index++)
        {
            state = state.AppendCallback(0x56FCD0).State;
        }
        byte[] target = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(target, 5);
        state = state.WritePhysicalModuleBytes(0x209D780, target);
        Assert.Equal((ushort)5, state.Slots[50].Flags);
        Assert.Equal((byte)5, state.RinoaPhysicalHit!.Target);
        (NativeBattleModuleState allocated, int slot) = state.AppendCallback(0x56FCD0);
        Assert.Equal(51, slot);
        Assert.Equal((ushort)5, allocated.Slots[50].Flags);
    }

    [Fact]
    public void PhysicalGlobalsObserveBankWritesAndOriginPaddingSurvivesSnapshot()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear();
        byte[] aliases = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(aliases, 5);
        BinaryPrimitives.WriteUInt32LittleEndian(aliases.AsSpan(4), 2);
        state = state.WritePhysicalModuleBytes(0x209D780, aliases);
        byte[] origin = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(origin, -123);
        BinaryPrimitives.WriteInt16LittleEndian(origin.AsSpan(2), 456);
        BinaryPrimitives.WriteInt16LittleEndian(origin.AsSpan(4), -789);
        BinaryPrimitives.WriteUInt16LittleEndian(origin.AsSpan(6), 0xABCD);
        state = state.WritePhysicalModuleBytes(0x209D790, origin);
        Assert.Equal((byte)2, state.RinoaPhysicalHit!.Actor);
        Assert.Equal((byte)5, state.RinoaPhysicalHit.Target);
        Assert.Equal(new NativeBattleVector(-123, 456, -789), state.RinoaPhysicalHit.Origin);
        NativeBattleModuleState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModuleState>(NativeBattleTestSnapshots.Json(state));
        Assert.Equal(origin, restored.ReadPhysicalModuleBytes(0x209D790, 8).ToArray());
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void CurveSetterPreservesAllFourthWordsAcrossTaskHeadersAndSnapshot()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear();
        for (int index = 0; index < 69; index++)
        {
            uint address = index < 5 ? 0x209D798u + (uint)index * 8 + 6 : 0x209D7C0u + (uint)(index - 5) * 8 + 6;
            byte[] padding = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(padding, (ushort)(0xA000 + index));
            state = state.WritePhysicalModuleBytes(address, padding);
        }
        byte[] before = state.ReadPhysicalModuleBytes(0x209D798, 5 * 8 + 64 * 8).ToArray();
        NativeBattleRinoaPhysicalHitState globals = state.RinoaPhysicalHit!;
        state = state with
        {
            RinoaPhysicalHit = globals with
            {
                Controls = Enumerable.Range(0, 5).Select(index => new NativeBattleVector((short)index, (short)-index, (short)(index * 2))).ToImmutableArray(),
                Curve = Enumerable.Range(0, 64).Select(index => new NativeBattleVector((short)(index * 3), (short)-index, (short)(index * 5))).ToImmutableArray()
            }
        };
        byte[] after = state.ReadPhysicalModuleBytes(0x209D798, before.Length).ToArray();
        for (int row = 0; row < 69; row++)
        {
            Assert.Equal(before.AsSpan(row * 8 + 6, 2).ToArray(), after.AsSpan(row * 8 + 6, 2).ToArray());
        }
        Assert.Equal(new NativeBattleVector(189, -63, 315), state.RinoaPhysicalHit!.Curve[63]);
        NativeBattleModuleState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModuleState>(NativeBattleTestSnapshots.Json(state));
        Assert.Equal(after, restored.ReadPhysicalModuleBytes(0x209D798, after.Length).ToArray());
    }

    [Fact]
    public void SampleSixtyFourAliasesTheNextTaskHeaderWithoutWrapping()
    {
        NativeBattleModuleState state = NativeBattleModuleState.Clear();
        byte[] header = [0x34, 0x12, 0x78, 0x56, 0xBC, 0x9A, 0xF0, 0xDE];
        state = state.WritePhysicalModuleBytes(0x209D9C0, header);
        Assert.Equal((ushort)0x1234, state.Slots[66].Flags);
        Assert.Equal((ushort)0x5678, state.Slots[66].State);
        Assert.Equal(header, state.ReadPhysicalModuleBytes(0x209D7C0 + 64 * 8, 8).ToArray());
        NativeBattleModuleState restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModuleState>(NativeBattleTestSnapshots.Json(state));
        Assert.Equal(header, restored.ReadPhysicalModuleBytes(0x209D9C0, 8).ToArray());
    }
    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleModuleCallback Projectile(NativeBattleModuleState state, NativeBattleRinoaPhysicalHitInputs inputs) => NativeBattleRinoaPhysicalHit.AdvanceProjectile(state, 0, inputs, Identity, (current, _, _, _) => current);
    private static NativeBattleModuleState Task(uint callback) => NativeBattleModuleState.Clear().AppendCallback(callback).State;
    private static NativeBattleModuleState Retained(NativeBattleModuleState state) => state with { RinoaPhysicalHit = new(2, 5, new(1000, 0, 0, 4096), new(0, 0, 0)) };
    private static short Word(NativeBattleModuleState state, int offset, int slot = 0) => BinaryPrimitives.ReadInt16LittleEndian(state.Slots[slot].Payload.AsSpan().Slice(offset - 12));
    private static NativeBattleModuleState Set(NativeBattleModuleState state, int offset, int value)
    {
        byte[] payload = state.Slots[0].Payload.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(offset - 12), unchecked((short)value));
        return state with { Slots = state.Slots.SetItem(0, state.Slots[0] with { Payload = payload.ToImmutableArray() }) };
    }

    private sealed class Fixture
    {
        public List<(int Actor, byte Bone, short Fraction)> Bones { get; } = [];
        public List<int> Anchors { get; } = [];
        public List<(byte Actor, int Number)> Sounds { get; } = [];
        public List<NativeBattleModuleDraw> Sprites { get; } = [];
        public int Consumed { get; private set; }
        public bool Secondary { get; init; }
        public NativeBattleRinoaPhysicalHitInputs Inputs(uint flags = 0) => new(new(flags)
        {
            CrtRandom = new(1),
            Resources = [Sprite(), new(20, 0xC70020, 0x570CC0, 0x5707A0, [0, 0, 0, 1]), new(21, 0xC70024, 0x570CC0, 0x5707A0, [0, 0, 0, 1])],
            ReadModelAnchor = model =>
            {
                Anchors.Add(model);
                return new(1000, 0, 0, 4096);
            },
            ReadModelBone = (actor, bone, fraction) =>
            {
                Bones.Add((actor, bone, fraction));
                return new(10, 20, 30);
            }
        }, () => (2, 5), _ => 0x200, (actor, number, _) => Sounds.Add((actor, number)), () => Secondary, () => Consumed++, draw => Sprites.Add(draw));
        private static NativeBattleModuleResource Sprite()
        {
            byte[] bytes = new byte[48];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 16);
            for (int index = 0; index <= 16; index++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + index * 2), 44);
            }
            return new(6, 0xC70000, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
        }
    }
}
