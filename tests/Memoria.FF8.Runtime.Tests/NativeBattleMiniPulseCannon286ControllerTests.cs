using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleMiniPulseCannon286Controller;
using static Memoria.FF8.Runtime.NativeBattleMiniPulseCannon286Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMiniPulseCannon286ControllerTests
{
    [Fact]
    public void Entry_defers_common_reset_and_initializes_only_the_physical_root()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        Assert.Single(frame.Effect.Pools);
        Assert.Null(frame.State.RootBankStorage);
        Assert.Equal(new byte[8], TaskView(Read(frame, RootBank), 12, 8).Read(0, 8));
        Assert.Equal(ModelAddress(3), frame.State.ReadDword(CasterGlobal));
        Assert.Equal((byte)0xA5, frame.Memory.Arena!.Read(0x480, 1)[0]);
        Assert.Equal(new NativeCrtRandomState(123), frame.Random);
    }

    [Theory]
    [InlineData(0u, 0x10FD4u, 1u)]
    [InlineData(1u, 0xFD4u, 0u)]
    [InlineData(0x80000000u, 0xFD4u, 0u)]
    public void Root_parity_reads_full_DWORD_and_ignores_graphics_pause(uint old, uint offset, uint next)
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteDword(16, old));
        var actual = AdvanceTask(frame, RootBank, Resources(), Inputs([]) with { GraphicsFlags = 0x201 }).Frame;
        Assert.Equal(ArenaAddress + offset, actual.State.ReadDword(CursorGlobal));
        Assert.Equal(next, Read(actual, RootBank).ReadDword(16));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Fact]
    public void Setup_keeps_full_anchor_and_four_workspaces_then_composes_and_restores()
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        List<NativeBattleMiniPulseCannon286Operation> operations = [];
        int anchors = 0;
        var inputs = Inputs(operations) with { ReadAnchor571400 = (current, model) =>
        {
            anchors++;
            Assert.Equal(ModelAddress(0), model);
            return new(current with { Random = current.Random.Draw(out _) }, PointView(new(-123, 456, -789), -222));
        } };
        var actual = AdvanceTask(frame, RootBank, Resources(), inputs).Frame;
        Assert.Equal(1, anchors);
        Assert.Equal(new uint[] { ProgrammeAAddress, ProgrammeBAddress, ProgrammeCAddress, ProgrammeDAddress }, new[] { 28, 584, 928, 1604 }.Select(offset => Read(actual, Pools[2].Bank).ReadDword(offset)));
        Assert.Equal(PointView(new(-123, 456, -789), -222).Read(0, 8), TaskView(Read(actual, Pools[2].Bank), 20, 8).Read(0, 8));
        Assert.Equal(ModelAddress(0), Read(actual, Pools[2].Bank).ReadDword(16));
        Assert.IsType<NativeBattleMiniPulseCannon286ComposeCaster5095B0>(operations[0]);
        Assert.IsType<NativeBattleMiniPulseCannon286RestoreCaster508C90>(operations[1]);
        Assert.Equal(2, operations.Count);
        Assert.Equal((ushort)1, Read(actual, Pools[2].Bank).ReadWord(12));
        Assert.Throws<NotSupportedException>(() => actual.Memory.Arena!.Read(0x480, 1));
    }

    [Theory]
    [InlineData((ushort)23, 0, 0)]
    [InlineData((ushort)24, 1, 0)]
    [InlineData((ushort)53, 0, 1)]
    [InlineData((ushort)54, 0, 0)]
    public void Sound_and_single_physical_contact_have_exact_old_ages(ushort age, int sounds, int contacts)
    {
        var frame = MainAt(age);
        List<NativeBattleMiniPulseCannon286Operation> operations = [];
        var actual = AdvanceTask(frame, Pools[2].Bank, Resources(), Inputs(operations)).Frame;
        Assert.Equal(sounds, operations.OfType<NativeBattleMiniPulseCannon286Sound501330>().Count());
        Assert.Equal(contacts, operations.OfType<NativeBattleMiniPulseCannon286Contact506690>().Count());
        if (contacts != 0)
        {
            Assert.Equal(1, Assert.Single(operations.OfType<NativeBattleMiniPulseCannon286Contact506690>()).ContactIndex);
        }
        if (sounds != 0)
        {
            Assert.Equal(new NativeBattleMiniPulseCannon286Sound501330(Resources().Sound, 0, 128), Assert.Single(operations.OfType<NativeBattleMiniPulseCannon286Sound501330>()));
        }
        Assert.Equal(unchecked((ushort)(age + 1)), Read(actual, Pools[2].Bank).ReadWord(12));
        Assert.Equal(frame.Random, actual.Random);
    }

    [Theory]
    [InlineData((ushort)25)]
    [InlineData((ushort)32)]
    public void Successful_pair_consumes_six_CRT_draws_and_writes_native_payload(ushort age)
    {
        var frame = MainAt(age);
        frame = Write(frame, Pools[1].Bank, Read(frame, Pools[1].Bank).MarkPayloadUnknown(26, 2));
        var actual = AdvanceTask(frame, Pools[2].Bank, Resources(), Inputs([])).Frame;
        var random = frame.Random;
        for (int index = 0; index < 6; index++)
        {
            random = random.Draw(out _);
        }
        Assert.Equal(random, actual.Random);
        for (int index = 0; index < 2; index++)
        {
            var task = actual.Effect.Pools[1].Slots[index];
            Assert.Equal(SpriteAddress, task.ReadDword(12));
            Assert.Equal(1200u, task.ReadDword(16));
            Assert.Equal((ushort)0, task.ReadWord(26));
            Assert.Equal((ushort)10, task.ReadWord(34));
        }
        Assert.Equal((ushort)0, actual.Effect.Pools[1].Slots[0].ReadWord(26));
    }

    [Fact]
    public void Exhausted_sprite_pool_breaks_before_random_draws()
    {
        var frame = MainAt(25);
        for (int index = 0; index < 32; index++)
        {
            (frame, _) = Append(frame, Pools[1].Descriptor, 0x67A7C0);
        }
        var actual = AdvanceTask(frame, Pools[2].Bank, Resources(), Inputs([])).Frame;
        Assert.Equal(frame.Random, actual.Random);
    }

    [Theory]
    [InlineData((ushort)33)]
    [InlineData((ushort)66)]
    [InlineData((ushort)67)]
    [InlineData((ushort)68)]
    public void No_sprite_birth_after_eighth_frame_and_main_retires_at_new_69(ushort age)
    {
        var frame = MainAt(age);
        var actual = AdvanceTask(frame, Pools[2].Bank, Resources(), Inputs([]));
        Assert.Equal(age == 68 ? 2 : 0, actual.Result);
        Assert.Equal(frame.Random, actual.Frame.Random);
        Assert.Null(actual.Frame.Effect.Pools[1].Head);
    }

    [Fact]
    public void Retired_main_clears_only_actor_bit_eight_then_restores_before_sprite_scan()
    {
        var frame = MainAt(68);
        List<NativeBattleMiniPulseCannon286Operation> operations = [];
        var actual = Advance(frame, Resources(), Inputs(operations));
        Assert.Equal(new NativeBattleMiniPulseCannon286ActorFlags(ModelAddress(3), 8, 0), Assert.Single(operations.OfType<NativeBattleMiniPulseCannon286ActorFlags>()));
        var ordered = operations.Where(operation => operation is NativeBattleMiniPulseCannon286ComposeCaster5095B0 or NativeBattleMiniPulseCannon286ActorFlags or NativeBattleMiniPulseCannon286RestoreCaster508C90).ToArray();
        Assert.IsType<NativeBattleMiniPulseCannon286ComposeCaster5095B0>(ordered[0]);
        Assert.IsType<NativeBattleMiniPulseCannon286ActorFlags>(ordered[1]);
        Assert.IsType<NativeBattleMiniPulseCannon286RestoreCaster508C90>(ordered[2]);
        Assert.Equal(3, ordered.Length);
        int clearIndex = operations.FindIndex(operation => operation is NativeBattleMiniPulseCannon286ActorFlags);
        Assert.All(operations.Select((operation, index) => (operation, index)).Where(item => item.operation is NativeBattleMiniPulseCannon286DrawWindow108), item => Assert.InRange(item.index, 1, clearIndex - 1));
        Assert.Equal(0u, actual.Effect.ReturnedDescriptor);
        Assert.Empty(actual.Effect.Pools);
        Assert.NotNull(actual.State.RootBankStorage);
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleMiniPulseCannon286Frame>(NativeBattleTestSnapshots.Json(actual))!;
        Assert.Single(Initialize(restored, Resources(), Inputs([])).Effect.Pools);
    }

    private static NativeBattleMiniPulseCannon286Frame MainAt(ushort age)
    {
        var frame = Initialize(Inactive(), Resources(), Inputs([]));
        frame = Write(frame, RootBank, Read(frame, RootBank).WriteWord(12, 1));
        frame = AdvanceTask(frame, RootBank, Resources(), Inputs([])).Frame;
        frame = Write(frame, Pools[2].Bank, Read(frame, Pools[2].Bank).WriteWord(12, age));
        if (age >= 67)
        {
            frame = WriteBytes(frame, Pools[2].Bank + 928 + 4, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(age - 49)), 4);
        }
        return frame;
    }

    private static NativeBattleMiniPulseCannon286Frame Inactive()
    {
        byte[] tim = new byte[32];
        BitConverter.GetBytes(12).CopyTo(tim, 8);
        BitConverter.GetBytes(12).CopyTo(tim, 20);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x30000, tim).Write(0x480, new byte[] { 0xA5 }), ModuleSpriteBillboard21DFED0 = Identity() };
        NativeBattleOriginalEffectState effect = new(286, 285, new(286, Entry, Initializer), new(3, 0, 0x7F, 1, 0, 0), null) { ResourcePointers = [new(InitializerGlobal, 0x30000)] };
        return new(memory, effect, new(123), NativeBattleMiniPulseCannon286State.FromColdProcess()) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
    }

    private static NativeBattleMiniPulseCannon286Inputs Inputs(List<NativeBattleMiniPulseCannon286Operation> operations)
    {
        var packets = Packets();
        return new(packets, NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => Identity(), _ => new(null, 100, 160, 108), (_, model) => Model(model), (_, _, _) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = 1000, Y = 2000, Z = 3000 }, null), (_, _) => Identity(), (current, model) => new(current, PointView(Model(model).Transform!.Position28, Model(model).Transform!.Angle36)), _ => packets, (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
    }

    private static NativeBattleActionPacketsState Packets()
    {
        var packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(3, 8, 12, -1, 293, 286, null));
        (packets, _) = packets.AppendContact(new(new(6, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        (packets, _) = packets.Prepare(new(3, 8, 12, -1, 293, 286, null));
        (packets, _) = packets.AppendContact(new(new(0, 4, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        return packets;
    }

    private static NativeBattleMiniPulseCannon286Resources Resources()
    {
        var resources = NativeBattleMiniPulseCannon286StateTests.Resources();
        byte[] bytes = resources.ProgrammeC.Data.ToArray();
        for (int index = 0; index < 9; index++)
        {
            int node = 0x200 + index * 64;
            Put(0x108 + index * 4, node - 0x100);
            Put(node, 0);
            Put(node + 4, 0x40);
            Put(node + 8, 12);
            Put(node + 12, 18);
            Put(node + 16, 0);
            Put(node + 20, 0);
            Put(node + 24, 0);
            Put(node + 28, 19);
            Put(node + 32, 0);
            Put(node + 36, 0);
            Put(node + 40, 0);
            Put(node + 44, -1);
        }
        resources = resources with { ProgrammeC = resources.ProgrammeC with { Data = bytes.ToImmutableArray() } };
        resources.Validate();
        return resources;

        void Put(int offset, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
        }
    }
    private static NativeBattlePackedMatrix32 Identity() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
    private static uint ModelAddress(int actor) => 0x1D972C0u + (uint)(actor * 156);
    private static NativeBattleGraphicsModel Model(uint address)
    {
        int actor = checked((int)((address - 0x1D972C0) / 156));
        return new(actor, 2) { Rotation14 = 0, Transform = new(new(0, 0, 0), new(100, 200, 1000), (short)(444 + actor), 777, 4096) };
    }
}
