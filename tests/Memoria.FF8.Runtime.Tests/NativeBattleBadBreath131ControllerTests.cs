using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBadBreath131Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBadBreath131ControllerTests
{
    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    public void Root_selects_actual_private_bank_before_first_child_initialization(uint toggle, int offset, uint next)
    {
        Fixture fixture = new();
        NativeBattleBadBreath131Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [fixture.Frame.Effect.Pools[0]] } };
        frame = NativeBattleBadBreath131Controller.Write(frame, RootBank, NativeBattleBadBreath131Controller.Read(frame, RootBank).WriteDword(16, toggle));
        var actual = NativeBattleBadBreath131Controller.AdvanceRoot(frame, RootBank, fixture.Resources, fixture.Inputs);
        Assert.Equal(0, actual.Result);
        Assert.Equal(ArenaAddress + (uint)offset, actual.Frame.State.ReadDword(CursorGlobal));
        Assert.Equal(next, NativeBattleBadBreath131Controller.Read(actual.Frame, RootBank).ReadDword(16));
        Assert.Empty(fixture.Operations);
    }

    [Fact]
    public void Root_initializes_native_pools_sorts_targets_ascending_and_uses_WORD_wrapped_mean_Z()
    {
        Fixture fixture = new();
        NativeBattleBadBreath131Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = [fixture.Frame.Effect.Pools[0]] } };
        frame = NativeBattleBadBreath131Controller.Write(frame, RootBank, NativeBattleBadBreath131Controller.Read(frame, RootBank).WriteWord(12, 1));
        var actual = NativeBattleBadBreath131Controller.AdvanceRoot(frame, RootBank, fixture.Resources, fixture.Inputs);
        Assert.Equal(4, actual.Frame.Effect.Pools.Length);
        Assert.Equal(new ushort[] { 20, 52, 324, 24 }, actual.Frame.Effect.Pools.Select(pool => pool.Stride).ToArray());
        NativeBattleModulePoolTask main = NativeBattleBadBreath131Controller.Read(actual.Frame, Fixture.MainAddress);
        Assert.Equal((ushort)3, main.ReadWord(14));
        Assert.Equal(new uint[] { Fixture.ModelAddress(4), Fixture.ModelAddress(5), Fixture.ModelAddress(3) }, Enumerable.Range(0, 3).Select(index => main.ReadDword(16 + index * 4)).ToArray());
        Assert.Equal(new byte[] { 2, 0, 1 }, Enumerable.Range(0, 3).Select(index => main.ReadByte(0x1C + index)).ToArray());
        Assert.Equal(unchecked((ushort)(unchecked((short)90000) / 3)), main.ReadWord(0x20));
        Assert.Equal((ushort)1, main.ReadWord(12));
        Assert.Equal((short)125, BinaryPrimitives.ReadInt16LittleEndian(actual.Frame.State.ReadMesh(MeshAAddress).AsSpan().Slice(24, 2)));
    }

    [Theory]
    [InlineData(14, 0)]
    [InlineData(15, 2)]
    [InlineData(64, 2)]
    [InlineData(65, 0)]
    public void Main_draws_only_native_fifty_frame_window_and_always_queries_current_caster_bone(int age, int draws)
    {
        Fixture fixture = new();
        var actual = NativeBattleBadBreath131Controller.AdvanceMain(fixture.Age(age, 3), Fixture.MainAddress, fixture.Resources, fixture.Inputs);
        Assert.Equal(draws, fixture.Operations.OfType<NativeBattleBadBreath131DrawStrip92>().Count());
        Assert.Equal((0, (short)11, 8192), Assert.Single(fixture.Bones));
        NativeBattleModulePoolTask task = NativeBattleBadBreath131Controller.Read(actual.Frame, Fixture.MainAddress);
        Assert.Equal((ushort)100, task.ReadWord(0x2C));
        Assert.Equal((ushort)200, task.ReadWord(0x2E));
        Assert.Equal((ushort)300, task.ReadWord(0x30));
        Assert.Equal(unchecked((ushort)-50), task.ReadWord(0x22));
        Assert.Equal((ushort)200, task.ReadWord(0x24));
        Assert.Equal((ushort)(age >= 65 ? age : age + 1), task.ReadWord(12));
        Assert.Equal(age >= 65 ? 2 : 0, actual.Result);
        if (draws != 0)
        {
            Assert.Equal(144u, actual.Frame.State.ReadDword(CursorGlobal));
            Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(actual.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
            Assert.All(fixture.Contexts, context => Assert.Throws<NotSupportedException>(() => context.Read(7, 1)));
            Assert.All(fixture.Contexts, context => Assert.Equal(ArenaAddress + ScratchOffset, BinaryPrimitives.ReadUInt32LittleEndian(context.Read(32, 4))));
        }
    }

    [Fact]
    public void Main_spawns_after_phase25_with_original_relative_contact_and_angle_minus_decimal200()
    {
        Fixture fixture = new();
        var actual = NativeBattleBadBreath131Controller.AdvanceMain(fixture.Age(40, 0), Fixture.MainAddress, fixture.Resources, fixture.Inputs);
        NativeBattleModulePoolTask child = Assert.Single(actual.Frame.Effect.Pools[2].Slots.Where(task => (task.Flags & 1) != 0));
        Assert.Equal((byte)2, child.ReadByte(14));
        Assert.Equal((byte)4, child.ReadByte(15));
        Assert.Equal(0x123456u, child.ReadDword(24));
        Assert.Equal((ushort)100, child.ReadWord(22));
        Assert.Equal(TargetActAddress, child.ReadDword(28));
        Assert.Equal((ushort)1, NativeBattleBadBreath131Controller.Read(actual.Frame, Fixture.MainAddress).ReadWord(0x26));
        Assert.Equal(fixture.Frame.Random, actual.Frame.Random);
    }

    [Fact]
    public void Main_sound_uses_native_flags_zero_pan128_only_at_old20()
    {
        Fixture fixture = new();
        NativeBattleBadBreath131Controller.AdvanceMain(fixture.Age(20, 3), Fixture.MainAddress, fixture.Resources, fixture.Inputs);
        Assert.Equal(new NativeBattleBadBreath131Sound(fixture.Resources.Sound, 0, 128), Assert.Single(fixture.Operations.OfType<NativeBattleBadBreath131Sound>()));
    }

    private sealed class Fixture
    {
        public const uint MainAddress = ArenaAddress + MainOffset;
        public NativeBattleBadBreath131Frame Frame { get; }
        public NativeBattleBadBreath131Resources Resources { get; }
        public NativeBattleBadBreath131Inputs Inputs { get; }
        public List<NativeBattleBadBreath131Operation> Operations { get; } = [];
        public List<(int Actor, short Bone, int Distance)> Bones { get; } = [];
        public List<NativeBattleEffectArena> Contexts { get; } = [];
        public Fixture()
        {
            byte[] mesh = new byte[5248];
            BinaryPrimitives.WriteInt32LittleEndian(mesh, 1328);
            BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 165);
            for (int index = 0; index < 165; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(12 + index * 8), unchecked((short)(-10 * (index + 1))));
            }
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(24), 100);
            byte[] act = new byte[20];
            BinaryPrimitives.WriteInt32LittleEndian(act.AsSpan(4), 8);
            BinaryPrimitives.WriteInt32LittleEndian(act.AsSpan(12), 34);
            Resources = new(new(TargetActAddress, act.ToImmutableArray()), new(0, ParticleSpriteAddress, 0, 0, []), new("unused", []), mesh.ToImmutableArray(), mesh.ToImmutableArray(), new short[8192].ToImmutableArray(), [0x202020, 0x404040, 0x606060, 0x808080], 0xAA3344, 0x64194);
            NativeBattleEffectTaskPool[] pools =
            [
                NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1),
                NativeBattleEffectTaskPool.Initialize(MainDescriptor, MainAddress, MainStride, 1),
                NativeBattleEffectTaskPool.Initialize(TargetDescriptor, ArenaAddress + TargetOffset, TargetStride, TargetCount),
                NativeBattleEffectTaskPool.Initialize(ParticleDescriptor, ArenaAddress, ParticleStride, ParticleCount)
            ];
            (pools[0], _) = pools[0].Append(RootCallback);
            (pools[1], _) = pools[1].Append(MainCallback);
            pools[0] = NativeBattleTaskFixture.EditPool(pools[0], data => data with { Slots = pools[0].Slots.SetItem(0, pools[0].Slots[0].WriteDword(12, 0).WriteDword(16, 0)) });
            NativeBattleModulePoolTask main = pools[1].Slots[0].WriteBytes(12, new byte[40]).WriteWord(14, 3).WriteDword(16, ModelAddress(4)).WriteByte(0x1C, 2);
            pools[1] = NativeBattleTaskFixture.EditPool(pools[1], data => data with { Slots = pools[1].Slots.SetItem(0, main) });
            NativeBattleOriginalEffectState effect = new(131, 131, new(131, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor) { Pools = pools.ToImmutableArray() };
            NativeBattleBadBreath131State state = new(NativeBattleEffectArena.Clear(), mesh.ToImmutableArray(), mesh.ToImmutableArray()) { PendingColdSources = false };
            Frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(ScratchOffset, ScratchLength) }, effect, new(11), state.WriteDword(WorkingGlobal, ArenaAddress).WriteDword(CasterGlobal, ModelAddress(0))) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
            (packets, _) = packets.Prepare(new(0, 8, 184, 0, 14, 131, null));
            foreach (byte actor in new byte[] { 5, 3, 4 })
            {
                (packets, _) = packets.AppendContact(new(new(actor, 1, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
            }
            (packets, _) = packets.Publish(3);
            Inputs = new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, actor => new(actor, 2) { Color40 = 0x123456, Transform = new(new(0, 0, 0), new(unchecked((short)(actor == 4 ? -2000 : actor == 5 ? 0 : 1000)), 0, 30000), 0, 300, 4096) }, _ => 20, _ => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null), (frame, _) => new(frame, new(500, 600, 700, 888)), (frame, actor, bone, distance) =>
            {
                Bones.Add((actor, bone, distance));
                return new(frame, new(100, 200, 300));
            }, (frame, _, _, _, _, _) => new(frame, 1), (frame, operation) =>
            {
                Operations.Add(operation);
                if (operation is NativeBattleBadBreath131DrawStrip92 draw)
                {
                    Contexts.Add(NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(frame.Staging!, draw.TemporaryAddress, 92), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 92), 0, 92));
                    frame = frame with { State = frame.State.WriteDword(CursorGlobal, frame.State.ReadDword(CursorGlobal) + 72) };
                }
                return frame;
            });
        }
        public NativeBattleBadBreath131Frame Age(int age, int selected) => NativeBattleBadBreath131Controller.Write(Frame, MainAddress, NativeBattleBadBreath131Controller.Read(Frame, MainAddress).WriteWord(12, unchecked((ushort)age)).WriteWord(0x26, unchecked((ushort)selected)));
        public static uint ModelAddress(int actor) => 0x1D972C0u + (uint)actor * 156;
    }
}
