using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect31Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect31ActorTests
{
    [Theory]
    [InlineData(0x872920u, 16)]
    [InlineData(0x872B10u, 15)]
    [InlineData(0x8731D0u, 16)]
    public void Delays_compare_absolute_age_and_transition_without_reset(uint callback, int threshold)
    {
        Fixture fixture = new();
        (NativeBattleEffect31Frame frame, uint address) = fixture.Task(4, callback);
        frame = Write(frame, address, Read(frame, address).WriteWord(0x24, unchecked((ushort)(threshold - 1))));
        frame = NativeBattleEffect31Actors.Advance(frame, address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal(0, Read(frame, address).ReadByte(0x29));
        Assert.Equal(threshold, Read(frame, address).ReadWord(0x24));
        frame = NativeBattleEffect31Actors.Advance(frame, address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal(1, Read(frame, address).ReadByte(0x29));
        Assert.Equal(threshold + 1, Read(frame, address).ReadWord(0x24));
        Assert.Equal(new NativeCrtRandomState(1), frame.Random);
        Assert.Empty(fixture.Draws);
    }

    [Fact]
    public void Wave_particle_uses_signed_truncation_and_hides_before_terminal_draw()
    {
        Fixture fixture = new();
        (NativeBattleEffect31Frame frame, uint address) = fixture.Task(5, 0x8723E0);
        frame = Write(frame, address, Read(frame, address).WriteWord(0x78, unchecked((ushort)-17)).WriteWord(0x7A, unchecked((ushort)-129)).WriteWord(0x7C, 17).WriteWord(0x50, 7).WriteWord(0x52, 7).WriteWord(0x24, 90));
        NativeBattleEffect31Step result = NativeBattleEffect31Actors.Advance(frame, address, fixture.Resources, fixture.Inputs);
        NativeBattleModulePoolTask task = Read(result.Frame, address);
        Assert.Equal(unchecked((ushort)-14), task.ReadWord(0x78));
        Assert.Equal(ushort.MaxValue, task.ReadWord(0x7A));
        Assert.Equal(14, task.ReadWord(0x7C));
        Assert.Equal(0, task.ReadWord(0x1C));
        Assert.Equal(0, task.ReadWord(0x1E));
        Assert.Equal(0, task.ReadWord(0x20));
        Assert.Equal(7, task.ReadWord(0x50));
        Assert.Equal(5, task.ReadByte(0x26));
        Assert.Equal(1, task.ReadByte(0x29));
        Assert.Equal(91, task.ReadWord(0x24));
        Assert.Equal(2, result.Result);
        Assert.Empty(fixture.Draws);
    }

    [Theory]
    [InlineData(0x873010u, 18)]
    [InlineData(0x8730C0u, 14)]
    public void Trail_terminal_frame_clamps_and_skips_unavailable_sprite_resource(uint callback, int last)
    {
        Fixture fixture = new();
        (NativeBattleEffect31Frame frame, uint address) = fixture.Task(5, callback);
        frame = Write(frame, address, Read(frame, address).WriteByte(0x29, 1).WriteWord(0x50, unchecked((ushort)last)).WriteWord(0x52, unchecked((ushort)last)));
        NativeBattleEffect31Step result = NativeBattleEffect31Actors.Advance(frame, address, fixture.Resources, fixture.Inputs);
        Assert.Equal(2, result.Result);
        Assert.Equal(last, Read(result.Frame, address).ReadWord(0x50));
        Assert.Equal(2, Read(result.Frame, address).ReadByte(0x29));
        Assert.Equal(5, Read(result.Frame, address).ReadByte(0x26));
        Assert.Empty(fixture.Draws);
    }

    [Fact]
    public void Waves_append_26_then_13_children_and_consume_three_ordered_randoms_each()
    {
        Fixture fixture = new();
        (NativeBattleEffect31Frame frame, uint address) = fixture.Task(5, 0x872170);
        frame = WriteBytes(frame, address + 0x58, PackedArena(fixture.Identity), 32);
        frame = NativeBattleEffect31Actors.Advance(frame, address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal(26, Read(frame, address).ReadByte(0x28));
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < 78; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        NativeBattleModulePoolTask[] children = frame.Effect.Pools[5].Slots.Where(task => task.Callback == 0x8723E0 && (task.Flags & 1) != 0).ToArray();
        Assert.Equal(26, children.Length);
        Assert.Equal(10, children.Count(task => task.ReadDword(0x4C) == 0x15F95E0));
        Assert.Equal(16, children.Count(task => task.ReadDword(0x4C) == 0x15F96BC));
        Assert.All(children, child => Assert.Equal(address, child.ReadDword(0x18)));
        frame = NativeBattleEffect31Actors.Advance(frame, address, fixture.Resources, fixture.Inputs).Frame;
        for (int index = 0; index < 39; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.Equal(39, Read(frame, address).ReadByte(0x28));
        Assert.Equal(2, Read(frame, address).ReadByte(0x29));
        Assert.Equal(1, Read(frame, address).ReadByte(0x26));
        Assert.Empty(fixture.Draws);
    }

    [Fact]
    public void Flight_terminal_sample_still_appends_four_children_and_never_reads_ACT_workspace()
    {
        Fixture fixture = new();
        (NativeBattleEffect31Frame frame, uint address) = fixture.Task(4, 0x872B10);
        frame = Write(frame, address, Read(frame, address).WriteByte(0x29, 2).WriteWord(0x1AE, 8).WriteWord(0x19C, 100).WriteWord(0x19E, 200).WriteWord(0x1A0, 300).WriteWord(0x1A4, 500).WriteWord(0x1A6, 600).WriteWord(0x1A8, 700));
        frame = NativeBattleEffect31Actors.Advance(frame, address, fixture.Resources, fixture.Inputs).Frame;
        NativeBattleModulePoolTask task = Read(frame, address);
        Assert.Equal(3, task.ReadByte(0x29));
        Assert.Equal(1, task.ReadByte(0x26));
        Assert.Equal(4, task.ReadByte(0x28));
        Assert.Equal(9, task.ReadWord(0x1AE));
        Assert.Equal(500, task.ReadWord(0x1C));
        Assert.Equal(600, task.ReadWord(0x1E));
        Assert.Equal(700, task.ReadWord(0x20));
        Assert.Equal(new uint[] { 0x873010, 0x873010, 0x8730C0, 0x8730C0 }, frame.Effect.Pools[5].Slots.Take(4).Select(child => child.Callback));
        NativeCrtRandomState expected = new(1);
        for (int index = 0; index < 8; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected, frame.Random);
        Assert.Empty(fixture.Draws);
        Assert.Contains((0x872CB0u, 0x872DF3u), fixture.Observations);
    }

    [Fact]
    public void Persistent_program_keeps_cleared_zero_rotation_and_ignores_first_remaining_zero()
    {
        Fixture fixture = new();
        (NativeBattleEffect31Frame frame, uint address) = fixture.Task(4, 0x8731D0);
        frame = Write(frame, address, Read(frame, address).WriteByte(0x29, 1).WriteDword(0x74, 0x160AF24).WriteDword(0x78, 0x6C));
        NativeBattleEffect31Step first = NativeBattleEffect31Actors.Advance(frame, address, fixture.Resources, fixture.Inputs);
        Assert.Equal(0, first.Result);
        Assert.Equal(2, Read(first.Frame, address).ReadByte(0x29));
        Assert.Equal(0, Read(first.Frame, address).ReadByte(0x26));
        Assert.Equal(new byte[18], ReadBytes(first.Frame, address + 0x30, 18).Read(0, 18));
        NativeBattleEffect31Step second = NativeBattleEffect31Actors.Advance(first.Frame, address, fixture.Resources, fixture.Inputs);
        Assert.Equal(2, second.Result);
        Assert.Equal(3, Read(second.Frame, address).ReadByte(0x29));
        Assert.Equal(new byte[18], ReadBytes(second.Frame, address + 0x30, 18).Read(0, 18));
    }

    [Fact]
    public void Unknown_caller_rotation_is_never_seeded_from_bone_or_identity()
    {
        Fixture fixture = new();
        (NativeBattleEffect31Frame frame, uint address) = fixture.Task(4, 0x871F60);
        frame = Write(frame, address, Read(frame, address).WriteByte(0x29, 1));
        NativeBattlePackedMatrix32 unknown = new(new byte[32].ToImmutableArray(), Enumerable.Repeat(false, 32).ToImmutableArray());
        NativeBattleEffect31Inputs inputs = fixture.Inputs with { ReadActorStack = (current, _, body) => new(current, unknown) };
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect31Actors.Advance(frame, address, fixture.Resources, inputs));
        Assert.Empty(fixture.Draws);
    }

    private sealed class Fixture
    {
        public NativeBattleEffect31Frame Frame { get; }
        public NativeBattleEffect31Inputs Inputs { get; }
        public NativeBattleEffect31Resources Resources { get; }
        public NativeBattlePackedMatrix32 Identity { get; } = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0);
        public List<NativeBattleEffect31DrawBatch> Draws { get; } = [];
        public List<(uint Body, uint Instruction)> Observations { get; } = [];

        public Fixture()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
            (packets, _) = packets.Prepare(new(3, 8, 13, 0, 26, 31, null));
            (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            Inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => packets, (_, actor) => new(actor, 0), (_, _) => null!, _ => Identity, _ => new(null, 100, 160, 108), _ => 0, (_, _, _) => NativeBattleEffectArena.Clear(), (current, _, _, _, retained) => new(current, retained), (current, operation) =>
            {
                if (operation is NativeBattleEffect31DrawBatch draw)
                {
                    Draws.Add(draw);
                }
                return current;
            })
            {
                ReadActorStack = (current, _, _) => new(current, Identity),
                ObserveActorStack = (current, _, body, instruction, _) =>
                {
                    Observations.Add((body, instruction));
                    return current;
                }
            };
            byte[] programme = new byte[16];
            BitConverter.GetBytes(8).CopyTo(programme, 4);
            ImmutableDictionary<uint, ImmutableArray<byte>> staticBytes = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(FactorAddress, new short[] { 656, 1232, 1792, 2304, 2768, 3184, 3552, 3872, 4096 }.SelectMany(BitConverter.GetBytes).ToImmutableArray()).Add(YOffsetAddress, new short[] { 256, 448, 576, 640, 640, 576, 448, 256, 0 }.SelectMany(BitConverter.GetBytes).ToImmutableArray());
            Resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty.Add(0x160AF24, new(0x160AF24, programme.ToImmutableArray())), ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, staticBytes);
            NativeBattleOriginalEffectState effect = new(31, 31, new(31, Entry, Initializer), new(3, 1, 1, 0, 0, 0), null)
            {
                Pools = Pools.Select(shape => NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity)).ToImmutableArray(),
                ReturnedDescriptor = RootDescriptor
            };
            NativeBattleEffect31Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(1), NativeBattleEffect31State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            (frame, uint root) = AppendHierarchy(frame, 0, RootCallback, 0x64, 0, Inputs);
            Frame = Write(frame, root, Read(frame, root).WriteDword(12, 0x1D99A78));
        }

        public (NativeBattleEffect31Frame Frame, uint Address) Task(int pool, uint callback) => AppendHierarchy(Frame, pool, callback, Pools[pool].Stride, RootBank, Inputs);
    }
}
