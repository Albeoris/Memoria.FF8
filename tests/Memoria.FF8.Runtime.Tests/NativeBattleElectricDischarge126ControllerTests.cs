using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleElectricDischarge126Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleElectricDischarge126ControllerTests
{
    [Fact]
    public void Initialize_saves_three_physical_party_colors_and_seeds_only_shared_effect_random_zero()
    {
        Fixture f = new();
        NativeBattleElectricDischarge126Frame frame = NativeBattleElectricDischarge126Controller.Initialize(f.Frame, f.Resources, f.Inputs);
        Assert.Equal(f.Frame.Random, frame.Random);
        Assert.Equal(new NativeBattleSharedEffectRandomState(0), frame.Memory.SharedEffectRandom);
        Assert.Equal(new uint[] { 0x102030, 0x203040, 0x304050 }, Enumerable.Range(0, 3).Select(actor => frame.State.ReadDword(SavedColorsGlobal + (uint)(actor * 4))).ToArray());
        Assert.Equal((uint)Fixture.CasterPointer, frame.State.ReadDword(CasterGlobal));
        Assert.Single(frame.Effect.Pools);
        Assert.Equal(RootCallback, frame.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal(f.Resources.Camera, frame.Memory.Textures!.Animation!.Source);
    }

    [Theory]
    [InlineData(0u, SecondPacketOffset)]
    [InlineData(1u, FirstPacketOffset)]
    public void Root_clears_all_three_party_800_bits_and_publishes_stage_even_before_initialization(uint toggle, int offset)
    {
        Fixture f = new();
        NativeBattleElectricDischarge126Frame frame = f.Root(f.Frame, 0, false, toggle);
        (frame, int result) = NativeBattleElectricDischarge126Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(0, result);
        Assert.Equal(ArenaAddress + (uint)offset, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(new byte[] { 0, 1, 2 }, f.Operations.OfType<NativeBattleElectricDischarge126ActorFlags>().Select(value => value.Actor).ToArray());
        Assert.All(f.Operations.OfType<NativeBattleElectricDischarge126ActorFlags>(), value => Assert.Equal((ushort)0x800, value.ClearMask));
        Assert.Equal(new NativeBattleElectricDischarge126Stage(0, 0), Assert.Single(f.Operations.OfType<NativeBattleElectricDischarge126Stage>()));
        Assert.Equal((ushort)1, NativeBattleElectricDischarge126Controller.Read(frame, RootBank).ReadWord(12));
    }

    [Fact]
    public void Root_executes_actual_view_compose_restore_scope_even_with_all_empty_children()
    {
        Fixture f = new();
        NativeBattleElectricDischarge126Frame frame = f.Root(f.Frame, 90, true, 0);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, frame.Effect.Pools[1] with { Head = null, Tail = null, Slots = frame.Effect.Pools[1].Slots.Select(task => task with { Flags = 0 }).ToImmutableArray() }) } };
        (frame, int result) = NativeBattleElectricDischarge126Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        Assert.Equal(2, result);
        Assert.Equal((ushort)90, NativeBattleElectricDischarge126Controller.Read(frame, RootBank).ReadWord(12));
        Assert.Equal(new[] { nameof(NativeBattleElectricDischarge126LoadView), nameof(NativeBattleElectricDischarge126ComposeCaster), nameof(NativeBattleElectricDischarge126RestoreCaster), nameof(NativeBattleElectricDischarge126Stage) }, f.Operations.Skip(3).Select(value => value.GetType().Name).ToArray());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void First_eight_main_ages_create_three_actual_typed_bone_trails_with_nine_shared_draws(int age)
    {
        Fixture f = new();
        NativeBattleElectricDischarge126Frame frame = f.Age(f.Frame, age);
        NativeBattleSharedEffectRandomState expected = frame.Memory.SharedEffectRandom!;
        for (int index = 0; index < 9; index++)
        {
            expected = expected.Draw7059E0(out _);
        }
        (frame, int result) = NativeBattleElectricDischarge126Controller.AdvanceMain(frame, Fixture.MainAddress, f.Resources, f.Inputs);
        Assert.Equal(0, result);
        Assert.Equal(expected, frame.Memory.SharedEffectRandom);
        Assert.Equal(f.Frame.Random, frame.Random);
        NativeBattleModulePoolTask[] bones = frame.Effect.Pools[3].Slots.Where(task => (task.Flags & 1) != 0).ToArray();
        Assert.Equal(3, bones.Length);
        Assert.All(bones, task =>
        {
            Assert.Equal(new NativeBattleResourceRowReference(8, null), task.ReadResourceRowReference(36));
            Assert.Throws<NotSupportedException>(() => task.ReadDword(36));
        });
        Assert.Equal(age == 0 ? 1 : 0, f.Operations.OfType<NativeBattleElectricDischarge126SpatialSound>().Count());
    }

    [Fact]
    public void Main_old8_copies_masked_target_velocity_without_requiring_its_unwritten_W()
    {
        Fixture f = new();
        NativeBattleElectricDischarge126Frame frame = f.Age(f.Frame, 8);
        frame = frame with { State = frame.State.WriteView(TargetPointGlobal, NativeBattleEffectArena.Clear().MarkUnknown(6, 2), 8) };
        (frame, _) = NativeBattleElectricDischarge126Controller.AdvanceMain(frame, Fixture.MainAddress, f.Resources, f.Inputs);
        NativeBattleModulePoolTask world = Assert.Single(frame.Effect.Pools[2].Slots.Where(task => (task.Flags & 1) != 0));
        uint head = world.ReadDword(16);
        Assert.Equal(new byte[6], frame.Memory.Arena!.Read(checked((int)(head + 8 - ArenaAddress)), 6));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(checked((int)(head + 14 - ArenaAddress)), 2));
        Assert.Single(frame.Effect.Pools[3].Slots.Where(task => (task.Flags & 1) != 0));
    }

    [Fact]
    public void Main_party_cycle_ignores_captured_enemy_contacts_and_uses_five_selector_bytes()
    {
        Fixture f = new();
        NativeBattleElectricDischarge126Frame frame = f.Age(f.Frame, 20);
        NativeBattleSharedEffectRandomState expected = frame.Memory.SharedEffectRandom!;
        for (int index = 0; index < 4; index++)
        {
            expected = expected.Draw7059E0(out _);
        }
        expected.Draw7059E0(out ushort selected);
        (frame, _) = NativeBattleElectricDischarge126Controller.AdvanceMain(frame, Fixture.MainAddress, f.Resources, f.Inputs);
        Assert.Equal((Fixture.Caster, (short)f.Resources.ColorIndices[selected * 5 >> 15]), f.Bones[0]);
        Assert.Equal(1, f.Bones[1].Actor);
        Assert.Equal((ushort)2, f.Main(frame).ReadWord(16));
        Assert.Equal((short)1, unchecked((short)Assert.Single(frame.Effect.Pools[2].Slots.Where(task => (task.Flags & 1) != 0)).ReadWord(0x36)));
    }

    [Fact]
    public void Main_old52_emits_its_last_bone_before_bulk_contact_and_does_not_increment_on_retirement()
    {
        Fixture f = new();
        NativeBattleElectricDischarge126Frame frame = f.Age(f.Frame, 52);
        (frame, int result) = NativeBattleElectricDischarge126Controller.AdvanceMain(frame, Fixture.MainAddress, f.Resources, f.Inputs);
        Assert.Equal(2, result);
        Assert.Single(frame.Effect.Pools[3].Slots.Where(task => (task.Flags & 1) != 0));
        Assert.Equal(new NativeBattleElectricDischarge126ContactAll(0, 2), Assert.Single(f.Operations.OfType<NativeBattleElectricDischarge126ContactAll>()));
        Assert.Equal((ushort)52, f.Main(frame).ReadWord(12));
    }

    [Fact]
    public void Act_context_adds_decimal200_to_Y_and_preserves_actual_anchor_fourth_WORD_and_returned_frame()
    {
        Fixture f = new();
        (NativeBattleElectricDischarge126Frame frame, uint act) = NativeBattleElectricDischarge126Controller.Append(f.Frame, 5, ActCallback);
        (frame, int result) = NativeBattleElectricDischarge126Controller.AdvanceAct(frame, act, f.Resources, f.Inputs);
        Assert.Equal(2, result);
        NativeBattleEffectArena context = Assert.Single(f.Contexts);
        Assert.Equal((short)400, BinaryPrimitives.ReadInt16LittleEndian(context.Read(2, 2)));
        Assert.Equal((short)444, BinaryPrimitives.ReadInt16LittleEndian(context.Read(6, 2)));
        Assert.Equal(ArenaAddress + MorphOffset, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(8, 4)));
        Assert.Equal(123u, frame.State.ReadDword(FadeGlobal));
    }

    [Fact]
    public void Sprite_draws_phase7_with_actual_W_scale_before_signed_phase8_retirement()
    {
        Fixture f = new();
        (NativeBattleElectricDischarge126Frame frame, uint address) = NativeBattleElectricDischarge126Controller.Append(f.Frame, 4, SpriteCallback);
        NativeBattleModulePoolTask task = NativeBattleElectricDischarge126Controller.Read(frame, address).WriteWord(12, 7).WriteByte(14, 1).WriteByte(15, 255).WriteWord(0x16, 1234);
        frame = NativeBattleElectricDischarge126Controller.Write(frame, address, task);
        (frame, int result) = NativeBattleElectricDischarge126Controller.AdvanceSprite(frame, address, f.Resources, f.Inputs);
        Assert.Equal(2, result);
        Assert.Equal((ushort)8, NativeBattleElectricDischarge126Controller.Read(frame, address).ReadWord(12));
        NativeBattleElectricDischarge126DrawSprite180 op = Assert.Single(f.Operations.OfType<NativeBattleElectricDischarge126DrawSprite180>());
        Assert.Equal(SpriteBAddress, op.SourceAddress);
        Assert.Equal((ushort)7, BinaryPrimitives.ReadUInt16LittleEndian(frame.Staging!.Read(op.TemporaryAddress + 4, 2)));
        Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(frame.Staging!.Read(op.TemporaryAddress + 0x24, 2)));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Dedicated_world_branch_uses_positive_Z_distance_and_retains_returned_state_through_JSON()
    {
        Fixture f = new();
        (NativeBattleElectricDischarge126Frame frame, uint head) = NativeBattleElectricDischarge126Controller.StartWorld(f.Frame, new(0, 0, 0), new(100, -100, 300), new(100, -100, 300), new(0, 0, 0), -1, 0, 0, 100, 0, 3);
        uint taskAddress = frame.Effect.Pools[2].BankAddress;
        frame = JsonSerializer.Deserialize<NativeBattleElectricDischarge126Frame>(JsonSerializer.Serialize(frame))!;
        (frame, _) = NativeBattleElectricDischarge126Controller.AdvanceWorldTrail(frame, taskAddress, f.Resources, f.Inputs);
        NativeBattleModulePoolTask[] branches = frame.Effect.Pools[2].Slots.Where(task => (task.Flags & 1) != 0).Skip(1).ToArray();
        Assert.NotEmpty(branches);
        Assert.All(branches, task => Assert.InRange(unchecked((short)task.ReadWord(0x24)) - unchecked((short)task.ReadWord(0x1C)), 1024, 2047));
        Assert.Equal(456u, frame.State.ReadDword(FadeGlobal));
        Assert.Equal(f.Frame.Random, frame.Random);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Root_warm_reclaim_preserves_transferred_resource_row_metadata_and_unknown_numeric_bytes(bool cpuRow)
    {
        Fixture f = new();
        NativeBattleResourceRowReference row = cpuRow ? new(8, null) : new(null, 0x1939018);
        NativeBattleEffectTaskPool bone = f.Frame.Effect.Pools[3];
        int slot = bone.Slots.Length - 1;
        bone = bone with { Slots = bone.Slots.SetItem(slot, bone.Slots[slot].WriteResourceRowReference(36, row)) };
        NativeBattleOriginalEffectState effect = f.Frame.Effect with
        {
            ReturnedDescriptor = 0,
            Pools = f.Frame.Effect.Pools.SetItem(3, bone).Select(pool => pool with
            {
                Head = null,
                Tail = null,
                Slots = pool.Slots.Select(task => task with { Flags = 0 }).ToImmutableArray()
            }).ToImmutableArray()
        };
        NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(f.Frame.Memory, effect);
        NativeBattleElectricDischarge126Frame frame = f.Frame with { Memory = transfer.Memory, Effect = transfer.Effect with { Pools = [f.Frame.Effect.Pools[0]] } };
        frame = JsonSerializer.Deserialize<NativeBattleElectricDischarge126Frame>(JsonSerializer.Serialize(frame))!;
        frame = f.Root(frame, 1, false, 0);
        (frame, _) = NativeBattleElectricDischarge126Controller.AdvanceRoot(frame, RootBank, f.Resources, f.Inputs);
        NativeBattleModulePoolTask reclaimed = frame.Effect.Pools[3].Slots[slot];
        Assert.Equal(row, reclaimed.ReadResourceRowReference(36));
        Assert.Equal((ushort)0, reclaimed.Flags);
        if (cpuRow)
        {
            Assert.Throws<NotSupportedException>(() => reclaimed.ReadDword(36));
        }
        else
        {
            Assert.Equal(0x1939018u, reclaimed.ReadDword(36));
        }
    }

    private sealed class Fixture
    {
        public const int Caster = 3;
        public const uint CasterPointer = 0x1D972C0 + Caster * 156;
        public const uint MainAddress = ArenaAddress + MainOffset;
        public NativeBattleElectricDischarge126Frame Frame { get; }
        public NativeBattleElectricDischarge126Resources Resources { get; }
        public NativeBattleElectricDischarge126Inputs Inputs { get; }
        public List<NativeBattleElectricDischarge126Operation> Operations { get; } = [];
        public List<(int Actor, short Selector)> Bones { get; } = [];
        public List<NativeBattleEffectArena> Contexts { get; } = [];

        public Fixture()
        {
            byte[] camera = new byte[CameraLength];
            BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), CameraLength);
            byte[] act = new byte[0xA10];
            BinaryPrimitives.WriteInt32LittleEndian(act.AsSpan(4), 8);
            BinaryPrimitives.WriteInt32LittleEndian(act.AsSpan(12), 32);
            byte[] paths = new byte[0x780];
            BinaryPrimitives.WriteInt16LittleEndian(paths, 3);
            for (int path = 0; path < 3; path++)
            {
                int start = 8 + path * 66;
                BinaryPrimitives.WriteInt16LittleEndian(paths.AsSpan(2 + path * 2), (short)(start / 2));
                for (int index = 0; index < 16; index++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(paths.AsSpan(start + index * 4), (short)(path + 1));
                    BinaryPrimitives.WriteInt16LittleEndian(paths.AsSpan(start + index * 4 + 2), (short)index);
                }
                BinaryPrimitives.WriteInt16LittleEndian(paths.AsSpan(start + 64), -1);
            }
            Resources = new(new(ActAddress, act.ToImmutableArray()), Sprite(SpriteAAddress, 0x154), Sprite(SpriteBAddress, 0xDC), new("FF8_EN:12421E8", camera.ToImmutableArray()), new byte[16384].ToImmutableArray(), new short[4096].ToImmutableArray(), new byte[] { 10, 11, 12, 13, 14, 99 }.ToImmutableArray(), paths.ToImmutableArray(), 0x404040, 0x64111, 4096);
            NativeBattleEffectTaskPool[] pools =
            [
                NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1),
                NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainAddress, MainStride, 1),
                NativeBattleEffectTaskPool.Initialize(WorldTrailDescriptor, ArenaAddress + WorldOffset, WorldStride, WorldCount),
                NativeBattleEffectTaskPool.Initialize(BoneTrailDescriptor, ArenaAddress + BoneOffset, BoneStride, BoneCount),
                NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, ArenaAddress + SpriteOffset, SpriteStride, SpriteCount),
                NativeBattleEffectTaskPool.Initialize(ActDescriptor, ArenaAddress, ActStride, ActCount)
            ];
            (pools[0], _) = pools[0].Append(RootCallback);
            (pools[1], _) = pools[1].Append(MainCallback);
            NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
            for (int index = 0; index < PointCount; index++)
            {
                arena = arena.Write(WorldPointsOffset + index * PointStride + 6, BitConverter.GetBytes((short)-1)).Write(BonePointsOffset + index * PointStride, BitConverter.GetBytes((short)-1));
            }
            byte[] tim = new byte[546];
            BinaryPrimitives.WriteInt32LittleEndian(tim, 0x10);
            BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(4), 9);
            BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(8), 524);
            BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(16), 256);
            BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(18), 1);
            BinaryPrimitives.WriteInt32LittleEndian(tim.AsSpan(532), 14);
            BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(540), 1);
            BinaryPrimitives.WriteInt16LittleEndian(tim.AsSpan(542), 1);
            arena = arena.Write(0x30000, tim);
            NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().SeedSharedEffectRandom7059D0(0) with { Arena = arena, ModuleSpriteBillboard21DFED0 = NativeBattlePackedMatrix32.FromMatrix(identity, null) };
            NativeBattleOriginalEffectState effect = new(126, 125, new(126, Entry, Initializer), new(Caster, 0, 1, 0, 0, 0), null) { Pools = pools.ToImmutableArray(), ReturnedDescriptor = RootDescriptor, ResourcePointers = [new(InitializerGlobal, 0x30000)] };
            NativeBattleElectricDischarge126State state = NativeBattleElectricDischarge126State.FromColdProcess().WriteDword(CasterGlobal, CasterPointer).WriteDword(WorkingGlobal, ArenaAddress);
            Frame = new(memory, effect, new(543), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(Caster, 8, 15, 0, 28, 126, null));
            for (int index = 0; index < 2; index++)
            {
                (packets, _) = packets.AppendContact(new(new((byte)(index + 3), 1, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            }
            (packets, _) = packets.Publish(2);
            Inputs = new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, actor => new(actor, (ushort)(actor == 1 || actor == Caster ? 2 : 0)) { Color40 = (uint)(0x102030 + actor * 0x101010) }, _ => 30, _ => new(8, null), _ => NativeBattlePackedMatrix32.FromMatrix(identity with { Z = 2000 }, null), (frame, _) => new(frame, new(100, 200, 300, 444)), (frame, actor, selector, _) =>
            {
                Bones.Add((actor, selector));
                return new(frame, new(100, 200, 300));
            }, (frame, _, context) =>
            {
                Contexts.Add(context);
                return new(frame with { State = frame.State.WriteDword(FadeGlobal, 123) }, 2);
            }, (frame, operation) =>
            {
                Operations.Add(operation);
                return operation is NativeBattleElectricDischarge126ProjectWorld or NativeBattleElectricDischarge126DrawWorld ? frame with { State = frame.State.WriteDword(FadeGlobal, 456) } : frame;
            });
        }

        public NativeBattleModulePoolTask Main(NativeBattleElectricDischarge126Frame frame) => NativeBattleElectricDischarge126Controller.Read(frame, MainAddress);
        public NativeBattleElectricDischarge126Frame Age(NativeBattleElectricDischarge126Frame frame, int age) => NativeBattleElectricDischarge126Controller.Write(frame, MainAddress, Main(frame).WriteWord(12, unchecked((ushort)age)));
        public NativeBattleElectricDischarge126Frame Root(NativeBattleElectricDischarge126Frame frame, ushort age, bool initialized, uint toggle) => NativeBattleElectricDischarge126Controller.Write(frame, RootBank, NativeBattleElectricDischarge126Controller.Read(frame, RootBank).WriteWord(12, age).WriteByte(15, initialized ? (byte)1 : (byte)0).WriteDword(16, toggle));
        private static NativeBattleModuleResource Sprite(uint address, int length)
        {
            byte[] bytes = new byte[length];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 8);
            return new(0, address, 0, 0, bytes.ToImmutableArray());
        }
    }
}
