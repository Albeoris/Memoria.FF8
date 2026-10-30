using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShockwavePulsar260ControllerTests
{
    [Theory]
    [InlineData(206, 585)]
    [InlineData(212, 4096)]
    [InlineData(213, 4096)]
    public void Fixed_flash_envelope_runs_before_ACT_and_has_actual_last_plateau(int age, int factor)
    {
        Fixture fixture = new(age);
        NativeBattleShockwavePulsar260TaskStep step = fixture.Advance();
        NativeBattleShockwavePulsar260Flash flash = Assert.Single(fixture.Operations.OfType<NativeBattleShockwavePulsar260Flash>());
        Assert.Equal(new NativeBattleShockwavePulsar260Flash(255, 255, 255, factor), flash with { PilotIdentity = null });
        Assert.Equal((ushort)260, flash.PilotIdentity!.Value.Effect);
        Assert.DoesNotContain("PilotIdentity", JsonSerializer.Serialize(flash));
        Assert.True(fixture.Operations.IndexOf(flash) < fixture.Operations.FindIndex(operation => operation is NativeBattleShockwavePulsar260ActNode));
        Assert.Equal((ushort)(age + 1), NativeBattleShockwavePulsar260Controller.Read(step.Frame, MainAddress).ReadWord(0xC));
    }

    [Fact]
    public void Contact252_is_after_IO_and_wait262_keeps_age_and_clears_only_descriptor_links()
    {
        Fixture contact = new(252);
        contact.Advance();
        Assert.True(contact.Operations.FindIndex(operation => operation is NativeBattleShockwavePulsar260AdvanceIo) < contact.Operations.FindIndex(operation => operation is NativeBattleShockwavePulsar260ContactAll));
        Fixture wait = new(262);
        NativeBattleShockwavePulsar260TaskStep busy = wait.Advance(busy: true);
        Assert.Equal(0, busy.Result);
        Assert.Equal((ushort)262, NativeBattleShockwavePulsar260Controller.Read(busy.Frame, MainAddress).ReadWord(0xC));
        Assert.Null(busy.Frame.Effect.Pools[2].Head);
        Assert.Equal(1, busy.Frame.Effect.Pools[2].Slots[0].Flags & 1);
        NativeBattleShockwavePulsar260TaskStep done = wait.Advance(busy: false);
        Assert.Equal(2, done.Result);
        Assert.Equal(1, wait.LastPoll);
        Assert.Single(wait.Operations.OfType<NativeBattleShockwavePulsar260CameraRestore>());
    }

    [Fact]
    public void Position_restoration214_preserves_saved_W_and_changes_only_original_hidden_bit()
    {
        Fixture fixture = new(214, 1);
        NativeBattleShockwavePulsar260TaskStep step = fixture.Advance();
        NativeBattleShockwavePulsar260ActorWrite position = Assert.Single(fixture.Operations.OfType<NativeBattleShockwavePulsar260ActorWrite>());
        Assert.Equal(0x1C, position.Offset);
        Assert.Equal(new byte[] { 11, 0, 22, 0, 33, 0 }, position.Bytes.Read(0, 6));
        Assert.Throws<NotSupportedException>(() => position.Bytes.Read(6, 2));
        Assert.Equal((ushort)6, fixture.Models[3].Flags0);
        Assert.Equal(4096u, step.Frame.State.ReadDword(0x25086A8));
        NativeBattleShockwavePulsar260ActNode node = Assert.Single(fixture.Operations.OfType<NativeBattleShockwavePulsar260ActNode>());
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(node.Context20.Read(2, 2)));
        Assert.Equal((short)-4096, BinaryPrimitives.ReadInt16LittleEndian(node.Context20.Read(12, 2)));
    }

    [Fact]
    public void Particle_updates_wrap_WORDs_use_signed_BYTE_velocity_and_do_not_read_dead_tails()
    {
        Fixture fixture = new(270);
        NativeBattleShockwavePulsar260Frame frame = fixture.Frame;
        NativeBattleEffectArena arena = frame.Memory.Arena!.MarkUnknown(PuffsOffset, PuffsCount * 16).MarkUnknown(SecondaryParticlesOffset, SecondaryParticlesCount * 16);
        for (int i = 0; i < PuffsCount; i++)
        {
            arena = arena.Write(PuffsOffset + i * 16 + 15, [0]);
        }
        for (int i = 0; i < SecondaryParticlesCount; i++)
        {
            arena = arena.Write(SecondaryParticlesOffset + i * 16, new byte[2]);
        }
        arena = arena.Write(PuffsOffset + 4, new byte[] { 0, 128, 255, 127, 10, 0 }).Write(PuffsOffset + 12, new byte[] { 255, 1, 128, 1 });
        arena = arena.Write(SecondaryParticlesOffset, BitConverter.GetBytes((ushort)1)).Write(SecondaryParticlesOffset + 4, BitConverter.GetBytes((ushort)65535)).Write(SecondaryParticlesOffset + 6, BitConverter.GetBytes((ushort)5)).Write(SecondaryParticlesOffset + 8, BitConverter.GetBytes((ushort)2)).Write(SecondaryParticlesOffset + 10, BitConverter.GetBytes((ushort)65530));
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        frame = NativeBattleShockwavePulsar260Controller.UpdateSecondaryParticles(NativeBattleShockwavePulsar260Controller.UpdatePuffs(frame));
        Assert.Equal((ushort)32764, Word(frame, PuffsOffset + 4));
        Assert.Equal((ushort)32771, Word(frame, PuffsOffset + 6));
        Assert.Equal(unchecked((ushort)-502), Word(frame, PuffsOffset + 8));
        Assert.Equal((ushort)1, Word(frame, SecondaryParticlesOffset + 4));
        Assert.Equal((ushort)65535, Word(frame, SecondaryParticlesOffset + 6));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(PuffsOffset + 10, 2));
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(NativeBattleTestSnapshots.RestoreJson<NativeBattleShockwavePulsar260Frame>(NativeBattleTestSnapshots.Json(frame))));
    }

    [Fact]
    public void Main_puff_birth_uses_twelve_CRT_draws_and_updates_new_particles_after_drawing()
    {
        Fixture fixture = new(93);
        NativeBattleShockwavePulsar260TaskStep step = fixture.Advance();
        NativeCrtRandomState expected = fixture.Frame.Random;
        for (int index = 0; index < 12; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, step.Frame.Random);
        NativeCrtRandomState first = fixture.Frame.Random.Draw(out _);
        _ = first.Draw(out ushort angle);
        Assert.Equal(unchecked((ushort)(NativeBattleCameraMath.Sine(angle) >> 3)), Word(step.Frame, PuffsOffset + 4));
        Assert.Equal(unchecked((ushort)((NativeBattleCameraMath.Cosine(angle) >> 3) + 2048)), Word(step.Frame, PuffsOffset + 8));
        for (int index = 0; index < 4; index++)
        {
            int row = PuffsOffset + index * 16;
            Assert.Equal((byte)79, step.Frame.Memory.Arena!.Read(row + 15, 1)[0]);
            Assert.Equal(unchecked((ushort)((index & 1) == 0 ? -4048 : 4048)), Word(step.Frame, row + 6));
            Assert.Equal(0x111D0F8u, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Memory.Arena.Read(row, 4)));
        }
    }
    [Theory]
    [InlineData(1, 50)]
    [InlineData(9, 42)]
    [InlineData(17, 34)]
    [InlineData(25, 26)]
    public void Late_target_delay_uses_raw_age_minus_one_not_contact_index(int age, int delay)
    {
        Fixture fixture = new(age, 4);
        NativeBattleShockwavePulsar260TaskStep step = fixture.Advance();
        NativeBattleModulePoolTask child = Assert.Single(step.Frame.Effect.Pools[2].Slots, task => task.Callback == LateTargetCallback);
        Assert.Equal((ushort)delay, child.ReadWord(0x12));
        Assert.Equal(MainAddress + 0x10u + (uint)(((age - 1) / 8) * 0x24), child.ReadDword(0xC));
    }
    private const uint MainAddress = ArenaAddress + MainOffset;
    private static ushort Word(NativeBattleShockwavePulsar260Frame frame, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena!.Read(offset, 2));
    private sealed class Fixture
    {
        public NativeBattleShockwavePulsar260Frame Frame { get; }
        public NativeBattleGraphicsModel[] Models { get; } = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, index == 3 ? (ushort)14 : (ushort)0)).ToArray();
        public List<NativeBattleShockwavePulsar260Operation> Operations { get; } = [];
        public int LastPoll { get; private set; }
        private NativeBattleShockwavePulsar260Resources Resources { get; }
        public Fixture(int age, ushort targets = 0)
        {
            byte[] data = new byte[32];
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 8);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8), 1);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(12), 500);
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(16), 16);
            Resources = new(null!, [new(0x112015C, data.ToImmutableArray()), new(0x111DEBC, data.ToImmutableArray())], [], null!, [], [], [], []);
            NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 16, 1);
            NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainAddress, MainStride, 1);
            (main, int mainIndex) = main.Append(MainCallback);
            main = NativeBattleTaskFixture.EditPool(main, data => data with { Slots = main.Slots.SetItem(mainIndex, main.Slots[mainIndex].WriteWord(0xC, (ushort)age).WriteWord(0xE, targets)) });
            NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(ChildDescriptor, ArenaAddress + ChildrenOffset, ChildStride, ChildCount);
            (children, _) = children.Append(ChildActCallback);
            NativeBattleOriginalEffectState effect = new(260, 259, new(260, Entry, Initializer), null, RootDescriptor) { Pools = [root, main, children] };
            NativeBattleShockwavePulsar260State state = NativeBattleShockwavePulsar260State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress).WriteDword(SavedFlagsAddress + 12, 6).WriteDword(0x2508614, ArenaAddress + PuffsOffset).WriteDword(0x2508618, ArenaAddress + SecondaryParticlesOffset);
            NativeBattleShockwavePulsar260Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), EffectIo = new(0x534150, 0, 0) }, effect, new(123), state);
            frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, effect.Pools) };
            NativeBattleEffectArena record = NativeBattleEffectArena.Clear().MarkUnknown(0, 36).Write(0, BitConverter.GetBytes(0x1D972C0u + 3 * 156u)).Write(4, new byte[] { 11, 0, 22, 0, 33, 0 }).Write(0x14, BitConverter.GetBytes(0u));
            for (int index = 0; index < Math.Max(1, (int)targets); index++)
            {
                frame = NativeBattleShockwavePulsar260Controller.WriteView(frame, MainAddress + 0x10u + (uint)(index * 0x24), record, 36);
            }
            Frame = NativeBattleShockwavePulsar260Controller.InitializeAct(frame, MainAddress, 0x198, 0x1100, 0x112015C, Resources);
        }
        public NativeBattleShockwavePulsar260TaskStep Advance(bool busy = false)
        {
            Operations.Clear();
            return NativeBattleShockwavePulsar260Controller.AdvanceMain(Frame, MainAddress, Resources, new(NativeBattleActionPacketsState.Fresh(), 368, actor => Models[actor], _ => throw new InvalidOperationException(), _ => null, _ => throw new InvalidOperationException(), _ => NativeBattleCameraState.FromColdProcess(), (_, _) => throw new InvalidOperationException(), (frame, index) => { LastPoll = index; return new(frame, busy); }, Execute));
        }
        private NativeBattleShockwavePulsar260Frame Execute(NativeBattleShockwavePulsar260Frame frame, NativeBattleShockwavePulsar260Operation operation)
        {
            Operations.Add(operation);
            if (operation is NativeBattleShockwavePulsar260ActorFlags flags)
            {
                Models[flags.Actor] = Models[flags.Actor] with { Flags0 = (ushort)((Models[flags.Actor].Flags0 & ~flags.ClearMask) | flags.SetMask) };
            }
            return frame;
        }
    }
}
