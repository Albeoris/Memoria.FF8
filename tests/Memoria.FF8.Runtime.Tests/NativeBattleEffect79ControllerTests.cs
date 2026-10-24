using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect79Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect79ControllerTests
{
    [Theory]
    [InlineData(0, 0x8000, 0x10000)]
    [InlineData(1, 0, 0x8000)]
    [InlineData(256, 0x8000, 0x10000)]
    public void Root_uses_low_byte_parity_and_current_view(int parity, int first, int second)
    {
        Harness harness = new();
        NativeBattleEffect79Frame frame = Active();
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(0x5C, (ushort)parity));
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal(0x20DFAB8u + (uint)first, frame.State.ReadDword(CursorGlobal));
        Assert.Equal(0x20DFAB8u + (uint)second, frame.State.ReadDword(0x25E5020));
        Assert.Equal(ViewAddress, frame.State.ReadDword(0x25EB3AC));
        Assert.Equal(1234, frame.Memory.SharedEffectView2793E58!.ReadMatrix().Z);
    }

    [Fact]
    public void Root_clears_exact_current_common_extents()
    {
        Harness harness = new();
        NativeBattleEffect79Frame frame = Active();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0xFFFF, [0xA5]).Write(0x1A5F0, [0x5A]) } };
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 2));
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal(0x20EFAB8u, frame.State.ReadDword(0x25EAF34));
        Assert.Equal(new byte[0xA5F0], frame.Memory.Arena!.Read(0x10000, 0xA5F0));
        Assert.Equal((byte)0xA5, frame.Memory.Arena.Read(0xFFFF, 1)[0]);
        Assert.Equal((byte)0x5A, frame.Memory.Arena.Read(0x1A5F0, 1)[0]);
    }

    [Fact]
    public void Root_music_waits_for_two_distinct_scalar_completions_then_auxiliary_byte()
    {
        Harness harness = new();
        NativeBattleEffect79Frame frame = Active();
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteByte(0x29, 6));
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal(new NativeBattleEffect79QueueFile48D0A0(401, 0x20DFAB8, 0x7BE1E0), Assert.Single(harness.Operations.OfType<NativeBattleEffect79QueueFile48D0A0>()));
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Single(harness.Operations.OfType<NativeBattleEffect79QueueFile48D0A0>());
        frame = CompleteFileCallback(frame, 0x7BE1E0, -1, null!, harness.Inputs());
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Single(harness.Operations.OfType<NativeBattleEffect79StageMusic501A20>());
        Assert.Equal((byte)0, frame.State.Read(0x25DF340, 1)[0]);
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Empty(harness.Operations.OfType<NativeBattleEffect79MusicGain501B10>());
        frame = WriteCompletionByte(frame, 0x25DF340, 1);
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Single(harness.Operations.OfType<NativeBattleEffect79MusicGain501B10>());
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Single(harness.Operations.OfType<NativeBattleEffect79TextureLoad508630>());
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal((byte)10, Read(frame, RootBank).ReadByte(0x29));
        frame = WriteCompletionByte(frame, RootBank + 0x62, 1);
        frame = Advance(frame, null!, harness.Inputs());
        frame = Advance(frame, null!, harness.Inputs());
        Assert.Equal(0u, frame.Effect.ReturnedDescriptor);
        Assert.All(frame.State.FixedBanks.Values, bank => Assert.NotNull(bank));
    }

    [Fact]
    public void Programme_factory_retains_signed_size_and_selector_as_separate_fields()
    {
        Harness harness = new();
        NativeBattleEffect79Frame frame = SpawnProgramme(Active(), RootBank, 0x7BDAD0, 0x19F4880, 0x218, 3, harness.Inputs());
        NativeBattleModulePoolTask child = frame.Effect.Pools[4].Slots[0];
        Assert.Equal(0x218u, child.ReadDword(0x78));
        Assert.Equal((ushort)3, child.ReadWord(0x80));
        Assert.Equal((ushort)0, child.ReadWord(0x82));
        Assert.Equal(0x19F4880u, child.ReadDword(0x74));
    }

    [Fact]
    public void Party_restore_changes_only_saved_visibility_bit_on_enabled_rows()
    {
        Harness harness = new();
        NativeBattleEffect79Frame frame = PartyFlags(Active(), false, harness.Inputs());
        Assert.Equal(3, harness.Operations.OfType<NativeBattleEffect79TargetFlags>().Count());
        Assert.Equal(2u, frame.State.ReadDword(TargetAddress + 0x20));
        harness.Operations.Clear();
        frame = PartyFlags(frame, true, harness.Inputs());
        Assert.All(harness.Operations.OfType<NativeBattleEffect79TargetFlags>(), operation =>
        {
            Assert.True(operation.WordWidth);
            Assert.Equal((ushort)0xFFFB, operation.AndMask);
            Assert.Equal((ushort)0, operation.OrMask);
        });
    }

    private static (NativeBattleEffect79Frame Frame, uint Address) Child(int pool, uint callback, int length, Harness harness) => AppendHierarchy(Active(), pool, callback, length, RootBank, harness.Inputs());

    private static NativeBattleEffect79Frame Active()
    {
        NativeBattleOriginalEffectState effect = new(79, 78, new(79, Entry, Initializer), new(4, 0, 0x7F, 0, 0, 0), null);
        NativeBattleEffect79State state = NativeBattleEffect79State.FromColdProcess().WriteDword(InitializerGlobal, 0x20DFAB8).WriteDword(0x25EA344, 0x20DFAB8).WriteDword(0x25EA340, 0x20DFAB8 + 0x8000).WriteDword(0x25E973C, 0x20DFAB8 + 0x8000).WriteDword(0x25E9738, 0x20DFAB8 + 0x10000).WriteDword(0x25EB3B8, 0x20DFAB8 + 0x10000);
        ImmutableArray<NativeBattleEffectTaskPool> pools = Pools.Select(shape => NativeBattleEffectTaskPool.Initialize(shape.Descriptor, shape.Bank, shape.Stride, shape.Capacity)).ToImmutableArray();
        NativeBattleEffectHierarchyAppend root = new NativeBattleEffectHierarchy(pools).Append8DC540(0, RootCallback, 100);
        for (int index = 0; index < Pools.Length; index++)
        {
            state = state.DetachBank(index);
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() };
        NativeBattleEffect79Frame frame = new(memory, effect with { Pools = root.Hierarchy.Pools, ReturnedDescriptor = RootDescriptor }, NativeCrtRandomState.FromColdProcess(), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        return Write(frame, RootBank, NativeBattleEffectRootTask.Initialize(Read(frame, RootBank), effect.Parameters!, Packets()));
    }

    private static NativeBattleActionPacketsState Packets()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.FromColdLoaderStorage(new byte[NativeBattleActionPacketsState.HeaderCapacity * 20], new byte[NativeBattleActionPacketsState.ContactCapacity * 24]);
        (packets, _) = packets.Prepare(new(4, 8, 16, -1, 351, 79, null));
        (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(0, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(2);
        return packets;
    }

    private sealed class Harness
    {
        public List<NativeBattleEffect79Operation> Operations { get; } = [];

        public NativeBattleEffect79Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, frame => Packets(), frame => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 1234 }, null), frame => new(null, 100, 160, 108), (frame, index) => new(index, 2), (frame, index) => throw new NotSupportedException("This scoped phase does not read a pose source."), frame => 0, (frame, source) => source == 0xB8B7D8 ? 0x12345678u : source == 0xB8B9A8 ? 0x90ABCDEFu : throw new InvalidDataException(), (frame, operation) =>
        {
            Operations.Add(operation);
            return frame;
        });
    }
}
