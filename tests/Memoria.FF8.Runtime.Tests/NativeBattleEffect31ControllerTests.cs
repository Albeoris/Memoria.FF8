using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect31Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect31ControllerTests
{
    [Fact]
    public void Fade_uses_four_physical_groups_retains_high_word_and_never_resets_age()
    {
        Fixture fixture = new();
        (NativeBattleEffect31Frame frame, uint address) = fixture.Task(6, 0x86CD70);
        frame = Write(frame, address, Read(frame, address).WriteDword(0x1C, 0xABCD4321).WriteWord(0x24, 27));
        frame = AdvanceTask(frame, address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal(0xABCD0000u, Read(frame, address).ReadDword(0x1C));
        Assert.Equal(28, Read(frame, address).ReadWord(0x24));
        Assert.Equal(4, fixture.Operations.OfType<NativeBattleEffect31StageTransition>().Count(value => value.ClearRgb));
        NativeBattleEffect31Step result = new(frame, 0);
        for (int index = 0; index < 4; index++)
        {
            result = AdvanceTask(result.Frame, address, fixture.Resources, fixture.Inputs);
        }
        Assert.Equal(2, result.Result);
        Assert.Equal(0xABCD0400u, Read(result.Frame, address).ReadDword(0x1C));
        Assert.Equal(32, Read(result.Frame, address).ReadWord(0x24));
        Assert.Equal(0, Read(result.Frame, RootBank).ReadByte(0x28));
        Assert.Equal(20, fixture.Operations.OfType<NativeBattleEffect31StageTransition>().Count());
    }

    [Fact]
    public void Target_birth_keeps_factory_order_arguments_and_even_empty_child()
    {
        Fixture fixture = new();
        (NativeBattleEffect31Frame frame, uint address) = fixture.Task(1, 0x86CE90);
        frame = AdvanceTask(frame, address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal(new uint[] { 0x871F60, 0x872920, 0x872B10, 0x8731D0 }, frame.Effect.Pools[4].Slots.Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
        Assert.Equal(new uint[] { 0x100, 0x2C, 0x7C, 0x6C }, frame.Effect.Pools[4].Slots.Take(4).Select(task => task.ReadDword(0x78)));
        Assert.Equal(new ushort[] { 0, 1, 2, 3 }, frame.Effect.Pools[4].Slots.Take(4).Select(task => task.ReadWord(0x80)));
        NativeBattleModulePoolTask custom = frame.Effect.Pools[3].Slots[0];
        Assert.Equal(CustomDirectoryAddress, custom.ReadDword(0x30));
        Assert.Equal(45, custom.ReadWord(0x29E));
        Assert.Equal(0x86D100u, frame.Effect.Pools[5].Slots[0].Callback);
        Assert.Equal(6, Read(frame, address).ReadByte(0x28));
        Assert.Equal((byte)0x80, Assert.Single(fixture.Operations.OfType<NativeBattleEffect31Sound501330>()).Pan);
        NativeBattleEffect31Step empty = AdvanceTask(frame, Pools[5].Bank, fixture.Resources, fixture.Inputs);
        Assert.Equal(2, empty.Result);
        Assert.Equal(5, Read(empty.Frame, address).ReadByte(0x28));
    }

    [Fact]
    public void Target_unlock_at_age30_precedes_one_contact_at_age31()
    {
        Fixture fixture = new();
        (NativeBattleEffect31Frame frame, uint address) = fixture.Task(1, 0x86CE90);
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x63, 1));
        frame = Write(frame, address, Read(frame, address).WriteByte(0x29, 1).WriteWord(0x24, 30));
        frame = AdvanceTask(frame, address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Empty(fixture.Operations.OfType<NativeBattleEffect31Contact506690>());
        Assert.Equal(0, Read(frame, RootBank).ReadByte(0x63));
        Assert.Equal(31, Read(frame, address).ReadWord(0x24));
        NativeBattleEffect31Step result = AdvanceTask(frame, address, fixture.Resources, fixture.Inputs);
        Assert.Equal(2, result.Result);
        Assert.Equal(fixture.Contact, Assert.Single(fixture.Operations.OfType<NativeBattleEffect31Contact506690>()).AbsoluteIndex);
        Assert.Equal(32, Read(result.Frame, address).ReadWord(0x24));
    }

    [Fact]
    public void Root_loops_header_group_and_tests_previous_survivor_sum()
    {
        Fixture fixture = new();
        NativeBattleEffect31Frame frame = Write(fixture.Frame, RootBank, Read(fixture.Frame, RootBank).WriteByte(0x29, 4).WriteWord(0x58, 1));
        frame = AdvanceTask(frame, RootBank, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal(1, Read(frame, RootBank).ReadByte(0x2A));
        Assert.Equal(0, Read(frame, RootBank).ReadByte(0x2B));
        Assert.Equal(1, Read(frame, RootBank).ReadByte(0x2E));
        Assert.Equal(3, Read(frame, RootBank).ReadByte(0x29));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 6).WriteWord(0x5E, 1));
        frame = AdvanceTask(frame, RootBank, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal(6, Read(frame, RootBank).ReadByte(0x29));
        Assert.Equal(0, Read(frame, RootBank).ReadWord(0x5E));
        frame = AdvanceTask(frame, RootBank, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal(7, Read(frame, RootBank).ReadByte(0x29));
    }

    [Fact]
    public void Overlay_wait_uses_retained_color_and_terminal_does_not_emit_or_advance_index()
    {
        Fixture fixture = new();
        (NativeBattleEffect31Frame frame, uint address) = fixture.Task(2, 0x86CF60);
        frame = Write(frame, address, Read(frame, address).WriteDword(0x30, 0x15F8DC8).WriteDword(0x34, 0x123456).WriteWord(0x24, 2));
        frame = AdvanceTask(frame, address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal(0x123456u, Assert.Single(fixture.Operations.OfType<NativeBattleEffect31DrawOverlay>()).Color);
        Assert.Equal(0, Read(frame, address).ReadWord(0x38));
        frame = Write(frame, address, Read(frame, address).WriteWord(0x24, 5));
        frame = AdvanceTask(frame, address, fixture.Resources, fixture.Inputs).Frame;
        Assert.Equal(0x654321u, fixture.Operations.OfType<NativeBattleEffect31DrawOverlay>().Last().Color);
        Assert.Equal(1, Read(frame, address).ReadWord(0x38));
        frame = Write(frame, address, Read(frame, address).WriteWord(0x38, 2));
        NativeBattleEffect31Step result = AdvanceTask(frame, address, fixture.Resources, fixture.Inputs);
        Assert.Equal(2, result.Result);
        Assert.Equal(2, Read(result.Frame, address).ReadWord(0x38));
        Assert.Equal(2, fixture.Operations.OfType<NativeBattleEffect31DrawOverlay>().Count());
    }

    [Fact]
    public void Copied_view_preserves_unknown_padding_and_partial_overwrite_knownness()
    {
        Fixture fixture = new();
        NativeBattleEffect31Frame frame = AdvanceTask(fixture.Frame, RootBank, fixture.Resources, fixture.Inputs).Frame;
        Assert.Throws<NotSupportedException>(() => ReadBytes(frame, 0x2793E58, 32).Read(18, 2));
        frame = WriteBytes(frame, 0x2793E58 + 18, NativeBattleEffectArena.Clear().Write(0, [0xA5, 0x5A]), 2);
        Assert.Equal(new byte[] { 0xA5, 0x5A }, ReadBytes(frame, 0x2793E58, 32).Read(18, 2));
        Assert.Equal(4096, frame.Memory.SharedEffectView2793E58!.ReadMatrix().Rotation[0]);
    }

    [Fact]
    public void Original_sources_bind_once_and_never_replace_current_warm_bytes()
    {
        Fixture fixture = new();
        NativeBattleEffect31Frame frame = fixture.Frame with { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        frame = BindOriginalSources(frame, fixture.Resources);
        Assert.True(frame.State.StaticSourcesBound);
        NativeBattleFileStaging staging = frame.Staging!.Write(0x15F8DC8, [0xA5]);
        frame = frame with { Staging = staging };
        Assert.Same(frame, BindOriginalSources(frame, null!));
        Assert.Equal((byte)0xA5, frame.Staging!.Read(0x15F8DC8, 1)[0]);
        NativeBattleEffect31Frame unknown = fixture.Frame with { State = NativeBattleEffect31State.FromUnknown(), Staging = NativeBattleFileStaging.Unknown() };
        Assert.Throws<NotSupportedException>(() => BindOriginalSources(unknown, fixture.Resources));
        NativeBattleEffect31Frame partial = fixture.Frame with { Staging = NativeBattleFileStaging.Unknown().Write(0x15F8DC8, [1]) };
        Assert.Throws<NotSupportedException>(() => BindOriginalSources(partial, fixture.Resources));
    }

    [Fact]
    public void Custom_context_republishes_its_current_owner_even_on_terminal_inner_state()
    {
        Fixture fixture = new();
        (NativeBattleEffect31Frame frame, uint address) = fixture.Task(3, 0x86D1D0);
        frame = frame with { State = frame.State.WriteDword(ContextGlobal, 0xDEADBEEF) };
        frame = Write(frame, address, Read(frame, address).WriteByte(0x29, 1).WriteWord(0x54, 2));
        NativeBattleEffect31Step result = AdvanceTask(frame, address, fixture.Resources, fixture.Inputs);
        Assert.Equal(2, result.Result);
        Assert.Equal(address + 0x34, result.Frame.State.ReadDword(ContextGlobal));
        Assert.Equal(1, Read(result.Frame, address).ReadWord(0x24));
        Assert.Equal(0, Read(result.Frame, RootBank).ReadByte(0x28));
    }

    private sealed class Fixture
    {
        public NativeBattleEffect31Frame Frame { get; }
        public NativeBattleEffect31Inputs Inputs { get; }
        public NativeBattleEffect31Resources Resources { get; }
        public List<NativeBattleEffect31Operation> Operations { get; } = [];
        public int Contact { get; }

        public Fixture()
        {
            NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[640], new byte[1176]);
            (packets, _) = packets.Prepare(new(3, 8, 13, 0, 26, 31, null));
            (packets, _) = packets.AppendContact(new(new(0, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
            (packets, _) = packets.Publish(1);
            Contact = packets.Headers[0].FirstContactIndex!.Value;
            NativeBattlePackedMatrix32 view = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
            Inputs = new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => packets, (_, actor) => new(actor, 0), (_, _) => throw new NotSupportedException(), _ => view, _ => new(null, 100, 160, 108), _ => 0, (_, _, length) => NativeBattleEffectArena.Clear().MarkUnknown(0, length), (frame, _, _, _, retained) => new(frame, retained), (frame, operation) =>
            {
                Operations.Add(operation);
                return frame;
            });
            Resources = new(null!, null!, ImmutableDictionary<uint, NativeBattleGfAnimationSource>.Empty, ImmutableDictionary<uint, NativeBattleStaticMeshResource>.Empty, ImmutableDictionary<uint, NativeBattleModuleResource>.Empty, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0x15F8DC8, new uint[] { 0xFE050000, 0x00654321, 0xFF000000 }.SelectMany(BitConverter.GetBytes).ToImmutableArray()));
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
            NativeBattleOriginalEffectState effect = new(31, 31, new(31, Entry, Initializer), new(3, 1, 1, 0, 0, 0), null)
            {
                Pools = Pools.Select(shape => NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity)).ToImmutableArray(),
                ReturnedDescriptor = RootDescriptor
            };
            NativeBattleEffect31State state = NativeBattleEffect31State.FromColdProcess().WriteDword(0x26D2DA4, CommonAddress).WriteDword(0x26D2DA0, CommonAddress + 0xD000).WriteDword(0x26D219C, CommonAddress + 0xD000).WriteDword(0x26D2198, CommonAddress + 0x1A000).WriteDword(AllocationCursorGlobal, CommonAddress + 0x1A000);
            for (int index = 0; index < Pools.Length; index++)
            {
                state = state.DetachBank(index).DetachQueue(Pools[index].Descriptor);
            }
            NativeBattleEffect31Frame current = new(memory, effect, new(1), state);
            (current, uint root) = AppendHierarchy(current, 0, RootCallback, 0x64, 0, Inputs);
            Frame = Write(current, root, Read(current, root).WriteDword(12, 0x1D99A78).WriteByte(0x2C, 3).WriteByte(0x2D, 0).WriteWord(0x5A, 1));
        }

        public (NativeBattleEffect31Frame Frame, uint Address) Task(int pool, uint callback) => AppendHierarchy(Frame, pool, callback, Pools[pool].Stride, RootBank, Inputs);
    }
}
