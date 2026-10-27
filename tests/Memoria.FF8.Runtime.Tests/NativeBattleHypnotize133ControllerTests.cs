using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleHypnotize133Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHypnotize133ControllerTests
{
    [Fact]
    public void Root_uses_actual_toggle_then_initializes_four_physical_banks_and_scans_births_same_callback()
    {
        Fixture fixture = new();
        NativeBattleHypnotize133Frame frame = fixture.Step(fixture.Frame);
        Assert.Equal(ArenaAddress + SecondPacketOffset, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(new ushort[] { 20, 196, 176, 84, 20 }, frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 1, 1, 8, 16, 32 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(new[] { NativeBattleHypnotize133ContextKind.Main, NativeBattleHypnotize133ContextKind.Anchored }, fixture.Contexts);
        Assert.Single(fixture.Operations.OfType<NativeBattleHypnotize133Sound>());
        Assert.Equal((ushort)1, frame.Effect.Pools[1].Slots[0].ReadWord(12));
        Assert.Equal((ushort)2, frame.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal(frame.Effect.Pools[2].Slots[0].ReadBytes(12, 1), frame.Memory.Arena!.Read(0x7C0 + 12, 1));
        NativeBattleHypnotize133Frame next = fixture.Step(frame);
        Assert.Equal(ArenaAddress + FirstPacketOffset, next.State.ReadDword(CursorGlobal));
    }

    [Fact]
    public void Root_before_age_one_only_flips_cursor_and_does_not_fabricate_children()
    {
        Fixture fixture = new();
        NativeBattleHypnotize133Frame frame = fixture.Frame;
        frame = NativeBattleHypnotize133Controller.Write(frame, RootBank, frame.Effect.Pools[0].Slots[0].WriteWord(12, 0));
        NativeBattleHypnotize133Frame next = fixture.Step(frame);
        Assert.Single(next.Effect.Pools);
        Assert.Empty(fixture.Contexts);
        Assert.Equal((ushort)1, next.Effect.Pools[0].Slots[0].ReadWord(12));
    }

    [Fact]
    public void Fade_birth_has_exact_five_CRT_order_and_selected_native_actor_bone_distance_factor()
    {
        Fixture fixture = new();
        NativeBattleHypnotize133Frame frame = Age(fixture.Step(fixture.Frame), 2);
        fixture.Clear();
        NativeCrtRandomState random = frame.Random;
        random = random.Draw(out ushort actor);
        random = random.Draw(out ushort distance);
        random = random.Draw(out ushort bone);
        random = random.Draw(out ushort jitter);
        random = random.Draw(out ushort factor);
        NativeBattleHypnotize133Frame next = fixture.Step(frame);
        Assert.Equal(random, next.Random);
        Assert.Equal((0, (byte)((bone * 7) >> 15), (short)(distance >> 3)), Assert.Single(fixture.Bones));
        NativeBattleModulePoolTask child = Assert.Single(next.Effect.Pools[3].Slots, task => (task.Flags & 1) != 0);
        Assert.Equal(unchecked((ushort)(100 + (jitter & 0x7FF) - 0x400)), child.ReadWord(12));
        Assert.Equal((ushort)((factor >> 4) + 0x800), child.ReadWord(18));
        Assert.Equal(fixture.Resources.FadingActs[0].Address, child.ReadDword(20));
        Assert.Contains(NativeBattleHypnotize133ContextKind.Fading, fixture.Contexts);
        Assert.NotNull(next.Memory.GeometryRegisters);
    }

    [Fact]
    public void Sprite_birth_uses_four_draws_and_reached_180_allocation_with_phase_zero_then_one()
    {
        Fixture fixture = new();
        NativeBattleHypnotize133Frame frame = Age(fixture.Step(fixture.Frame), 24);
        fixture.Clear();
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 4; index++)
        {
            random = random.Draw(out _);
        }
        NativeBattleHypnotize133Frame next = fixture.Step(frame);
        Assert.Equal(random, next.Random);
        NativeBattleHypnotize133DrawSprite180 draw = Assert.Single(fixture.Operations.OfType<NativeBattleHypnotize133DrawSprite180>());
        Assert.Equal(NativeBattleTemporaryMemory.Address, draw.Temporary180);
        Assert.Equal((ushort)0, Assert.Single(fixture.SpritePhases));
        Assert.Equal((ushort)1, NativeBattleHypnotize133Controller.Read(next, draw.TaskAddress).ReadWord(18));
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(next.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
        Assert.Equal((0, (byte)0xF0, (short)0), Assert.Single(fixture.Bones));
    }

    [Theory]
    [InlineData(33, 0)]
    [InlineData(34, 1)]
    [InlineData(35, 0)]
    public void Contact_is_bulk_only_at_old_34_with_actual_header_contact_count(int age, int expected)
    {
        Fixture fixture = new();
        NativeBattleHypnotize133Frame frame = Age(fixture.Step(fixture.Frame), age);
        fixture.Clear();
        NativeBattleHypnotize133Frame next = fixture.Step(frame);
        Assert.Equal(expected, fixture.Operations.OfType<NativeBattleHypnotize133BulkContact>().Count());
        if (expected != 0)
        {
            Assert.Equal(new NativeBattleHypnotize133BulkContact(0, 1), Assert.Single(fixture.Operations.OfType<NativeBattleHypnotize133BulkContact>()));
        }
        Assert.Equal((ushort)(age + 1), next.Effect.Pools[1].Slots[0].ReadWord(12));
    }

    [Fact]
    public void Full_fading_bank_skips_native_NULL_birth_without_consuming_CRT()
    {
        Fixture fixture = new();
        NativeBattleHypnotize133Frame frame = Age(fixture.Step(fixture.Frame), 2);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[3];
        for (int index = 0; index < pool.Slots.Length; index++)
        {
            (pool, int slot) = pool.Append(0x6B1900);
            pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteBytes(12, new byte[pool.Stride - 12]).WriteDword(20, fixture.Resources.FadingActs[0].Address)) });
        }
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(3, pool) } };
        fixture.Clear();
        Assert.Equal(frame.Random, fixture.Step(frame).Random);
        Assert.Empty(fixture.Bones);
    }

    [Fact]
    public void All_survivors_must_finish_and_terminal_root_does_not_increment_its_counter()
    {
        Fixture fixture = new();
        NativeBattleHypnotize133Frame frame = Age(fixture.Step(fixture.Frame), 41);
        fixture.Remaining = 0;
        ushort rootAge = frame.Effect.Pools[0].Slots[0].ReadWord(12);
        NativeBattleHypnotize133Frame next = fixture.Step(frame);
        Assert.Equal(0u, next.Effect.ReturnedDescriptor);
        Assert.Equal(rootAge, next.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.All(next.Effect.Pools, pool => Assert.Null(pool.Head));
    }

    [Fact]
    public void Independent_JSON_continuation_preserves_task_workspaces_RNG_and_next_service_order()
    {
        Fixture fixture = new();
        NativeBattleHypnotize133Frame frame = Age(fixture.Step(fixture.Frame), 18);
        NativeBattleHypnotize133Frame imported = NativeBattleTestSnapshots.RestoreJson<NativeBattleHypnotize133Frame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleHypnotize133Frame expected = fixture.Step(frame);
        NativeBattleHypnotize133Frame actual = fixture.Step(imported);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
    }

    private static NativeBattleHypnotize133Frame Age(NativeBattleHypnotize133Frame frame, int age) => NativeBattleHypnotize133Controller.Write(frame, frame.Effect.Pools[1].BankAddress, frame.Effect.Pools[1].Slots[0].WriteWord(12, unchecked((ushort)age)));

    private sealed class Fixture
    {
        public NativeBattleHypnotize133Frame Frame { get; }
        public NativeBattleHypnotize133Resources Resources { get; }
        public List<NativeBattleHypnotize133ContextKind> Contexts { get; } = [];
        public List<NativeBattleHypnotize133Operation> Operations { get; } = [];
        public List<(int Actor, byte Bone, short Distance)> Bones { get; } = [];
        public List<ushort> SpritePhases { get; } = [];
        public int Remaining { get; set; } = 1;
        private NativeBattleHypnotize133Inputs Inputs { get; }

        public Fixture()
        {
            byte[] camera = new byte[CameraLength];
            BitConverter.GetBytes((ushort)CameraLength).CopyTo(camera, 6);
            byte[] sprite = new byte[0x1A0];
            BitConverter.GetBytes((ushort)14).CopyTo(sprite, 8);
            for (int index = 0; index <= 14; index++)
            {
                BitConverter.GetBytes((ushort)0x40).CopyTo(sprite, 10 + index * 2);
            }
            Resources = new(new("FF8_EN:1212A30", camera.ToImmutableArray()), Source(0x1207958, 0x12D8), Enumerable.Range(0, 8).Select(index => Source(0x1208C30u + (uint)(index * 0xCB4), 0xCB4)).ToImmutableArray(), Enumerable.Range(0, 8).Select(index => Source(0x120F1D0u + (uint)(index * 0x70C), 0x70C)).ToImmutableArray(), Enumerable.Range(0, 8).Select(index => new NativeBattleModuleAnchor((short)index, 2, 3, (short)index)).ToImmutableArray(), new(0, 0x12077B8, 0, 0, sprite.ToImmutableArray()), 0x64286);
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(3, 8, 11, 0, 76, 133, null));
            (packets, _) = packets.AppendContact(new(new(0, 1, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
            (root, int slot) = root.Append(RootCallback);
            root = NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteBytes(12, new byte[8]).WriteWord(12, 1)) });
            NativeBattleOriginalEffectState effect = new(133, 132, new(133, Entry, Initializer), new(3, 0, 1, 0, 0, 0), null) { Pools = [root], ReturnedDescriptor = RootDescriptor };
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
            Frame = new(NativeBattleEffectTaskMemory.BindArenaBankOwnership(memory, effect.Pools), effect, new(123), NativeBattleHypnotize133State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CasterGlobal, 0x1D97494)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Inputs = new(packets, _ => 7, (current, actor, bone, distance) =>
            {
                Bones.Add((actor, bone, distance));
                return new(current, new(100, 200, 300));
            }, (current, _, kind) =>
            {
                Contexts.Add(kind);
                return new(current with { Memory = current.Memory with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() } }, NativeBattleEffectArena.Clear());
            }, (current, _, _, _, _, _) => new(current, Remaining), (current, operation) =>
            {
                Operations.Add(operation);
                if (operation is NativeBattleHypnotize133DrawSprite180 draw)
                {
                    SpritePhases.Add(NativeBattleHypnotize133Controller.Read(current, draw.TaskAddress).ReadWord(18));
                }
                return current;
            });
        }

        public NativeBattleHypnotize133Frame Step(NativeBattleHypnotize133Frame frame) => NativeBattleHypnotize133Controller.Advance(frame, Resources, Inputs);
        public void Clear()
        {
            Contexts.Clear();
            Operations.Clear();
            Bones.Clear();
            SpritePhases.Clear();
        }
        private static NativeBattleGfAnimationSource Source(uint address, int length)
        {
            byte[] bytes = new byte[length];
            BitConverter.GetBytes(8).CopyTo(bytes, 4);
            BitConverter.GetBytes(56).CopyTo(bytes, 12);
            return new(address, bytes.ToImmutableArray());
        }
    }
}
