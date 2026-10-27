using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEnergyBomber217Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEnergyBomber217ControllerTests
{
    [Fact]
    public void Root_selects_second_bank_first_then_initializes_real_pools_and_captures_anchor_W()
    {
        Fixture fixture = new();
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root.WriteSlot(slot, root.Slots[slot].WriteBytes(12, new byte[8]));
        NativeBattleEnergyBomber217Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [root] } };
        frame = NativeBattleEnergyBomber217Controller.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(ArenaAddress + SecondPacketOffset, frame.State.ReadDword(CursorGlobal));
        frame = NativeBattleEnergyBomber217Controller.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(ArenaAddress + FirstPacketOffset, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(new ushort[] { 20, 0x684, 32 }, frame.Effect.Pools.Select(pool => pool.Stride).ToArray());
        Assert.Equal(64, frame.Effect.Pools[2].Slots.Length);
        Assert.Equal((ushort)777, frame.Effect.Pools[1].Slots[0].ReadWord(0x2E));
    }

    [Fact]
    public void Age46_reads_caster_bone39_and_caches_WORD_velocity_without_inventing_local_W()
    {
        Fixture fixture = new();
        NativeBattleEnergyBomber217Frame frame = fixture.At(46);
        frame = fixture.SetVector(frame, 0x10, new(1, 2, 3));
        (frame, int result) = fixture.Main(frame);
        Assert.Equal(0, result);
        Assert.Equal((4, (byte)39, (short)0), Assert.Single(fixture.Bones));
        Assert.Equal(new NativeBattleVector(99, -14, 997), fixture.Vector(frame, 0x18));
        Assert.Throws<NotSupportedException>(() => Assert.Single(fixture.Contexts).Read(6, 2));
        Assert.Equal(unchecked((uint)-128), U32(fixture.Contexts[0], 8));
        Assert.Equal(ArenaAddress + MorphOffset, U32(fixture.Contexts[0], 12));
    }

    [Theory]
    [InlineData(55)]
    [InlineData(59)]
    [InlineData(63)]
    public void Ease_transitions_publish_blend_frame_and_insert_new_trail_point_after_drawing(int age)
    {
        Fixture fixture = new();
        NativeBattleEnergyBomber217Frame frame = fixture.At(age);
        frame = fixture.SetVector(frame, 0x10, new(100, 200, 300));
        frame = fixture.SetVector(frame, 0x20, new(400, 500, 600));
        frame = fixture.SetVector(frame, 0x30, new(700, 800, 900));
        frame = fixture.SetWord(frame, 0x40, 1);
        (frame, _) = fixture.Main(frame);
        Assert.Equal(new NativeBattleVector(700, 800, 900), Assert.Single(fixture.Operations.OfType<NativeBattleEnergyBomber217DrawTrail120>()).Position);
        Assert.Equal(new NativeBattleVector(111, 222, 333), fixture.Vector(frame, 0x30));
        Assert.Equal(new NativeBattleVector(700, 800, 900), fixture.Vector(frame, 0x38));
        Assert.Equal((ushort)2, fixture.Task(frame).ReadWord(0x40));
        Assert.Equal((ushort)0, fixture.Task(frame).ReadWord(0x42));
        Assert.Throws<NotSupportedException>(() => fixture.Task(frame).ReadWord(0x36));
        Assert.Single(fixture.Blends);
        Assert.Equal(fixture.Frame.Memory.ScriptedColorRegisters! with { Material = 0x12345678 }, frame.Memory.ScriptedColorRegisters);
    }

    [Fact]
    public void Births_consume_three_CRT_values_per_successful_child_and_do_not_reuse_pool_tail()
    {
        Fixture fixture = new();
        NativeBattleEnergyBomber217Frame frame = fixture.At(64);
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 48; index++)
        {
            random = random.Draw(out _);
        }
        (frame, _) = fixture.Main(frame);
        Assert.Equal(random, frame.Random);
        Assert.Equal(16, frame.Effect.Pools[2].Slots.Count(task => (task.Flags & 1) != 0));
        Assert.All(frame.Effect.Pools[2].Slots.Where(task => (task.Flags & 1) != 0), task => Assert.Equal((ushort)0, task.ReadWord(18)));
        NativeBattleEffectTaskPool full = frame.Effect.Pools[2];
        while (full.Slots.Any(task => (task.Flags & 1) == 0))
        {
            (full, _) = full.Append(SpriteCallback);
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, full) } };
        NativeCrtRandomState before = frame.Random;
        (frame, _) = fixture.Main(fixture.SetWord(frame, 12, 65));
        Assert.Equal(before, frame.Random);
    }

    [Fact]
    public void Sprite_draw_precedes_acceleration_and_position_stores_and_twelfth_draw_retires()
    {
        Fixture fixture = new();
        NativeBattleEffectTaskPool sprites = fixture.Frame.Effect.Pools[2];
        (sprites, int slot) = sprites.Append(SpriteCallback);
        NativeBattleModulePoolTask task = sprites.Slots[slot].WriteBytes(12, new byte[20]).WriteWord(12, 100).WriteWord(18, 11).WriteWord(20, 20).WriteWord(26, unchecked((ushort)-2));
        sprites = sprites.WriteSlot(slot, task);
        NativeBattleEnergyBomber217Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = fixture.Frame.Effect.Pools.SetItem(2, sprites) } };
        (frame, int result) = NativeBattleEnergyBomber217Controller.AdvanceSprite(frame, sprites.BankAddress, fixture.Inputs);
        Assert.Equal((ushort)100, Assert.Single(fixture.SpriteTasks).ReadWord(12));
        Assert.Equal((ushort)11, fixture.SpriteTasks[0].ReadWord(18));
        Assert.Equal((ushort)118, frame.Effect.Pools[2].Slots[0].ReadWord(12));
        Assert.Equal((ushort)12, frame.Effect.Pools[2].Slots[0].ReadWord(18));
        Assert.Equal(2, result);
        Assert.Equal(fixture.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4), frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Theory]
    [InlineData(77, 0)]
    [InlineData(78, 1)]
    [InlineData(79, 0)]
    public void Contact_uses_only_old78_and_first_contact_of_selected_header(int age, int count)
    {
        Fixture fixture = new();
        fixture.Main(fixture.At(age));
        Assert.Equal(count, fixture.Operations.OfType<NativeBattleEnergyBomber217Contact>().Count());
        if (count != 0)
        {
            Assert.Equal(1, Assert.Single(fixture.Operations.OfType<NativeBattleEnergyBomber217Contact>()).Index);
        }
    }

    [Fact]
    public void Root_scans_sprites_but_retires_on_main_survivors_only()
    {
        Fixture fixture = new();
        NativeBattleEnergyBomber217Frame frame = fixture.At(86);
        NativeBattleEffectTaskPool sprites = frame.Effect.Pools[2];
        (sprites, int slot) = sprites.Append(SpriteCallback);
        sprites = sprites.WriteSlot(slot, sprites.Slots[slot].WriteBytes(12, new byte[20]));
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(2, sprites) } };
        frame = NativeBattleEnergyBomber217Controller.Advance(frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.Single(fixture.SpriteTasks);
        Assert.Equal((ushort)1, frame.Effect.Pools[2].Slots[0].ReadWord(18));
        Assert.Equal((ushort)87, frame.Effect.Pools[1].Slots[0].ReadWord(12));
    }

    [Fact]
    public void JSON_continuation_preserves_task_W_masks_and_single_random_stream()
    {
        Fixture fixture = new();
        NativeBattleEnergyBomber217Frame frame = fixture.At(47);
        frame = NativeBattleEnergyBomber217Controller.Write(frame, fixture.MainAddress, fixture.Task(frame).MarkPayloadUnknown(0x16, 2));
        NativeBattleEnergyBomber217Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEnergyBomber217Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(fixture.Main(frame).Frame)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(fixture.Main(restored).Frame)));
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private sealed class Fixture
    {
        public NativeBattleEnergyBomber217Frame Frame { get; }
        public uint MainAddress => ArenaAddress + MainOffset;
        public NativeBattleEnergyBomber217Resources Resources { get; }
        public NativeBattleEnergyBomber217Inputs Inputs { get; }
        public List<NativeBattleEnergyBomber217Operation> Operations { get; } = [];
        public List<NativeBattleEffectArena> Contexts { get; } = [];
        public List<(int Actor, byte Selector, short Distance)> Bones { get; } = [];
        public List<(NativeBattleVector First, NativeBattleVector Second, short A, short B)> Blends { get; } = [];
        public List<NativeBattleModulePoolTask> SpriteTasks { get; } = [];
        public Fixture()
        {
            byte[] first = new byte[0x4900];
            BitConverter.GetBytes(8).CopyTo(first, 4);
            BitConverter.GetBytes(100).CopyTo(first, 12);
            byte[] second = new byte[0x1BC0];
            BitConverter.GetBytes(8).CopyTo(second, 4);
            BitConverter.GetBytes(23).CopyTo(second, 12);
            Resources = new(new(MainActAddress, first.ToImmutableArray()), new(ImpactActAddress, second.ToImmutableArray()), new(0, TrailMeshAddress, new byte[0x270].ToImmutableArray()), new(0, SpriteAddress, 0, 0, new byte[0x16C].ToImmutableArray()), new("FF8_EN:11355D4", new byte[CameraLength].ToImmutableArray()), 0x642C8);
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
            (root, int rootSlot) = root.Append(RootCallback);
            root = root.WriteSlot(rootSlot, root.Slots[rootSlot].WriteBytes(12, new byte[8]).WriteWord(12, 2).WriteByte(15, 1));
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainAddress, MainStride, 1);
            (main, int mainSlot) = main.Append(MainCallback);
            main = main.WriteSlot(mainSlot, main.Slots[mainSlot].WriteBytes(12, new byte[MainStride - 12]).WriteWord(0x28, 1000).WriteWord(0x2A, 2000).WriteWord(0x2C, 3000).WriteWord(0x2E, 777));
            NativeBattleEffectTaskPool sprites = NativeBattleEffectTaskPool.Initialize(SpriteDescriptor, ArenaAddress, SpriteStride, SpriteCount);
            NativeBattleOriginalEffectState effect = new(217, 216, new(217, Entry, Initializer), new(4, 0, 1, 1, 0, 0), null) { Pools = [root, main, sprites], ReturnedDescriptor = RootDescriptor };
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            for (int index = 0; index < 2; index++)
            {
                (packets, _) = packets.Prepare(new(4, 8, 16, 0, 316, 217, null));
                (packets, _) = packets.AppendContact(new(new((byte)(index == 0 ? 0 : 3), 1, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
                (packets, _) = packets.Publish(1);
            }
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
            Frame = new(memory, effect, new(123), NativeBattleEnergyBomber217State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CasterGlobal, 0x1D97530).WriteDword(TargetGlobal, 0x1D97494)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Inputs = new(packets, (frame, _) => new(frame, new(1000, 2000, 3000, 777)), (frame, actor, selector, distance) =>
            {
                Bones.Add((actor, selector, distance));
                return new(frame, new(100, 500, 1000));
            }, (frame, _, _, _, _, context) =>
            {
                Contexts.Add(context);
                return new(frame, 1);
            }, (frame, a, b, firstWeight, secondWeight) =>
            {
                Blends.Add((a, b, firstWeight, secondWeight));
                return new(frame with { Memory = frame.Memory with { ScriptedColorRegisters = frame.Memory.ScriptedColorRegisters! with { Material = 0x12345678 } } }, new(111, 222, 333));
            }, (frame, operation) =>
            {
                Operations.Add(operation);
                if (operation is NativeBattleEnergyBomber217DrawSprite180 sprite)
                {
                    NativeBattleModulePoolTask task = NativeBattleEnergyBomber217Controller.Read(frame, sprite.TaskAddress);
                    SpriteTasks.Add(new(task.Flags, task.State, task.Next, task.Callback, task.ReadBytes(12, task.PayloadLength).ToImmutableArray()));
                }
                return frame;
            });
        }
        public NativeBattleEnergyBomber217Frame At(int age) => SetWord(Frame, 12, unchecked((ushort)age));
        public NativeBattleEnergyBomber217Frame SetWord(NativeBattleEnergyBomber217Frame frame, int offset, ushort value) => NativeBattleEnergyBomber217Controller.Write(frame, MainAddress, Task(frame).WriteWord(offset, value));
        public NativeBattleEnergyBomber217Frame SetVector(NativeBattleEnergyBomber217Frame frame, int offset, NativeBattleVector value) => NativeBattleEnergyBomber217Controller.Write(frame, MainAddress, Task(frame).WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z)));
        public NativeBattleModulePoolTask Task(NativeBattleEnergyBomber217Frame frame) => NativeBattleEnergyBomber217Controller.Read(frame, MainAddress);
        public NativeBattleVector Vector(NativeBattleEnergyBomber217Frame frame, int offset) => new(unchecked((short)Task(frame).ReadWord(offset)), unchecked((short)Task(frame).ReadWord(offset + 2)), unchecked((short)Task(frame).ReadWord(offset + 4)));
        public (NativeBattleEnergyBomber217Frame Frame, int Result) Main(NativeBattleEnergyBomber217Frame frame) => NativeBattleEnergyBomber217Controller.AdvanceMain(frame, MainAddress, Resources, Inputs);
    }
}
