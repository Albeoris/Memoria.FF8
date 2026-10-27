using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleNeedlesFamilyControllerTests
{
    [Theory]
    [InlineData((ushort)177)]
    [InlineData((ushort)178)]
    public void Entry_clears_only_record_heads_and_preserves_warm_suffixes_and_unknown_packet_bytes(ushort effect)
    {
        Fixture fixture = new(effect);
        NativeBattleNeedlesFamilyProfile p = fixture.Profile;
        NativeBattleNeedlesFamilyFrame frame = fixture.Frame with { State = NativeBattleNeedlesFamilyState.FromUnknown(effect).WriteDword(p.NeedleBank + 8, 0xDEADBEEF).WriteDword(p.SparkBank + 16, 0xAABBCCDD) };
        NativeBattleNeedlesFamilyFrame initialized = NativeBattleNeedlesFamilyController.Initialize(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(0u, initialized.State.ReadDword(p.NeedleBank));
        Assert.Equal(0u, initialized.State.ReadDword(p.NeedleBank + 299 * 56));
        Assert.Equal(0u, initialized.State.ReadDword(p.SparkBank + 299 * 24));
        Assert.Equal(0xDEADBEEFu, initialized.State.ReadDword(p.NeedleBank + 8));
        Assert.Equal(0xAABBCCDDu, initialized.State.ReadDword(p.SparkBank + 16));
        Assert.Throws<NotSupportedException>(() => initialized.State.Read(p.NeedleBank + 6, 2));
        Assert.Throws<NotSupportedException>(() => initialized.State.ReadPacket(p.PacketBase, 4));
        Assert.Equal(new[] { typeof(NativeBattleNeedlesFamilyRegisterCamera), typeof(NativeBattleNeedlesFamilyEnqueueTim) }, fixture.Operations.Select(operation => operation.GetType()).ToArray());
    }

    [Theory]
    [InlineData((ushort)177, 1)]
    [InlineData((ushort)178, 36)]
    public void Scheduler_birth_sound_and_Main_scan_are_same_pass_with_actual_temporary_lifetimes(ushort effect, ushort age)
    {
        Fixture fixture = new(effect, age);
        fixture.FillNeedles(2);
        NativeBattleNeedlesFamilyFrame next = fixture.Step(fixture.Frame);
        NativeBattleModulePoolTask main = Assert.Single(next.Effect.Pools[1].Slots, task => task.Callback == fixture.Profile.MainCallback);
        Assert.Equal((ushort)1, main.ReadWord(12));
        Assert.IsType<NativeBattleNeedlesFamilySound>(Assert.Single(fixture.Operations));
        Assert.Equal(2, fixture.BoneCounts.Count);
        Assert.Empty(fixture.Bones);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(next.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal(0, fixture.RandomDraws(next));
    }

    [Theory]
    [InlineData((ushort)177, 50, 55)]
    [InlineData((ushort)178, 85, 90)]
    public void Scheduler_contacts_on_old_age_and_retires_only_after_new_signed_threshold(ushort effect, ushort contactAge, ushort finalAge)
    {
        Fixture fixture = new(effect, contactAge);
        NativeBattleNeedlesFamilyFrame contacted = fixture.Step(fixture.Frame);
        Assert.Equal(0, Assert.Single(fixture.Operations.OfType<NativeBattleNeedlesFamilyContact>()).Index);
        Assert.Equal(fixture.Profile.RootDescriptor, contacted.Effect.ReturnedDescriptor);
        fixture.Operations.Clear();
        NativeBattleModulePoolTask scheduler = contacted.Effect.Pools[1].Slots[0].WriteWord(12, finalAge);
        contacted = contacted with { Effect = contacted.Effect with { Pools = contacted.Effect.Pools.SetItem(1, NativeBattleTaskFixture.EditPool(contacted.Effect.Pools[1], data => data with { Slots = contacted.Effect.Pools[1].Slots.SetItem(0, scheduler) })) } };
        NativeBattleNeedlesFamilyFrame done = fixture.Step(contacted);
        Assert.Equal(0u, done.Effect.ReturnedDescriptor);
        Assert.Empty(fixture.Operations);
    }

    [Theory]
    [InlineData((ushort)177)]
    [InlineData((ushort)178)]
    public void One_free_slot_consumes_ten_CRT_draws_and_preserves_matrix_padding_and_temp_W(ushort effect)
    {
        Fixture fixture = new(effect, main: true);
        fixture.FillNeedles(2);
        NativeBattleNeedlesFamilyState free = fixture.Frame.State.WriteDword(fixture.Profile.NeedleBank, 0);
        free = free with { Globals = free.Globals.MarkUnknown(checked((int)(fixture.Profile.NeedleBank + 0x2A - fixture.Profile.BaseAddress)), 2) };
        fixture.Frame = fixture.Frame with { State = free };
        NativeBattleNeedlesFamilyFrame next = fixture.Step(fixture.Frame);
        Assert.Equal(10, fixture.RandomDraws(next));
        Assert.Equal(2, fixture.Bones.Count);
        NativeCrtRandomState random = fixture.Frame.Random.Draw(out ushort lifetime);
        random = random.Draw(out ushort casterChoice);
        Assert.Equal(((byte)3, (byte)(casterChoice % 8), (short)(casterChoice & 4095)), fixture.Bones[0]);
        for (int index = 0; index < 3; index++)
        {
            random = random.Draw(out _);
        }
        random = random.Draw(out ushort targetChoice);
        Assert.Equal(((byte)0, (byte)(targetChoice % 8), (short)(targetChoice & 4095)), fixture.Bones[1]);
        Assert.Equal((short)(lifetime % 4 + 3), BinaryPrimitives.ReadInt16LittleEndian(next.State.Read(fixture.Profile.NeedleBank + 4, 2)));
        Assert.Throws<NotSupportedException>(() => next.State.Read(fixture.Profile.NeedleBank + 0x2A, 2));
        NativeBattleNeedlesFamilyDrawNeedle draw = Assert.Single(fixture.Operations.OfType<NativeBattleNeedlesFamilyDrawNeedle>());
        Assert.Equal(0, draw.Index);
        Assert.Equal(new[] { false, false }, fixture.SpawnWKnown);
        Assert.Equal(NativeBattleTemporaryMemory.Address + 220, fixture.NeedleCursor);
    }

    [Fact]
    public void Needle_death_creates_spark_before_same_callback_spark_draw_and_life_update()
    {
        Fixture fixture = new(177, age: 40, main: true);
        uint needle = fixture.Profile.NeedleBank;
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(needle, 1).Write(needle + 4, BitConverter.GetBytes((short)0)).WriteDword(needle + 0x2C, 100).WriteDword(needle + 0x30, unchecked((uint)-200)).WriteDword(needle + 0x34, 300) };
        NativeBattleNeedlesFamilyFrame next = fixture.Step(fixture.Frame);
        Assert.Equal(new[] { typeof(NativeBattleNeedlesFamilyDrawNeedle), typeof(NativeBattleNeedlesFamilyDrawSpark) }, fixture.Operations.Select(operation => operation.GetType()).ToArray());
        Assert.Equal(0u, next.State.ReadDword(needle));
        Assert.Equal((short)6, fixture.SparkDrawLife);
        Assert.Equal((short)5, BinaryPrimitives.ReadInt16LittleEndian(next.State.Read(fixture.Profile.SparkBank + 4, 2)));
        Assert.Equal(1u, next.State.ReadDword(fixture.Profile.SparkBank));
        Assert.Equal(new short[] { 100, -200, 300 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt16LittleEndian(next.State.Read(fixture.Profile.SparkBank + 8 + (uint)(axis * 2), 2))).ToArray());
        Assert.Equal(NativeBattleTemporaryMemory.Address + 220, fixture.SparkCursor);
    }

    [Fact]
    public void Pause_keeps_draws_and_actual_allocations_but_freezes_spawning_physics_age_and_RNG()
    {
        Fixture fixture = new(178, main: true) { Flags = 0x201 };
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(fixture.Profile.NeedleBank, 1).Write(fixture.Profile.NeedleBank + 4, BitConverter.GetBytes((short)2)).WriteDword(fixture.Profile.SparkBank, 1).Write(fixture.Profile.SparkBank + 4, BitConverter.GetBytes((short)6)) };
        NativeBattleNeedlesFamilyFrame next = fixture.Step(fixture.Frame);
        Assert.Equal(2, fixture.Operations.Count);
        Assert.Empty(fixture.BoneCounts);
        Assert.Equal(0, fixture.RandomDraws(next));
        Assert.Equal(fixture.Frame.State.Read(fixture.Profile.NeedleBank, 300 * 56), next.State.Read(fixture.Profile.NeedleBank, 300 * 56));
        Assert.Equal(fixture.Frame.State.Read(fixture.Profile.SparkBank, 300 * 24), next.State.Read(fixture.Profile.SparkBank, 300 * 24));
        Assert.Equal((ushort)0, next.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Equal(0, fixture.AxisX);
        Assert.Equal(0, fixture.AxisY);
        Assert.Equal(-4096, fixture.AxisZ);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(next.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void JSON_next_keeps_current_fixed_records_packet_owner_and_partial_temporary_bytes()
    {
        Fixture fixture = new(177, age: 40, main: true);
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(fixture.Profile.SparkBank, 1).Write(fixture.Profile.SparkBank + 4, BitConverter.GetBytes((short)2)) };
        NativeBattleNeedlesFamilyFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleNeedlesFamilyFrame>(NativeBattleTestSnapshots.Json(fixture.Frame))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(fixture.Step(fixture.Frame)), NativeBattleTestSnapshots.Json(fixture.Step(restored)));
    }

    private sealed class Fixture
    {
        public NativeBattleNeedlesFamilyProfile Profile { get; }
        public NativeBattleNeedlesFamilyResources Resources { get; }
        public NativeBattleNeedlesFamilyFrame Frame { get; set; }
        public NativeBattleNeedlesFamilyInputs Inputs { get; }
        public List<NativeBattleNeedlesFamilyOperation> Operations { get; } = [];
        public List<int> BoneCounts { get; } = [];
        public List<(byte Actor, byte Selector, short Distance)> Bones { get; } = [];
        public List<bool> SpawnWKnown { get; } = [];
        public uint Flags { get; set; }
        public uint NeedleCursor { get; private set; }
        public uint SparkCursor { get; private set; }
        public short SparkDrawLife { get; private set; }
        public int AxisX { get; private set; }
        public int AxisY { get; private set; }
        public int AxisZ { get; private set; }
        public Fixture(ushort effect, ushort age = 0, bool main = false)
        {
            Profile = NativeBattleNeedlesFamilyProfile.ForEffect(effect);
            Resources = CreateResources(Profile);
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(Profile.RootDescriptor, Profile.RootBank, 16, 1);
            (root, _) = root.Append(Profile.RootCallback);
            root = NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(0, root.Slots[0].WriteWord(12, 0)) });
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(Profile.ChildDescriptor, Profile.ChildBank, 36, 100);
            (children, _) = children.Append(main ? Profile.MainCallback : Profile.SchedulerCallback);
            children = NativeBattleTaskFixture.EditPool(children, data => data with { Slots = children.Slots.SetItem(0, children.Slots[0].WriteWord(12, age).WriteDword(0x10, 0).WriteDword(0x14, 0)) });
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(3, 8, 13, 0, effect == 177 ? (ushort)25 : (ushort)230, effect, null));
            (packets, _) = packets.AppendContact(new(new(0, 5, 1, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            NativeBattleOriginalEffectState selected = new(effect, effect, new(effect, Profile.Entry, Profile.Initializer), new(3, 0, 0, 0, 0, 0), null) { Pools = [root, children], ReturnedDescriptor = Profile.RootDescriptor, ResourcePointers = [new(Profile.InitializerGlobal, 0x800)] };
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), selected, new(123), NativeBattleNeedlesFamilyState.FromColdProcess(effect).WriteDword(Profile.CasterGlobal, 3).WriteDword(Profile.BaseAddress, 0)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Inputs = new(packets, actor =>
            {
                BoneCounts.Add(actor);
                return 8;
            }, (current, actor, selector, distance) =>
            {
                Bones.Add((actor, selector, distance));
                SpawnWKnown.Add(current.Staging!.ReadSnapshot(NativeBattleTemporaryMemory.Address + (actor == 3 ? 6u : 14u), 2).Known.All(value => value));
                return new(current, actor == 3 ? new(100, 200, 300) : new(-400, 500, -600));
            }, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null), _ => Flags, (current, operation) =>
            {
                Operations.Add(operation);
                if (operation is NativeBattleNeedlesFamilyDrawNeedle needle)
                {
                    NeedleCursor = BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
                    AxisX = BitConverter.ToInt32(current.Staging.Read(needle.Temporary220 + 0x20, 4));
                    AxisY = BitConverter.ToInt32(current.Staging.Read(needle.Temporary220 + 0x24, 4));
                    AxisZ = BitConverter.ToInt32(current.Staging.Read(needle.Temporary220 + 0x28, 4));
                }
                if (operation is NativeBattleNeedlesFamilyDrawSpark spark)
                {
                    SparkCursor = BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
                    SparkDrawLife = BinaryPrimitives.ReadInt16LittleEndian(current.State.Read(Profile.SparkBank + (uint)(spark.Index * 24) + 4, 2));
                }
                return current;
            });
        }
        public void FillNeedles(uint flag)
        {
            NativeBattleNeedlesFamilyState state = Frame.State;
            for (int index = 0; index < 300; index++)
            {
                state = state.WriteDword(Profile.NeedleBank + (uint)(index * 56), flag);
            }
            Frame = Frame with { State = state };
        }
        public NativeBattleNeedlesFamilyFrame Step(NativeBattleNeedlesFamilyFrame frame) => NativeBattleNeedlesFamilyController.Advance(frame, Resources, Inputs);
        public int RandomDraws(NativeBattleNeedlesFamilyFrame next)
        {
            NativeCrtRandomState random = Frame.Random;
            for (int index = 0; index <= 250; index++)
            {
                if (NativeBattleTestSnapshots.Json(random) == NativeBattleTestSnapshots.Json(next.Random))
                {
                    return index;
                }
                random = random.Draw(out _);
            }
            throw new InvalidDataException("Unexpected native CRT advancement.");
        }
        private static NativeBattleNeedlesFamilyResources CreateResources(NativeBattleNeedlesFamilyProfile profile)
        {
            byte[] camera = new byte[profile.CameraLength];
            BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), (ushort)camera.Length);
            byte[] sprite = new byte[40];
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(8), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(sprite.AsSpan(10), 16);
            return new(profile.Effect, new($"FF8_EN:{profile.CameraAddress:X}", camera.ToImmutableArray()), new(0, profile.NeedleVerticesAddress, 0, 0, new byte[32].ToImmutableArray()), new(0, profile.NeedleFacesAddress, 0, 0, new byte[80].ToImmutableArray()), new(0, profile.SparkSpriteAddress, 0, 0, sprite.ToImmutableArray()), [0, 0, -2, 0, -3, -8, -4], 0x641A9);
        }
    }
}
