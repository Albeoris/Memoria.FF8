using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleLaserEye71Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLaserEye71NodeTests
{
    [Fact]
    public void Signed_delay_does_not_seed_unknown_source_or_read_unreached_models()
    {
        Fixture fixture = new();
        NativeBattleLaserEye71Frame frame = fixture.Set(fixture.Frame, fixture.Task(fixture.Frame).WriteWord(0x24, unchecked((ushort)-2)).WriteWord(0x298, unchecked((ushort)-1)));
        NativeBattleLaserEye71TaskStep next = fixture.Step(frame);
        Assert.Equal(unchecked((ushort)-1), fixture.Task(next.Frame).ReadWord(0x24));
        Assert.Equal((byte)0, fixture.Task(next.Frame).ReadByte(0x29));
        Assert.Null(next.Frame.State.ExecutableBytes);
        Assert.Equal(JsonSerializer.Serialize(frame.Random), JsonSerializer.Serialize(next.Frame.Random));
    }

    [Fact]
    public void Reached_unknown_source_fails_without_loading_an_immutable_replacement()
    {
        Fixture fixture = new();
        Assert.Throws<NotSupportedException>(() => fixture.Step(fixture.Frame));
    }

    [Fact]
    public void Context_zero_phase_accumulates_real_counts_without_reading_auxiliary_owners()
    {
        Fixture fixture = new();
        NativeBattleLaserEye71Frame frame = fixture.Set(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x29, 1).WriteWord(0x34 + 0x14, 2).WriteWord(0x34 + 0x16, 3));
        frame = frame with { State = frame.State.WriteWord(0x25FCD9A, ushort.MaxValue).WriteWord(0x25FCD8A, ushort.MaxValue) };
        NativeBattleLaserEye71TaskStep next = fixture.Step(frame);
        Assert.Equal((ushort)1, next.Frame.State.ReadWord(0x25FCD9A));
        Assert.Equal((ushort)2, next.Frame.State.ReadWord(0x25FCD8A));
        Assert.Equal((ushort)1, fixture.Task(next.Frame).ReadWord(0x54));
        Assert.Equal(fixture.Address + 0x34, next.Frame.State.ReadDword(0x25FCDD4));
    }

    [Fact]
    public void Warm_bank_mutations_empty_current_lists_and_JSON_next_use_one_source_owner()
    {
        Fixture fixture = new();
        byte[] bytes = new byte[SourceLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 1);
        bytes[^1] = 0xA7;
        NativeBattleLaserEye71Frame frame = fixture.Set(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x29, 1).WriteWord(0x54, 1).WriteWord(0x4E, 1).WriteDword(0x258, SourceAddress + 4));
        frame = frame with { State = frame.State with { ExecutableBytes = bytes.ToImmutableArray() }, Memory = frame.Memory with { Arena = NativeBattleEffectArena.Clear(), SharedEffectView2793E58 = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null) } };
        frame = frame with { State = frame.State.WriteDword(0x25FCD8C, ArenaAddress + EmittersOffset).WriteDword(0x25FCDC8, ArenaAddress + GeometryOffset).WriteWord(0x25FAAE0, 0).WriteWord(0x25FCD98, 0) };
        NativeBattleLaserEye71Frame restored = JsonSerializer.Deserialize<NativeBattleLaserEye71Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleLaserEye71TaskStep next = fixture.Step(frame);
        Assert.Equal((byte)0xA7, next.Frame.State.ExecutableBytes!.Value[^1]);
        Assert.Equal(1u, next.Frame.State.ReadSourceFlag());
        Assert.Equal((ushort)2, fixture.Task(next.Frame).ReadWord(0x54));
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(fixture.Step(restored)));
        NativeBattleLaserEye71TaskStep done = fixture.Step(next.Frame);
        Assert.Equal(2, done.Result);
        Assert.Equal((byte)0, NativeBattleLaserEye71Controller.Read(done.Frame, fixture.Parent).ReadByte(0x28));
    }

    [Theory]
    [InlineData(255)]
    [InlineData(3)]
    public void Imported_phase_outside_native_table_fails(byte phase)
    {
        Fixture fixture = new();
        Assert.Throws<InvalidDataException>(() => fixture.Step(fixture.Set(fixture.Frame, fixture.Task(fixture.Frame).WriteByte(0x29, phase))));
    }

    private sealed class Fixture
    {
        public NativeBattleLaserEye71Frame Frame { get; }
        public uint Address { get; }
        public uint Parent { get; }
        public Fixture()
        {
            NativeBattleEffectHierarchy hierarchy = new(Pools.Select(pool => NativeBattleEffectTaskPool.Initialize(pool.Descriptor, pool.Bank, pool.Stride, pool.Capacity)).ToImmutableArray());
            NativeBattleEffectHierarchyAppend root = hierarchy.Append8DC540(0, NativeBattleLaserEye71Controller.RootCallback, 100);
            Parent = root.Address;
            NativeBattleEffectHierarchyAppend child = root.Hierarchy.Append8DC540(2, NativeBattleLaserEye71Controller.NodeCallback, 676, Parent, (_, _, _) => new(0, 3));
            Address = child.Address;
            NativeBattleModulePoolTask task = child.Hierarchy.Read(Address).WriteDword(0x30, SourceAddress).WriteWord(0x29C, 0);
            NativeBattleOriginalEffectState effect = new(71, 70, new(71, Entry, Initializer), null, null) { Pools = child.Hierarchy.Write(Address, task).Pools };
            Frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(17), NativeBattleLaserEye71State.FromUnknown().WriteWord(0x25FCD9A, 0).WriteWord(0x25FCD8A, 0));
        }
        public NativeBattleModulePoolTask Task(NativeBattleLaserEye71Frame frame) => NativeBattleLaserEye71Controller.Read(frame, Address);
        public NativeBattleLaserEye71Frame Set(NativeBattleLaserEye71Frame frame, NativeBattleModulePoolTask task) => NativeBattleLaserEye71Controller.Write(frame, Address, task);
        public NativeBattleLaserEye71TaskStep Step(NativeBattleLaserEye71Frame frame) => NativeBattleLaserEye71Nodes.Advance(frame, Address, null!, new(NativeBattleActionPacketsState.Fresh(), _ => throw new InvalidOperationException("Unreached model"), _ => throw new InvalidOperationException("Unreached pose"), new(null, 100, 160, 108), 0, NativeBattleGteColorInterpolation.CodeConstants45F270, (_, _, _, _) => throw new InvalidOperationException("Unreached basis"), (_, _) => throw new InvalidOperationException("Unreached packet")));
    }
}
