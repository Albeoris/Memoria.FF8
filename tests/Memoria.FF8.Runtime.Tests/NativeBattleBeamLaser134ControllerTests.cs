using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBeamLaser134Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBeamLaser134ControllerTests
{
    [Fact]
    public void Entry_saves_actual_word_flags_and_sets_only_bit_eight_with_current_arena_initializer()
    {
        Fixture fixture = new();
        fixture.Flags[3] = 0xA501;
        NativeBattleBeamLaser134Frame next = NativeBattleBeamLaser134Controller.Initialize(fixture.Frame, fixture.Resources, fixture.Inputs);
        Assert.Equal(0xA501u, next.State.ReadDword(SavedCasterFlagsGlobal));
        Assert.Equal((ushort)0xA509, fixture.Flags[3]);
        Assert.Equal(ArenaAddress, next.State.ReadDword(WorkingGlobal));
        Assert.Single(next.Effect.Pools);
        Assert.Equal(new byte[8], next.Effect.Pools[0].Slots[0].ReadBytes(12, 8));
        Assert.Same(fixture.Frame.Memory.Arena, next.Memory.Arena);
    }

    [Fact]
    public void Startup_uses_toggle_and_actual_five_workspaces_without_moving_bank_ownership()
    {
        Fixture fixture = new();
        NativeBattleBeamLaser134Frame next = fixture.Step(fixture.Frame);
        Assert.Equal(ArenaAddress + SecondPacketOffset, next.State.ReadDword(CursorGlobal));
        Assert.Equal(new ushort[] { 20, 0x540, 20, 28, 132 }, next.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 1, 1, 16, 16, 8 }, next.Effect.Pools.Select(pool => pool.Slots.Length));
        NativeBattleModulePoolTask main = next.Effect.Pools[1].Slots[0];
        foreach ((int offset, uint source) in new[] { (0x1C, 0x12038DCu), (0x74, 0x12038DCu), (0xCC, 0x1205270u), (0x2EC, 0x12065F0u), (0x390, 0x1204004u) })
        {
            Assert.Equal(source, main.ReadDword(offset));
        }
        Assert.Equal((ushort)0, main.ReadWord(26));
        Assert.Equal((byte)1, next.Effect.Pools[0].Slots[0].ReadByte(14));
        Assert.Equal(new[] { 0x1C, 0x74 }, fixture.Acts.Select(call => call.Offset));
        Assert.Equal(main.ReadDword(12), BitConverter.ToUInt32(next.Memory.Arena!.Read(0x720 + 12, 4)));
        Assert.Equal(ArenaAddress + FirstPacketOffset, fixture.Step(next).State.ReadDword(CursorGlobal));
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)8)]
    public void Terminal_restores_only_saved_bit_eight_preserving_every_other_current_flag(ushort saved)
    {
        Fixture fixture = new();
        NativeBattleBeamLaser134Frame frame = Age(fixture.Step(fixture.Frame), 46);
        frame = frame with { State = frame.State.WriteDword(SavedCasterFlagsGlobal, saved) };
        fixture.Flags[3] = 0xE7FF;
        NativeBattleBeamLaser134Frame next = fixture.Step(frame);
        Assert.Equal((ushort)(0xE7F7 | saved), fixture.Flags[3]);
        Assert.Equal(0u, next.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)46, next.Effect.Pools[1].Slots[0].ReadWord(12));
    }

    [Fact]
    public void Sound_old_two_and_first_contact_old_twenty_six_are_distinct_from_glyph_inference()
    {
        Fixture fixture = new();
        NativeBattleBeamLaser134Frame frame = fixture.Step(fixture.Frame);
        fixture.Clear();
        fixture.Step(Age(frame, 2));
        Assert.Single(fixture.Operations.OfType<NativeBattleBeamLaser134Sound>());
        Assert.Empty(fixture.Operations.OfType<NativeBattleBeamLaser134Contact>());
        fixture.Clear();
        NativeBattleBeamLaser134Frame next = fixture.Step(Age(frame, 26));
        Assert.Equal(new NativeBattleBeamLaser134Contact(0), Assert.Single(fixture.Operations.OfType<NativeBattleBeamLaser134Contact>()));
        Assert.Empty(fixture.Operations.OfType<NativeBattleBeamLaser134Sound>());
        Assert.Equal((ushort)27, next.Effect.Pools[1].Slots[0].ReadWord(12));
    }

    [Fact]
    public void Four_moving_births_consume_twelve_CRT_draws_and_draw_before_signed_word_velocity()
    {
        Fixture fixture = new();
        NativeBattleBeamLaser134Frame frame = Age(fixture.Step(fixture.Frame), 22);
        fixture.Clear();
        NativeCrtRandomState expected = frame.Random;
        ushort[] rolls = new ushort[12];
        for (int index = 0; index < rolls.Length; index++)
        {
            expected = expected.Draw(out rolls[index]);
        }
        NativeBattleBeamLaser134Frame next = fixture.Step(frame);
        Assert.Equal(expected, next.Random);
        var moving = fixture.SpriteDraws.Where(call => call.Task.Callback == 0x6B0D90).ToArray();
        Assert.Equal(4, moving.Length);
        Assert.All(moving, call => Assert.Equal((ushort)0, call.Task.ReadWord(18)));
        NativeBattleModulePoolTask main = frame.Effect.Pools[1].Slots[0];
        for (int index = 0; index < 4; index++)
        {
            NativeBattleModulePoolTask drawn = moving[index].Task;
            NativeBattleModulePoolTask moved = NativeBattleBeamLaser134Controller.Read(next, moving[index].Address);
            for (int axis = 0; axis < 3; axis++)
            {
                int velocity = (rolls[index * 3 + axis] & 0xFF) - 0x80;
                Assert.Equal(main.ReadWord(20 + axis * 2), drawn.ReadWord(12 + axis * 2));
                Assert.Equal(unchecked((ushort)(drawn.ReadWord(12 + axis * 2) + velocity)), moved.ReadWord(12 + axis * 2));
            }
            Assert.Equal((ushort)1, moved.ReadWord(18));
        }
        Assert.Equal(NativeBattleTemporaryMemory.Address, BitConverter.ToUInt32(next.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void Static_branch_has_its_actual_age_gate_and_unwritten_packed_padding()
    {
        Fixture fixture = new();
        NativeBattleBeamLaser134Frame frame = fixture.Step(fixture.Frame);
        fixture.Clear();
        fixture.Step(Age(frame, 13));
        Assert.Empty(fixture.Operations.OfType<NativeBattleBeamLaser134DrawStatic88>());
        fixture.Clear();
        fixture.Step(Age(frame, 14));
        NativeBattleBeamLaser134DrawStatic88 draw = Assert.Single(fixture.Operations.OfType<NativeBattleBeamLaser134DrawStatic88>());
        Assert.False(draw.World.Known[18]);
        Assert.False(draw.World.Known[19]);
        Assert.All(draw.World.Known.Skip(20), known => Assert.True(known));
        Assert.Equal(0, draw.FadeCue);
        fixture.Clear();
        fixture.Step(Age(frame, 34));
        Assert.Empty(fixture.Operations.OfType<NativeBattleBeamLaser134DrawStatic88>());
    }

    [Fact]
    public void Same_actor_header_revisit_resets_only_root_age_and_does_not_replace_active_main()
    {
        Fixture fixture = new();
        NativeBattleBeamLaser134Frame frame = fixture.Step(fixture.Frame);
        NativeBattleModulePoolTask root = frame.Effect.Pools[0].Slots[0].WriteWord(12, 1).WriteByte(14, 0);
        frame = NativeBattleBeamLaser134Controller.Write(frame, RootBank, root);
        NativeBattleBeamLaser134Frame next = fixture.Step(frame);
        Assert.Equal((ushort)1, next.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Equal((byte)0, next.Effect.Pools[0].Slots[0].ReadByte(14));
        Assert.Equal((ushort)2, next.Effect.Pools[1].Slots[0].ReadWord(12));
    }

    [Fact]
    public void Full_main_bank_for_a_different_actor_does_not_advance_header_ordinal_or_replace_payload()
    {
        Fixture fixture = new();
        NativeBattleBeamLaser134Frame frame = fixture.Step(fixture.Frame);
        frame = NativeBattleBeamLaser134Controller.Write(frame, RootBank, frame.Effect.Pools[0].Slots[0].WriteWord(12, 1).WriteByte(14, 0));
        NativeBattleActionPacketsState packets = fixture.Inputs.Packets;
        NativeBattleContactPacket contact = packets.ReadContact(0);
        packets = packets with { Contacts = packets.Contacts.SetItem(0, contact with { Primary = contact.Primary with { ActorIndex = 1 } }) };
        NativeBattleBeamLaser134Frame next = NativeBattleBeamLaser134Controller.Advance(frame, fixture.Resources, fixture.Inputs with { Packets = packets });
        Assert.Equal((byte)0, next.Effect.Pools[0].Slots[0].ReadByte(14));
        Assert.Equal(frame.Effect.Pools[1].Slots[0].ReadDword(16), next.Effect.Pools[1].Slots[0].ReadDword(16));
        Assert.Equal((ushort)2, next.Effect.Pools[0].Slots[0].ReadWord(12));
    }

    [Fact]
    public void Independent_JSON_next_retains_workspaces_flags_and_native_CRT_sequence()
    {
        Fixture fixture = new();
        NativeBattleBeamLaser134Frame frame = Age(fixture.Step(fixture.Frame), 22);
        NativeBattleBeamLaser134Frame imported = NativeBattleTestSnapshots.RestoreJson<NativeBattleBeamLaser134Frame>(NativeBattleTestSnapshots.Json(frame))!;
        NativeBattleBeamLaser134Frame expected = NativeBattleStorageSnapshots.Capture(fixture.Step(frame));
        NativeBattleBeamLaser134Frame actual = fixture.Step(imported);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
    }

    private static NativeBattleBeamLaser134Frame Age(NativeBattleBeamLaser134Frame frame, int age) => NativeBattleBeamLaser134Controller.Write(frame, frame.Effect.Pools[1].BankAddress, frame.Effect.Pools[1].Slots[0].WriteWord(12, unchecked((ushort)age)));

    private sealed class Fixture
    {
        public NativeBattleBeamLaser134Frame Frame { get; }
        public NativeBattleBeamLaser134Resources Resources { get; }
        public NativeBattleBeamLaser134Inputs Inputs { get; }
        public Dictionary<int, ushort> Flags { get; } = new() { [0] = 3, [3] = 0x1234 };
        public List<NativeBattleBeamLaser134Operation> Operations { get; } = [];
        public List<(int Offset, int Size)> Acts { get; } = [];
        public List<(uint Address, NativeBattleModulePoolTask Task)> SpriteDraws { get; } = [];

        public Fixture()
        {
            byte[] camera = new byte[CameraLength];
            BitConverter.GetBytes((ushort)CameraLength).CopyTo(camera, 6);
            uint[] addresses = [0x12038DC, 0x1204004, 0x1205270, 0x12065F0, 0x1207150];
            int[] lengths = [0x728, 0x126C, 0x1380, 0xB60, 0x52C];
            ImmutableArray<NativeBattleGfAnimationSource> acts = Enumerable.Range(0, 5).Select(index => Source(addresses[index], lengths[index])).ToImmutableArray();
            Resources = new(new("FF8_EN:1207680", camera.ToImmutableArray()), acts, Sprite(0x1201EBC), Sprite(0x1202068), new(0, 0x1202214, new byte[0x16C8].ToImmutableArray()), 0x61B82);
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(3, 8, 14, 0, 176, 134, null));
            (packets, _) = packets.AppendContact(new(new(0, 1, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            packets = packets.WritePlayerRepeatMode(0, 1);
            (packets, _) = packets.Publish(1);
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
            (root, int slot) = root.Append(RootCallback);
            root = NativeBattleTaskFixture.EditPool(root, data => data with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteBytes(12, new byte[8]).WriteWord(12, 1)) });
            NativeBattleOriginalEffectState effect = new(134, 133, new(134, Entry, Initializer), new(3, 0, 1, 0, 0, 0), null) { Pools = [root], ReturnedDescriptor = RootDescriptor, ResourcePointers = [new(InitializerGlobal, 0x30000)] };
            byte[] tim = new byte[546];
            BitConverter.GetBytes(0x10).CopyTo(tim, 0);
            BitConverter.GetBytes(9).CopyTo(tim, 4);
            BitConverter.GetBytes(524).CopyTo(tim, 8);
            BitConverter.GetBytes((short)320).CopyTo(tim, 12);
            BitConverter.GetBytes((short)240).CopyTo(tim, 14);
            BitConverter.GetBytes((short)256).CopyTo(tim, 16);
            BitConverter.GetBytes((short)1).CopyTo(tim, 18);
            BitConverter.GetBytes(14).CopyTo(tim, 532);
            BitConverter.GetBytes((short)384).CopyTo(tim, 536);
            BitConverter.GetBytes((short)256).CopyTo(tim, 538);
            BitConverter.GetBytes((short)1).CopyTo(tim, 540);
            BitConverter.GetBytes((short)1).CopyTo(tim, 542);
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim) };
            Frame = new(memory, effect, new(123), NativeBattleBeamLaser134State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CasterGlobal, 0x1D97494).WriteDword(SavedCasterFlagsGlobal, 0)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            Inputs = new(packets, actor => new(actor, Flags[actor]) { MinimumY146 = -2000, Transform = new(Initial20: new(0, 0, 0), Position28: new(700, -300, -500), Vertical34: 0, Angle36: 0, Scale38: 512) }, (current, _, selector, _) => new(current, selector == 0xF1 ? new(700, -300, -500) : new(100, -800, 300)), (current, _) => new(current, new(700, -300, -500, 512)), (current, matrix, local, _, anchor) =>
            {
                Assert.False(matrix.Known[18]);
                Assert.False(matrix.Known[20]);
                NativeBattleVector updated = new(unchecked((short)(local.X + anchor.X)), unchecked((short)(local.Y + anchor.Y)), unchecked((short)(local.Z + anchor.Z)));
                return new(current, NativeBattleEffectArena.Clear(), updated);
            }, (current, _) => new(current, NativeBattleEffectArena.Clear()), (current, _, _) => new(current, NativeBattleEffectArena.Clear()), (current, _, offset, size, _, _) =>
            {
                Acts.Add((offset, size));
                return new(current, 0);
            }, (current, operation) =>
            {
                Operations.Add(operation);
                if (operation is NativeBattleBeamLaser134ActorFlags flags)
                {
                    Flags[flags.Actor] = (ushort)((Flags[flags.Actor] & ~flags.Mask) | (flags.Bits & flags.Mask));
                }
                if (operation is NativeBattleBeamLaser134DrawSprite180 draw)
                {
                    NativeBattleModulePoolTask task = NativeBattleBeamLaser134Controller.Read(current, draw.TaskAddress);
                    SpriteDraws.Add((draw.TaskAddress, new(task.Flags, task.State, task.Next, task.Callback, task.ReadBytes(12, task.PayloadLength).ToImmutableArray())));
                }
                return current;
            });
        }
        public NativeBattleBeamLaser134Frame Step(NativeBattleBeamLaser134Frame frame) => NativeBattleBeamLaser134Controller.Advance(frame, Resources, Inputs);
        public void Clear()
        {
            Operations.Clear();
            Acts.Clear();
            SpriteDraws.Clear();
        }
        private static NativeBattleGfAnimationSource Source(uint address, int length)
        {
            byte[] data = new byte[length];
            BitConverter.GetBytes(8).CopyTo(data, 4);
            BitConverter.GetBytes(56).CopyTo(data, 12);
            return new(address, data.ToImmutableArray());
        }
        private static NativeBattleModuleResource Sprite(uint address)
        {
            byte[] data = new byte[0x1AC];
            BitConverter.GetBytes((ushort)16).CopyTo(data, 8);
            for (int index = 0; index <= 16; index++)
            {
                BitConverter.GetBytes((ushort)0x40).CopyTo(data, 10 + index * 2);
            }
            return new(0, address, 0, 0, data.ToImmutableArray());
        }
    }
}
